local constants = require 'constants'
local NonUniformQuantizer = require 'lib.math.non_uniform_quantizer'
local Point2 = _radiant.csg.Point2
local Rect2 = _radiant.csg.Rect2
local Region2 = _radiant.csg.Region2
local math_floor = math.floor
local math_ceil = math.ceil

local Biome = class()

function Biome:__init(biome_alias, config_file_path)
   self._terrain_types = { 'plains', 'foothills', 'mountains' }
   self._tile_size = constants.terrain.TILE_SIZE
   self._macro_block_size = constants.terrain.MACRO_BLOCK_SIZE
   self._feature_block_size = constants.terrain.FEATURE_BLOCK_SIZE
   self._slice_size = constants.mining.Y_CELL_SIZE

   assert(self._tile_size % self._macro_block_size == 0)
   assert(self._macro_block_size / self._feature_block_size == 2)

   self._landscape_info = nil
   self._terrain_detail_info = nil
   self._terrain_info = nil
   self._season = nil
   
   
   
   
   self._palettes = nil
   self._biome_alias = biome_alias

   local json = radiant.resources.load_json(config_file_path)
   self._landscape_info = json.landscape
   self._terrain_detail_info = json.terrain_detailer
   self._terrain_info = self:_generate_terrain_info(json)
   self._season = json.season
   self._palettes = json.palettes
   self._json_path = config_file_path

   self:_generate_quantizers()
   self:_generate_color_map()
   self:_generate_custom_name_map()
end

function Biome:get_biome_alias()
   assert(self._biome_alias)
   return self._biome_alias
end

function Biome:get_json_path()
   assert(self._json_path)
   return self._json_path
end

function Biome:get_terrain_types()
   return self._terrain_types
end

function Biome:get_tile_size()
   return self._tile_size
end

function Biome:get_macro_block_size()
   return self._macro_block_size
end

function Biome:get_feature_block_size()
   return self._feature_block_size
end

function Biome:get_slice_size()
   return self._slice_size
end

function Biome:get_terrain_info()
   return self._terrain_info
end

function Biome:get_landscape_info()
   return self._landscape_info
end

function Biome:get_terrain_detail_info()
   return self._terrain_detail_info
end

function Biome:get_quantizer()
   return self._quantizer
end

function Biome:get_mountains_quantizer()
   return self._mountains_quantizer
end

function Biome:get_terrain_type(height)
   local terrain_info = self._terrain_info

   if height <= terrain_info.plains.height_max then
      return 'plains'
   elseif height <= terrain_info.foothills.height_max then
      return 'foothills'
   else
      return 'mountains'
   end
end

-- takes quantized height values
function Biome:get_terrain_type_and_step(height)
   local terrain_info = self._terrain_info
   local terrain_type = self:get_terrain_type(height)
   local height_base = terrain_info[terrain_type].height_base
   local step_size = terrain_info[terrain_type].step_size
   local step_number = (height - height_base) / step_size
   return terrain_type, step_number
end

-- takes quantized height values
function Biome:get_terrain_code(height)
   local terrain_type, step = self:get_terrain_type_and_step(height)
   local terrain_code = self:_assemble_terrain_code(terrain_type, step)
   return terrain_code
end

function Biome:_assemble_terrain_code(terrain_type, step)
   return string.format('%s_%d', terrain_type, step)
end

function Biome:get_color_map()
   return self._color_map
end

function Biome:get_custom_name_map()
   return self._custom_name_map
end

-- convert world coordinates to the index of the feature cell
function Biome:get_feature_index(x, y)
   local feature_size = self._feature_block_size
   return math_floor(x / feature_size),
          math_floor(y / feature_size)
end

-- convert world dimensions into feature dimensions
function Biome:get_feature_dimensions(width, length)
   local feature_size = self._feature_block_size
   return math_ceil(width / feature_size),
          math_ceil(length / feature_size)
end

function Biome:region_to_feature_space(region)
   local new_region = Region2()

   for rect in region:each_cube() do
      local new_rect = self:rect_to_feature_space(rect)
      -- can't use add_unique_cube because of quantization to reduced coordinate space
      new_region:add_cube(new_rect)
   end

   return new_region
end

function Biome:rect_to_feature_space(rect)
   local feature_size = self._feature_block_size
   local min = rect.min
   local max = rect.max

   min = Point2(math_floor(min.x / feature_size),
                math_floor(min.y / feature_size))

   if rect:get_area() == 0 then
      max = min
   else
      max = Point2(math_ceil(max.x / feature_size),
                   math_ceil(max.y / feature_size))
   end

   return Rect2(min, max)
end

function Biome:_generate_terrain_info(json)
   -- yikes, should really clone this first
   local terrain_info = json.terrain
   local slice_size = self._slice_size

   assert(terrain_info.foothills.step_size % slice_size == 0)
   assert(terrain_info.mountains.step_size % slice_size == 0)

   -- compute terrain elevations
   local prev_terrain_type
   for i, terrain_type in ipairs(self._terrain_types) do
      local base
      if terrain_type == 'plains' then
         base = terrain_info.height_base
      else
         base = terrain_info[prev_terrain_type].height_max
      end
      terrain_info[terrain_type].height_base = base
      terrain_info[terrain_type].height_max = base + terrain_info[terrain_type].step_size * terrain_info[terrain_type].step_count
      prev_terrain_type = terrain_type
   end

   terrain_info.plains.height_valley  = terrain_info.height_base + terrain_info.plains.valley_count * terrain_info.plains.step_size
   assert(terrain_info.plains.height_max % slice_size == 0)
   terrain_info.height_min = terrain_info.plains.height_valley
   terrain_info.height_max = terrain_info.mountains.height_max

   return terrain_info
end

-- generate quantizers for height map quantization
function Biome:_generate_quantizers()
   local centroids = self:_get_terrain_elevations()
   self._quantizer = NonUniformQuantizer(centroids)

   local mountains_centroids = self:_get_mountain_elevations()
   self._mountains_quantizer = NonUniformQuantizer(mountains_centroids)
end

function Biome:_get_terrain_elevations()
   local terrain_info = self._terrain_info
   local elevations = {}

   for i, terrain_type in ipairs(self._terrain_types) do
      local min = terrain_info[terrain_type].height_base + terrain_info[terrain_type].step_size
      local max = terrain_info[terrain_type].height_max
      local step_size = terrain_info[terrain_type].step_size
      for value = min, max, step_size do
         table.insert(elevations, value)
      end
   end

   return elevations
end

-- compute mountain elevations for underground mountains
function Biome:_get_mountain_elevations()
   local terrain_info = self._terrain_info
   local elevations = {}

   local min = terrain_info.mountains.height_base % terrain_info.mountains.step_size
   local max = terrain_info.mountains.height_max
   local step_size = terrain_info.mountains.step_size
   for value = min, max, step_size do
      table.insert(elevations, value)
   end

   return elevations
end

function Biome:_generate_color_map()
   local palette = self._palettes[self._season]
   local minimap_palette = self._palettes.minimap
   local elevations = self:_get_terrain_elevations()
   local color_map = {
      water = minimap_palette and minimap_palette.water or '#1CBFFF',
      trees = minimap_palette and minimap_palette.trees or '#263C2C'
   }

   for _, elevation in ipairs(elevations) do
      local terrain_type, step = self:get_terrain_type_and_step(elevation)
      local terrain_code = self:_assemble_terrain_code(terrain_type, step)
      local color

      if terrain_type == 'plains' then
         color = step <= 1 and palette.dirt or palette.grass
      elseif terrain_type == 'foothills' then
         color = palette.grass_hills
      elseif terrain_type == 'mountains' then
         color = palette['rock_layer_' .. step]
      else
         error('unknown terrain type')
      end

      if minimap_palette and minimap_palette[terrain_code] then
         color = minimap_palette[terrain_code]
      end

      color_map[terrain_code] = color
   end

   self._color_map = color_map
end

function Biome:_generate_custom_name_map()
   local minimap_terrain = self._terrain_info.minimap
   local elevations = self:_get_terrain_elevations()
   local custom_name_map = {
      water = minimap_terrain and minimap_terrain.water or 'stonehearth:ui.shell.select_settlement.terrain_codes.water'
   }

   for _, elevation in ipairs(elevations) do
      local terrain_type, step = self:get_terrain_type_and_step(elevation)
      local terrain_code = self:_assemble_terrain_code(terrain_type, step)
      local custom_name = 'stonehearth:ui.shell.select_settlement.terrain_codes.' .. terrain_code

      if minimap_terrain and minimap_terrain[terrain_code] then
         custom_name = minimap_terrain[terrain_code]
      end

      custom_name_map[terrain_code] = custom_name
   end

   self._custom_name_map = custom_name_map
end

return Biome
