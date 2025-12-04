local Biome = require 'services.server.world_generation.biome'
local Array2D = require 'services.server.world_generation.array_2D'
local SimplexNoise = require 'lib.math.simplex_noise'
local FilterFns = require 'services.server.world_generation.filter.filter_fns'
local ValleyShapes = require 'services.server.world_generation.valley_shapes'
local Timer = require 'services.server.world_generation.timer'
local log = radiant.log.create_logger('world_generation')

local MicroMapGenerator = class()

function MicroMapGenerator:__init(biome,rng,seed)
   self._biome = biome
   self._tile_size = self._biome:get_tile_size()
   self._macro_block_size = self._biome:get_macro_block_size()
   self._feature_size = self._biome:get_feature_block_size()
   self._terrain_info = self._biome:get_terrain_info()
   self._rng = rng
   self._seed = seed
   self._macro_blocks_per_tile = self._tile_size / self._macro_block_size

   self._valley_shapes = ValleyShapes(rng)
end

-- recall that micro_maps are offset by half a macro_block for shared tiling boundaries
local function _elevation_map_to_micro_map_index(i)
   -- offset 1 element to skip over micro_map overlap
   -- convert to 0 based array, scale, convert back to 1 based array
   return math.floor((i+1-1) * 0.5) + 1
end

local function _elevation_map_to_micro_map_coords(i, j)
   return _elevation_map_to_micro_map_index(i), _elevation_map_to_micro_map_index(j)
end

function MicroMapGenerator:generate_micro_map(size_x, size_y)
   local micro_map, width, height

   -- create the noise map
   micro_map = self:generate_noise_map(size_x, size_y)

   --stylistic interpolating with tile centroids based on distance to centroid, creates chunkiness on the macroscale
   --self:_blockify(micro_map, size_x, size_y)

   self:_match_plains_percentile(micro_map)
   self:_accentuate_mountains(micro_map)
   -- quantize it
   self:_quantize(micro_map)
   -- postprocess it
   self:_postprocess(micro_map)

   local elevation_map = self:_convert_to_elevation_map(micro_map)

   return micro_map, elevation_map
end

-- given a surface micro map, generate the underground micro map that indicates rock elevations
function MicroMapGenerator:generate_underground_micro_map(surface_micro_map)
   local mountains_info = self._terrain_info.mountains
   local mountains_base_height = mountains_info.height_base
   local mountains_step_size = mountains_info.step_size
   local rock_line = mountains_step_size
   local width, height = surface_micro_map:get_dimensions()
   local size = width*height
   local unfiltered_map = Array2D(width, height)
   local underground_micro_map = Array2D(width, height)

   -- seed the map using the above ground mountains
   for i=1, size do
      local surface_elevation = surface_micro_map[i]
      local value

      if surface_elevation > mountains_base_height then
         value = surface_elevation
      else
         value = math.max(surface_elevation - mountains_step_size*2, rock_line)
      end

      unfiltered_map[i] = value
   end

   -- filter the map to generate the underground height map
   FilterFns.filter_2D_0125(underground_micro_map, unfiltered_map, width, height, 10)

   local quantizer = self._biome:get_mountains_quantizer()

   -- quantize the height map
   for i=1, size do
      local surface_elevation = surface_micro_map[i]
      local rock_elevation

      if surface_elevation > mountains_base_height then
         -- if the mountain breaks the surface just use its height
         rock_elevation = surface_elevation
      else
         -- quantize the filtered value
         rock_elevation = quantizer:quantize(underground_micro_map[i])

         -- make sure the sides of the rock faces stay beneath the surface
         -- e.g. we don't want a drop in an adjacent foothills block to expose the rock
         if rock_elevation > surface_elevation - mountains_step_size then
            rock_elevation = rock_elevation - mountains_step_size
         end

         -- make sure we have a layer of rock beneath everything
         if rock_elevation <= 0 then
            rock_elevation = rock_line
         end
      end

      underground_micro_map[i] = rock_elevation
   end

   local underground_elevation_map = self:_convert_to_elevation_map(underground_micro_map)

   return underground_micro_map, underground_elevation_map
end

function MicroMapGenerator:generate_noise_map(size_x, size_y)
   local mountains_info = self._terrain_info.mountains
   local macro_blocks_per_tile = self._macro_blocks_per_tile
   -- +1 for half macro_block margin on each edge
   local width = size_x * macro_blocks_per_tile + 1
   local height = size_y * macro_blocks_per_tile + 1
   local noise_map = Array2D(width, height)

   local fn = function (x,y)
      local noise_config = self._terrain_info.noise_map_settings
      local mean = mountains_info.height_base
      local range = (mountains_info.height_max - mean)*2
      local height = SimplexNoise.proportional_simplex_noise(noise_config.octaves,noise_config.persistence_ratio,noise_config.bandlimit, mean,range, noise_config.aspect_ratio, self._seed,x,y)
      return height
   end
   noise_map:fill(fn)
   return noise_map
end

--pull up the mountains with a smooth exponential map,
-- so the maximum height from generated noise tries to match the intended maximum height
function MicroMapGenerator:_accentuate_mountains(micro_map)
   local mountains_info = self._terrain_info.mountains
   local mountains_base = mountains_info.height_base
   local raw_mountains_range = self:get_percentile_altitude(99, micro_map) - mountains_base
   local mountains_range = (mountains_info.height_max - 0.5*mountains_info.step_size - mountains_base)
   --pull down the accentuation to eliminate overly steep mountains
   raw_mountains_range = 0.25*raw_mountains_range+0.75*mountains_range
   local exponential = math.log(mountains_range) / math.log(raw_mountains_range)
   local accentuation_fn = function(height)
      local result = height
      if height > mountains_base then
         result = mountains_base  + ((height - mountains_base))^exponential
      end
      return result
   end
   micro_map:process(accentuation_fn)
end

--given n-th perccentile, gives the n-th percentile value within micro map
function MicroMapGenerator:get_percentile_altitude(percentile, micro_map)
   local num_samples = micro_map.height * micro_map.width
   local sorted = micro_map:copy_to_1D_array()
   table.sort(sorted)
   local percentile_index = radiant.math.round(0.01*percentile*num_samples)
   if percentile_index < 1 then percentile_index = 1 end
   local percentile_altitude = sorted[percentile_index]
   return percentile_altitude
end

--offsets the height map so that the user specified amount of plains/foothills is reached
function MicroMapGenerator:_match_plains_percentile(micro_map)
   local percentile = self._terrain_info.plains_percentage
   local max = self._terrain_info.foothills.height_max
   if percentile == nil then return end
   local altitude_difference = self:get_percentile_altitude(percentile, micro_map) - max
   log:info('altitude skew(%.2f)', altitude_difference)
   micro_map:process(
      function (value)
         return value - altitude_difference
      end
   )
end

function MicroMapGenerator:_convert_to_elevation_map(micro_map)
   -- subtract the half macro_block margin from each edge
   local elevation_map_width = (micro_map.width-1)*2
   local elevation_map_height = (micro_map.height-1)*2
   local elevation_map = Array2D(elevation_map_width, elevation_map_height)
   local a, b, elevation

   for j=1, elevation_map.height do
      for i=1, elevation_map.width do
         a, b = _elevation_map_to_micro_map_coords(i, j)
         elevation = micro_map:get(a, b)
         elevation_map:set(i, j, elevation)
      end
   end

   return elevation_map
end

function MicroMapGenerator:_quantize(micro_map)
   local quantizer = self._biome:get_quantizer()
   local plains_max_height = self._terrain_info.plains.height_max

   micro_map:process(
      function (value)
         -- don't quantize below plains_max_height
         -- we have special detailing code for that
         -- could also use a different quantizer for this
         -- currently the same quantizer as the TerrainGenerator
         if value <= plains_max_height then
            return plains_max_height
         end
         return quantizer:quantize(value)
      end
   )
end

-- edge values may not change values! they are shared with the adjacent tile
function MicroMapGenerator:_postprocess(micro_map)
   self:_remove_juts(micro_map)
   self:_fill_holes(micro_map)
   self:_grow_peaks(micro_map)
   -- do this last, otherwise remove pointies will change the shapes
   self:_add_plains_valleys(micro_map)
end
-- makes the terrain more rectangular and zelda-like
-- removes macro blocks that jut into or out of the landscape contours
-- jut is actually a noun that means this
function MicroMapGenerator:_remove_juts(micro_map)
   -- don't iterate along map boundaries
   -- perform 1 pass over the map
   -- could perform recursive passes on neighbors when a jut is fixed
   for j=2, micro_map.height-1 do
      for i=2, micro_map.width-1 do
         self:_remove_jut(micro_map, i, j)
      end
   end
end

function MicroMapGenerator:_remove_jut(micro_map, x, y)
   if micro_map:is_boundary(x, y) then
      return
   end

   local offset = micro_map:get_offset(x, y)
   local value = micro_map[offset]
   local high_count = 0
   local low_count = 0
   local neighbors = {}
   local neighbor, new_value

   micro_map:each_neighbor(x, y, false, function(neighbor)
         table.insert(neighbors, neighbor)

         if value < neighbor then
            -- number of neighboring voxels that are higher
            high_count = high_count + 1
         elseif value > neighbor then
            -- number of neighboring voxels that are lower
            low_count = low_count + 1
         end
      end)

   local index = nil

   if high_count == 3 then
      -- (x, y) is surrounded by 3 higher voxels
      -- select the lowest of the three as the new elevation
      index = 2
   elseif low_count == 3 then
      -- (x, y) is surrounded by 3 lower voxels
      -- select the highest of the three as the new elevation
      index = 3
   end

   if not index then
      return
   end

   -- found a jut, set it (up or down) to the closet neighboring elevation
   table.sort(neighbors)
   new_value = neighbors[index]
   micro_map[offset] = new_value
end

function MicroMapGenerator:_fill_holes(micro_map)
   -- don't iterate along map boundaries
   for j=2, micro_map.height-1 do
      for i=2, micro_map.width-1 do
         -- can fill in place
         self:_fill_hole(micro_map, i, j)
      end
   end
end

function MicroMapGenerator:_fill_hole(micro_map, x, y)
   if micro_map:is_boundary(x, y) then
      return
   end

   local width = micro_map.width
   local offset = micro_map:get_offset(x, y)
   local value = micro_map[offset]
   local left, right, top, bottom, new_value

   left = micro_map[offset-1]
   if value >= left then
      return
   end

   right = micro_map[offset+1]
   if value >= right then
      return
   end

   top = micro_map[offset-width]
   if value >= top then
      return
   end

   bottom = micro_map[offset+width]
   if value >= bottom then
      return
   end

   -- found a hole, raise it to lowest neighbor
   new_value = math.min(left, right, top, bottom)
   micro_map[offset] = new_value
end

function MicroMapGenerator:_grow_peaks(micro_map)
   -- don't iterate along map boundaries
   for j=2, micro_map.height-1 do
      for i=2, micro_map.width-1 do
         self:_grow_peak(micro_map, i, j)
      end
   end
end

function MicroMapGenerator:_grow_peak(micro_map, x, y)
   if micro_map:is_boundary(x, y) then
      return
   end

   local width = micro_map.width
   local offset = micro_map:get_offset(x, y)
   local value = micro_map[offset]
   local left, right, top, bottom

   left = micro_map[offset-1]
   if value <= left then
      return
   end

   right = micro_map[offset+1]
   if value <= right then
      return
   end

   top = micro_map[offset-width]
   if value <= top then
      return
   end

   bottom = micro_map[offset+width]
   if value <= bottom then
      return
   end

   -- found a peak, try to fill out a 2x2 block using the peak at all 4 corners
   local base_height = math.max(left, right, top, bottom)
   local peak_height = value
   local x0, y0
   local length = 4

   -- o o o o o  Legend:
   -- o x x x o     * = peak
   -- o x * x o     x = potential peak
   -- o x x x o     o = base elevation
   -- o o o o o
   for j=-2, -1 do
      for i=-2, -1 do
         x0 = x + i
         y0 = y + j

         if micro_map:block_in_bounds(x0, y0, length, length) then
            -- only fill if the ring around the 2x2 block is level land (or part of the peak)
            if self:_not_lower_than_elevation(base_height, micro_map, x0, y0, length, length) then
               micro_map:set_block(x0+1, y0+1, 2, 2, peak_height)
            end
         end
      end
   end
end

function MicroMapGenerator:_not_lower_than_elevation(elevation, micro_map, x, y, width, height)
   local result = true

   micro_map:visit_block(x, y, width, height, function(value)
         if value < elevation then
            result = false
            -- return true to terminate iteration
            return true
         end
      end)

   return result
end

function MicroMapGenerator:_add_plains_valleys(micro_map)
   local rng = self._rng
   local shape_width = self._valley_shapes.shape_width
   local shape_height = self._valley_shapes.shape_height
   local plains_info = self._terrain_info.plains
   local noise_map = Array2D(micro_map.width, micro_map.height)
   local filtered_map = Array2D(micro_map.width, micro_map.height)
   local plains_max_height = plains_info.height_max
   -- no coincidence that this value is approx 1/((shape_width+4)*(shape_height+4))
   local valley_density = 0.015
   local num_sites = 0
   local sites = {}
   local value, site, roll

   local noise_fn = function(i, j)
      if micro_map:is_boundary(i, j) then
         return -10
      end
      if micro_map:get(i, j) ~= plains_max_height then
         return -100
      end
      return 1
   end

   noise_map:fill(noise_fn)

   FilterFns.filter_2D_0125(filtered_map, noise_map, noise_map.width, noise_map.height, 10)

   for j=1, filtered_map.height do
      for i=1, filtered_map.width do
         value = filtered_map:get(i, j)

         if value > 0 then
            num_sites = num_sites + 1
            -- -1 to move from center to origin (top left of block)
            site = { x = i-1, y = j-1 }
            table.insert(sites, site)
         end
      end
   end

   for i=1, num_sites*valley_density do
      roll = rng:get_int(1, num_sites)
      site = sites[roll]

      -- make sure pattern has good separation from other terrain
      if self:_is_high_plains(micro_map, site.x-2, site.y-2, shape_width+4, shape_height+4) then
         self:_place_valley(micro_map, site.x, site.y)
      end
   end
end

function MicroMapGenerator:_place_valley(micro_map, i, j)
   local terrain_info = self._terrain_info
   local plateau_height = terrain_info.plains.height_max
   local valley_height = terrain_info.plains.height_valley
   local block

   block = self._valley_shapes:get_random_shape(valley_height, plateau_height)

   Array2D.copy_block(micro_map, block, i, j, 1, 1, block.width, block.height)
end

function MicroMapGenerator:_is_high_plains(micro_map, i, j, width, height)
   local plains_height = self._terrain_info.plains.height_max
   local result = true

   micro_map:visit_block(i, j, width, height, function(value)
         if value ~= plains_height then
            result = false
            -- return true to terminate iteration
            return true
         end
      end)

   return result
end

----- stashed code below

function MicroMapGenerator:_consolidate_mountain_blocks(micro_map)
   local max_foothills_height = self._terrain_info.foothills.height_max
   local start_x = 1
   local start_y = 1
   local i, j, value

   for j=start_y, micro_map.height, 2 do
      for i=start_x, micro_map.width, 2 do
         value = self:_average_quad(micro_map, i, j)
         if value > max_foothills_height then
            self:_set_quad(micro_map, i, j, value)
         end
      end
   end
end

function MicroMapGenerator:_average_quad(micro_map, x, y)
   local offset = micro_map:get_offset(x, y)
   local width = micro_map.width
   local height = micro_map.height
   local sum, num_blocks

   sum = micro_map[offset]
   num_blocks = 1

   if x < width then
      sum = sum + micro_map[offset+1]
      num_blocks = num_blocks + 1
   end

   if y < height then
      sum = sum + micro_map[offset+width]
      num_blocks = num_blocks + 1
   end

   if x < width and y < height then
      sum = sum + micro_map[offset+width+1]
      num_blocks = num_blocks + 1
   end

   return sum / num_blocks
end

function MicroMapGenerator:_set_quad(micro_map, x, y, value)
   local offset = micro_map:get_offset(x, y)
   local width = micro_map.width
   local height = micro_map.height

   micro_map[offset] = value

   if x < width then
      micro_map[offset+1] = value
   end

   if y < height then
      micro_map[offset+width] = value
   end

   if x < width and y < height then
      micro_map[offset+width+1] = value
   end
end

--originally used as an alternative to the match_plains_percentile+accentuate_mountains processing combination
function MicroMapGenerator:_skew_raw_noise_to_height(noise_map)
   local num_samples = noise_map.height * noise_map.width
   local sorted = noise_map:clone_to_ascending_1D_array()
   local plains_index = radiant.math.round(0.01*self._terrain_info.percentage.plains*num_samples)
   --clamp index to valid values
   if plains_index < 2 then plains_index = 2 end
   if plains_index > num_samples then plains_index = num_samples end
   local plains_raw_max = sorted[plains_index]
   local mountains_raw_max = sorted[num_samples]
   local foothills_mountains_raw_width = mountains_raw_max - plains_raw_max
   local plains_max = self._terrain_info.plains.height_max
   local foothills_mountains_width = self._terrain_info.mountains.height_max - plains_max - 0.6*self._terrain_info.mountains.step_size
   local skew_range = foothills_mountains_width / foothills_mountains_raw_width
   local skew_mean = plains_max - skew_range*plains_raw_max
   return skew_mean, skew_range
end

--originally used in generate micro map to create a blockier look across tiles
function MicroMapGenerator:_blockify(micro_map, size_x, size_y)
   --can't do anything about extra 0.5 block offset, but doesn't matter that much (offscreen anyways)
   local tile_size = self._macro_blocks_per_tile
   local half_width_offset = 0.5*(tile_size - 1)
   for y = 1, size_y do
      for x = 1, size_x do
         local offset_x = (x-1)*tile_size
         local offset_y = (y-1)*tile_size
         local center_x = offset_x + radiant.math.round(half_width_offset)
         local center_y = offset_y + radiant.math.round(half_width_offset)
         local center_altitude = micro_map:get(center_x, center_y)
         for j = 1, tile_size do
            for i = 1, tile_size do
               local blockify_x = offset_x + i
               local blockify_y = offset_y + j
               local bias_x = 1- (math.abs(i-1 - half_width_offset) / half_width_offset)
               local bias_y = 1- (math.abs(j-1 - half_width_offset) / half_width_offset)
               local bias = (bias_x < bias_y and bias_x or bias_y)
               if bias < 0 then bias = 0 end
               log:info('point(%d,%d), bias(%.2f)',i,j,bias)

               local new_altitude = (1-bias)*micro_map:get(blockify_x,blockify_y) + bias*center_altitude
               micro_map:set(blockify_x,blockify_y,new_altitude)
            end
         end
      end
   end
end

--originally used in postprocess to remove qbert like corners
function MicroMapGenerator:_remove_diagonals(micro_map, degree)
   --should repeat process multiple times since for really steep slopes it just pushes the problem up one step size
   for times= 1, degree do
   -- don't iterate along map boundaries
      for j=2, micro_map.height-1 do
         for i=2, micro_map.width-1 do
            self:_remove_diagonal(micro_map, i, j)
         end
      end
   end
end

-- makes the terrain more rectangular and zelda-like
--removes qbert like corners
--should be called after the other postprocess steps
function MicroMapGenerator:_remove_diagonal(micro_map,x,y)
   local x_max = 1
   local y_max = 1
   local height_max = micro_map:get(x,y)
   local height_mid = height_max
   local height_min = height_max
   local height_type = 1

   for j=1,2 do
      local y_neighbor = y + j-1
      for i = 1,2 do
         local x_neighbor = x + i-1
         local height_neighbor = micro_map:get(x_neighbor, y_neighbor)
         if height_neighbor > height_max then
            height_type = height_type + 1
            height_mid = height_max
            height_max = height_neighbor
            x_max = x_neighbor
            y_max = y_neighbor
         elseif height_neighbor < height_min then
            height_type = height_type + 1
            height_mid = height_min
             height_min = height_neighbor
         end
         if height_type == 3 then
            micro_map:set(x_max,y_max, height_mid)
            return
         end
      end
   end
end

return MicroMapGenerator
