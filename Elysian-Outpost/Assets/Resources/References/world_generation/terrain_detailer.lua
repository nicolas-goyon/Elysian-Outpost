local Biome = require 'services.server.world_generation.biome'
local Array2D = require 'services.server.world_generation.array_2D'
local SimplexNoise = require 'lib.math.simplex_noise'
local FilterFns = require 'services.server.world_generation.filter.filter_fns'
local InverseGaussianRandom = require 'lib.math.inverse_gaussian_random'
local TerrainDetailer = class()

local log = radiant.log.create_logger('world_generation.detailer')

-- note that the tile_width and tile_height passed in are currently the oversize width and height
function TerrainDetailer:__init(biome, rng, seed, tile_width, tile_height)
   self._biome = biome
   self._seed = seed
   self._max_layers = 0
   self._tile_width = tile_width
   self._tile_height = tile_height
   self._detail_info = self._biome:get_terrain_detail_info()
   self._terrain_info = self._biome:get_terrain_info()
   self:_initialize_function_table()
   self._edge_map_buffer = Array2D(self._tile_width, self._tile_height)
end

function TerrainDetailer:_initialize_function_table()
   local detail_depth = {}
   local functions = {}
   functions.plains = self:_initialize_plains_detailer()
   functions.foothills = self:_initialize_detailer('foothills')
   functions.mountains = self:_initialize_detailer('mountains')
   self._function_table = functions
end

--plains detailer is just a simple protrusion for plain depressions
function TerrainDetailer:_initialize_plains_detailer()
   local fns = {}
   local depth_fn = function(x,y)
      return 1
   end
   local height_fn = function(x,y)
      return 1
   end
   fns.depth_function = depth_fn
   fns.height_function = height_fn
   return fns
end

function TerrainDetailer:_initialize_detailer(terrain_type)
   local fns = {}
   local config = self._detail_info[terrain_type]
   local step_size = self._terrain_info[terrain_type].step_size

   local depth_layer_count = config.depth_function.layer_count
   if depth_layer_count > self._max_layers then self._max_layers = depth_layer_count end

   --returns number of layers of protrusion we should have
   local depth_fn = function(x,y)
      local depth_config = config.depth_function
      local bandlimit = depth_config.unit_length
      local layer_count = depth_config.layer_count
      local mean = 0.5*layer_count
      local range = depth_config.amplitude * layer_count
      --round to quantized values and get noise
      local q_x, q_y = x - (x-1)%depth_config.unit_length, y - (y-1)%depth_config.unit_length
      local depth = SimplexNoise.proportional_simplex_noise(depth_config.octaves, depth_config.persistence_ratio,
                                                            bandlimit, mean, range, 1, self._seed, q_x, q_y)
      local result = radiant.math.round(depth)
      if result < 1 then return 1 end
      if result > layer_count then return layer_count end
      return result
   end

   --returns offset of height from terrain step maximum
   local height_fn = function(x,y)
      local height_config = config.height_function
      local bandlimit = height_config.unit_length
      local layer_thickness = height_config.layer_thickness
      local mean = 0.5 * step_size
      local range = height_config.amplitude * step_size
      --round to quantized values and get noise
      local q_x, q_y = x - (x-1)%height_config.unit_length, y - (y-1)%height_config.unit_length
      local height = SimplexNoise.proportional_simplex_noise(height_config.octaves, height_config.persistence_ratio,
                                                             bandlimit, mean, range, 1, self._seed, q_x, q_y)
      local result = layer_thickness * radiant.math.round(height / layer_thickness)
      if result < 0 then return 0 end
      if result > step_size then return step_size end
      return result
   end

   --TODO bring it into the json file
   --this basically determines the height offset of layers 2 and up, depending on the previous layer
   local inset_fn = function(x,y)
      local bandlimit = 4
      local mean = 1.5
      local range = 5
      local unit_length = 4
      local q_x, q_y = x - (x-1)%unit_length, y - (y-1)%unit_length
      local height = SimplexNoise.proportional_simplex_noise(3, 0.02, bandlimit, mean, range, 1, self._seed, q_y, q_x)
      local result = radiant.math.round(height)
      if result < 1 then return 1 end
      return result
   end

   fns.depth_function = depth_fn
   fns.height_function = height_fn
   fns.inset_function = inset_fn
   return fns
end

function TerrainDetailer:_get_edge_normals(tile_map, x,y)
   local offset = tile_map:get_offset(x, y)
   local altitude = tile_map[offset]
   local terrain_type = self._biome:get_terrain_type(altitude)
   local max_delta = self._terrain_info[terrain_type].step_size
   local width = tile_map.width
   local height = tile_map.height
   local neighbor_altitude
   local normals = {}

   if x > 1 then
      neighbor_altitude = tile_map[offset-1]
      if altitude - neighbor_altitude >= max_delta then
         table.insert(normals, { x = -1, y = 0 })
      end
   end

   if x < width then
      neighbor_altitude = tile_map[offset+1]
      if altitude - neighbor_altitude >= max_delta then
         table.insert(normals, { x = 1, y = 0 })
      end
   end

   if y > 1 then
      neighbor_altitude = tile_map[offset-width]
      if altitude - neighbor_altitude >= max_delta then
         table.insert(normals, { x = 0, y = -1 })
      end
   end

   if y < height then
      neighbor_altitude = tile_map[offset+width]
      if altitude - neighbor_altitude >= max_delta then
         table.insert(normals, { x = 0, y = 1 })
      end
   end

   return normals
end

--high level function
function TerrainDetailer:detail(global_i,global_j,tile_map, micro_map)
   --to prevent multiple subtractions to the corners, we have to keep track of the original map
   local old_map = tile_map:clone()
   local edge_map = self._edge_map_buffer
   local traversed = Array2D(tile_map.width, tile_map.height)
   local depth_map = Array2D(tile_map.width, tile_map.height)

   -- fast way to initialize entire array
   traversed:clear(false)
   depth_map:clear(0)

   for j=1, tile_map.height do
      for i=1, tile_map.width do
         local normals = self:_get_edge_normals(tile_map, i, j)
         edge_map:set(i, j, normals)
      end
   end

   --set offset wrt global coordinates of tile
   local tile_size = self._biome:get_tile_size()
   self.global_offset_i = tile_size * global_i --0.5*self._biome:get_macro_block_size()
   self.global_offset_j = tile_size * global_j --0.5*self._biome:get_macro_block_size()

   --add details
   if self._max_layers > 0 then
      self:_detail_first_layer_and_set_depth(tile_map, old_map, edge_map, depth_map, traversed)
   end
   for layer = 2, self._max_layers do
      self:_detail_layer(layer, tile_map, old_map, edge_map, depth_map, traversed)
   end
end

function TerrainDetailer:_detail_first_layer_and_set_depth(tile_map, old_map, edge_map, depth_map, traversed)
   local width = tile_map.width
   local height = tile_map.height
   local x, y
   local detail_info = self._detail_info

   for j=1, height do
      for i=1, width do
         x, y = self.global_offset_i+i, self.global_offset_j+j
         local normals = edge_map:get(i,j)
         --if is edge
         if #normals > 0 then
            --initialize detailing parameters
            local old_altitude = old_map:get(i,j)
            local terrain_type = self._biome:get_terrain_type(old_altitude)

            if detail_info[terrain_type].depth_function.layer_count > 0 then
               local step_size = self._terrain_info[terrain_type].step_size
               local base = old_altitude - step_size
               --set depth
               local depth_function = self._function_table[terrain_type].depth_function
               local depth = depth_function(x, y)
               depth_map:set(i,j,depth)
               --obtain height
               local height_function  = self._function_table[terrain_type].height_function
               local altitude = old_altitude - height_function(x,y)
               --snap so that there are no detailing chunks of offset one from base
               altitude = self:_snap_altitude(altitude, base, step_size)
               --set height of entire protrusion layer
               local depth_layer_thickness = detail_info[terrain_type].depth_function.layer_thickness
               local permutes = self:_get_normal_permutes(normals, terrain_type)

               for _, permute in ipairs(permutes) do
                  for index = 1, depth_layer_thickness do
                     local i_protrude = i + index * permute.x
                     local j_protrude = j + index * permute.y
                     self:_set_protrusion(altitude, i_protrude, j_protrude, tile_map, traversed)
                  end
               end
            end
         end
      end
   end
end

function TerrainDetailer:_detail_layer(layer,tile_map, old_map, edge_map, depth_map, traversed)
   local width = tile_map.width
   local height = tile_map.height
   local detail_info = self._detail_info

   for j=1, height do
      for i=1, width do
         --check if edge has at least a depth of "layer"
         if depth_map:get(i, j) >= layer then
            local normals = edge_map:get(i, j)
            if #normals == 1 then
               --initialization
               local normal = normals[1]
               local old_altitude = old_map:get(i,j)
               local terrain_type = self._biome:get_terrain_type(old_altitude)
               local step_size = self._terrain_info[terrain_type].step_size
               local base = old_altitude - step_size
               local depth_layer_thickness = detail_info[terrain_type].depth_function.layer_thickness
               local height_layer_thickness = detail_info[terrain_type].height_function.layer_thickness
               --base is the base of this protrusion layer
               local i_base = i + (layer-1)*depth_layer_thickness*normal.x
               local j_base = j + (layer-1)*depth_layer_thickness*normal.y
               --set height of protrusion layer
               local offset = self:_edge_border_offset(tile_map, height_layer_thickness, i_base, j_base, normal.x, normal.y)
               local inset_function = self._function_table[terrain_type].inset_function
               local altitude = tile_map:get(i_base,j_base) - height_layer_thickness*(inset_function(i_base,j_base)) - offset
               --snap so that there are no detailing chunks of offset one from base
               altitude = self:_snap_altitude(altitude, base, step_size)
               --set protrusion layer
               for index = 1, depth_layer_thickness do
                  local i_protrude = i_base + index*normal.x
                  local j_protrude = j_base + index*normal.y
                  self:_set_protrusion(altitude, i_protrude, j_protrude, tile_map, traversed)
               end
            end
         end
      end
   end
end

function TerrainDetailer:_snap_altitude(altitude, base, step_size)
   if step_size > 2 and altitude <= base + 1 then
      altitude = base
   end
   return altitude
end

function TerrainDetailer:_get_normal_permutes(normals, terrain_type)
   local permutes = {}

   if terrain_type ~= 'plains' and #normals > 1 then
      -- don't detail convex corners on foothills and mountains
      -- too difficult and looks bad
      return permutes
   end

   assert(#normals <= 2)
   local sum

   if #normals == 2 then
      sum = { x = 0, y = 0 }
   end

   for _, normal in ipairs(normals) do
      table.insert(permutes, normal)
      if #normals == 2 then
         sum.x = sum.x + normal.x
         sum.y = sum.y + normal.y
      end
   end

   if #normals == 2 then
      table.insert(permutes, sum)
   end

   return permutes
end

function TerrainDetailer:_set_protrusion(altitude, i, j, tile_map, traversed)
   if not tile_map:in_bounds(i, j) then
      return
   end

   local current_altitude = tile_map:get(i, j)
   -- check edge case for concave corners
   if altitude > current_altitude or not traversed:get(i, j) then
      tile_map:set(i, j, altitude)
      traversed:set(i, j, true)
   end
end

--returns offset if point(i,j) is higher than a neighbor by some threshold. this is used to generate proper insets.
function TerrainDetailer:_edge_border_offset(tile_map, threshold, i, j, dx, dy)
   local altitude = tile_map:get(i,j)
   local neighbor_delta
   --case handling for tile boundaries so that i+dy, x+dx doesn't go out of bounds.
   -- there might be a better way to do this?
   local neighbor_i = i+dy
   local neighbor_j = j+dx

   if tile_map:in_bounds(neighbor_i, neighbor_j) then
      neighbor_delta = altitude - tile_map:get(i+dy, j+dx)
      if neighbor_delta >= threshold then
         return neighbor_delta
      end
   end

   neighbor_i = i-dy
   neighbor_j = j-dx

   if tile_map:in_bounds(neighbor_i, neighbor_j) then
      neighbor_delta = altitude - tile_map:get(i-dy, j-dx)
      if neighbor_delta >= threshold then
         return neighbor_delta
      end
   end

   return 0
end

return TerrainDetailer
