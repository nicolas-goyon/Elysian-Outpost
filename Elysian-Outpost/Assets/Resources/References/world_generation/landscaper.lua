local Biome = require 'services.server.world_generation.biome'
local NonUniformQuantizer = require 'lib.math.non_uniform_quantizer'
local WeightedSet = require 'lib.algorithms.weighted_set'
local Array2D = require 'services.server.world_generation.array_2D'
local SimplexNoise = require 'lib.math.simplex_noise'
local FilterFns = require 'services.server.world_generation.filter.filter_fns'
local PerturbationGrid = require 'services.server.world_generation.perturbation_grid'
local Timer = require 'services.server.world_generation.timer'
local Point3 = _radiant.csg.Point3
local Cube3 = _radiant.csg.Cube3
local Region3 = _radiant.csg.Region3
local log = radiant.log.create_logger('world_generation')

local tree_tag = ':trees:'
local generic_vegetation_name = "vegetation"

local water_shallow = 'water_1'
local water_deep = 'water_2'
local Landscaper = class()

-- TODO: refactor this class into smaller pieces
function Landscaper:__init(biome, rng, seed)
   self._biome = biome
   self._tile_width = self._biome:get_tile_size()
   self._tile_height = self._biome:get_tile_size()
   self._feature_size = self._biome:get_feature_block_size()
   self._landscape_info = self._biome:get_landscape_info()
   self._rng = rng
   self._seed = seed

   self._noise_map_buffer = nil
   self._density_map_buffer = nil

   self._perturbation_grid = PerturbationGrid(self._tile_width, self._tile_height, self._feature_size, self._rng)

   self._water_table = {
      water_1 = self._landscape_info.water.depth.shallow,
      water_2 = self._landscape_info.water.depth.deep
   }

   self:_parse_landscape_info()
end

function Landscaper:_parse_landscape_info()
   local landscape_info = self._landscape_info

   self._placement_table = self._landscape_info.placement_table

   self._tree_size_data = self:_parse_tree_sizes(landscape_info.trees.sizes)

   --each of the following uses get_variant(elevation) to get specific parameters for each terrain step
   --.types variants are weighted sets, .noise_map_params are just a set of parameters for respective noise functions

   local boulder_config = landscape_info.scattered.boulders
   self._noise_map_params = self:_parse_simplex_noise(boulder_config)

   local plant_config = landscape_info.scattered.plants
   local plant_data = {}
   plant_data.types = self:_parse_weights(plant_config)
   plant_data.noise_map_parameters = self:_parse_simplex_noise(plant_config)
   self._plant_data = plant_data

   local tree_config = landscape_info.trees
   local tree_data = {}
   tree_data.types = self:_parse_weights(tree_config)
   tree_data.noise_map_parameters = self:_parse_gaussian_noise(tree_config)
   self._tree_data = tree_data
end

--json parsing functions

function Landscaper:_parse_tree_sizes(tree_variants)
   local tree_size_data = {}
   for tree_type, sizes in pairs(tree_variants) do

      local size_data = {}
      local size_map = {}
      local thresholds = {}
      table.insert(thresholds,0)

      size_data.ancient_percentage = 0

      for tree_size, threshold in pairs(sizes) do
         local tree_name = get_tree_name(tree_type, tree_size)

         if tree_size == 'ancient' then
            size_data.ancient_percentage = threshold.percentage
         else
            table.insert(thresholds, threshold)
            size_map[threshold] = tree_size
         end
      end
      size_data.size_map = size_map
      size_data.quantizer = NonUniformQuantizer(thresholds)
      tree_size_data[tree_type] = size_data
   end
   return tree_size_data
end

function Landscaper:_parse_weights(config)
   local type_distributions = config.weights
   local type_parse_fn = function(type_distribution)
      local types = WeightedSet(self._rng)
      for feature_type, probability in pairs(type_distribution) do
         types:add(feature_type, probability)
      end
      return types
   end

   local variants = self:_parse_terrain_step_based_variants(type_distributions, type_parse_fn)
   return variants
end

function Landscaper:_parse_gaussian_noise(config)
   local noise_config = config.noise_map_parameters.terrain_based
   local noise_parse_fn = function(noise_params)
      local params = {}
      params.mean = noise_params.mean
      params.std_dev = noise_params.std_dev
      params.density = noise_params.density
      return params
   end

   local variants = self:_parse_terrain_step_based_variants(noise_config, noise_parse_fn)
   return variants
end

function Landscaper:_parse_simplex_noise(config)
   local noise_config = config.noise_map_parameters.terrain_based
   local noise_parse_fn = function(noise_params)
      local params = {}
      params.probability = noise_params.probability
      params.density = noise_params.density
      return params
   end

   local variants = self:_parse_terrain_step_based_variants(noise_config, noise_parse_fn)
   return variants
end

function Landscaper:_parse_terrain_step_based_variants(config, parse_fn)
   local terrain_info = self._biome:get_terrain_info()
   local variants_set = {}

   for terrain_type, raw_variants in pairs(config) do
      local terrain_type_variants = {}
      local step_map = {}
      local step_count = terrain_info[terrain_type].step_count
      local step_variants = {}

      local raw_step_variant = {}
      local parsed_step_variant = {}

      local current_step = 1
      raw_step_variant = raw_variants[tostring(current_step)]
      assert(raw_step_variant ~= nil)

      for step=1,step_count do
         raw_step_variant = raw_variants[tostring(step)]
         if raw_step_variant ~= nil then
            current_step = step
            parsed_step_variant = parse_fn(raw_step_variant)
            step_variants[current_step] = parsed_step_variant
         end
         step_map[step] = current_step
      end
      terrain_type_variants.step_map = step_map
      terrain_type_variants.step_variants = step_variants
      variants_set[terrain_type] = terrain_type_variants
   end
   return variants_set
end

--water landscaping
function Landscaper:is_water_feature(feature_name)
   local result = self._water_table[feature_name] ~= nil
   return result
end

function Landscaper:mark_water_bodies(elevation_map, feature_map)
   local rng = self._rng
   local biome = self._biome
   local config = self._landscape_info.water.noise_map_settings
   local modifier_map, density_map = self:_get_filter_buffers(feature_map.width, feature_map.height)
   --fill modifier map to push water bodies away from terrain type boundaries
   local modifier_fn = function (i,j)
      if self:_is_flat(elevation_map, i, j, 1) then
         return 0
      else
         return -1*config.range
      end
   end
   --use density map as buffer for smoothing filter
   density_map:fill(modifier_fn)
   FilterFns.filter_2D_0125(modifier_map, density_map, modifier_map.width, modifier_map.height, 10)
   --mark water bodies on feature map using density map and simplex noise
   local old_feature_map = Array2D(feature_map.width, feature_map.height)
   for j=1, feature_map.height do
      for i=1, feature_map.width do
         local occupied = feature_map:get(i, j) ~= nil
         if not occupied then
            local elevation = elevation_map:get(i, j)
            local terrain_type = biome:get_terrain_type(elevation)
            local value = SimplexNoise.proportional_simplex_noise(config.octaves,config.persistence_ratio, config.bandlimit,config.mean[terrain_type],config.range,config.aspect_ratio, self._seed,i,j)
            value = value + modifier_map:get(i,j)
            if value > 0 then
               local old_value = feature_map:get(i, j)
               old_feature_map:set(i, j, old_value)
               feature_map:set(i, j, water_shallow)
            end
         end
      end
   end
   self:_remove_juts(feature_map)
   self:_remove_ponds(feature_map, old_feature_map)
   self:_fix_tile_aligned_water_boundaries(feature_map, old_feature_map)
   self:_add_deep_water(feature_map)
end

function Landscaper:_remove_juts(feature_map)
   -- just 1 pass currently
   -- could record fixups and recursively recheck the 8 adjacents
   for j=2, feature_map.height-1 do
      for i=2, feature_map.width-1 do
         if self:_is_peninsula(feature_map, i, j) then
            feature_map:set(i, j, water_shallow)
         end
      end
   end
end

function Landscaper:_remove_ponds(feature_map, old_feature_map)
   for j=2, feature_map.height-1 do
      for i=2, feature_map.width-1 do
         local feature_name = feature_map:get(i, j)

         if self:is_water_feature(feature_name) then
            local has_water_neighbor = false

            feature_map:each_neighbor(i, j, false, function(value)
                  if self:is_water_feature(value) then
                     has_water_neighbor = true
                     return true -- stop iteration
                  end
               end)

            if not has_water_neighbor then
               local old_value = old_feature_map:get(i, j)
               feature_map:set(i, j, old_value)
            end
         end
      end
   end
end

function Landscaper:_fix_tile_aligned_water_boundaries(feature_map, old_feature_map)
   local map_width, map_height = feature_map:get_dimensions()
   local features_per_tile_width = self._tile_width / self._feature_size
   local features_per_tile_height = self._tile_height / self._feature_size
   local i, j

   -- scan horizontal tile boundaries
   j = features_per_tile_height
   while j < map_height do
      for i = 1, map_width do
         -- synchronize vertically across the scan line
         self:_sync_water_pair(i, j, i, j+1, feature_map, old_feature_map)
      end
      -- advance to next horizontal scan line
      j = j + features_per_tile_height
   end

   -- scan vertical tile boundaries
   i = features_per_tile_width
   while i < map_width do
      for j = 1, map_height do
         -- synchronize horizontally across the scan line
         self:_sync_water_pair(i, j, i+1, j, feature_map, old_feature_map)
      end
      -- advance to next vertical scan line
      i = i + features_per_tile_width
   end
end

function Landscaper:_sync_water_pair(i, j, m, n, feature_map, old_feature_map)
   local feature_1 = feature_map:get(i, j)
   local feature_2 = feature_map:get(m, n)

   -- fast exit for common case
   if feature_1 == feature_2 then
      return
   end

   local is_water_1 = self:is_water_feature(feature_1)
   local is_water_2 = self:is_water_feature(feature_2)

   if is_water_1 and not is_water_2 then
      local old_feature = old_feature_map:get(i, j)
      feature_map:set(i, j, old_feature)
   elseif is_water_2 and not is_water_1 then
      local old_feature = old_feature_map:get(m, n)
      feature_map:set(m, n, old_feature)
   end
end

function Landscaper:_add_deep_water(feature_map)
   for j=2, feature_map.height-1 do
      for i=2, feature_map.width-1 do
         local feature_name = feature_map:get(i, j)

         if self:is_water_feature(feature_name) then
            local surrounded_by_water = true

            feature_map:each_neighbor(i, j, true, function(value)
                  if not self:is_water_feature(value) then
                     surrounded_by_water = false
                     return true -- stop iteration
                  end
               end)

            if surrounded_by_water then
               feature_map:set(i, j, water_deep)
            end
         end
      end
   end
end

function Landscaper:place_water_bodies(tile_region, tile_map, feature_map, tile_offset_x, tile_offset_y)
   local water_region = Region3()

   feature_map:visit(function(value, i, j)
         if not self:is_water_feature(value) then
            return
         end

         local depth = self._water_table[value]
         local x, y, w, h = self._perturbation_grid:get_cell_bounds(i, j)

         -- use the center of the cell to get the elevation because the edges may have been detailed
         local cx, cy = x + math.floor(w*0.5), y + math.floor(h*0.5)
         local lake_top = tile_map:get(cx, cy)
         local lake_bottom = lake_top - depth

         local world_x, world_z = self:_to_world_coordinates(x, y, tile_offset_x, tile_offset_y)
         local cube = Cube3(
               Point3(world_x, lake_bottom, world_z),
               Point3(world_x + w, lake_top, world_z + h)
            )

         tile_region:subtract_cube(cube)

         water_region:add_cube(cube)
      end)

   water_region:optimize('place water bodies')

   return water_region
end

function Landscaper:is_forest_feature(feature_name)
   if feature_name == nil then
      return false
   end

   if self:is_tree_name(feature_name) then
      return true
   end

   if feature_name == generic_vegetation_name then
      return true
   end

   return false
end

function Landscaper:is_water_feature(feature_name)
   if feature_name == nil then
      return false
   end

   local result = feature_name == water_shallow or feature_name == water_deep
   return result
end

function Landscaper:mark_trees(elevation_map, feature_map)
   local rng = self._rng
   local biome = self._biome

   local noise_map, density_map = self:_get_filter_buffers(feature_map.width, feature_map.height)
   local tree_name, tree_type, tree_size, occupied, value, elevation, tree_types, tree_density, noise_variant
   --generate initial gaussian noise for density map
   local noise_fn = function(i, j)
      local noise_mean_offset = self._landscape_info.trees.noise_map_parameters.mean_offset
      elevation = elevation_map:get(i, j)
      noise_variant = self:_get_variant(self._tree_data.noise_map_parameters, elevation)
      local mean = noise_variant.mean
      local std_dev = noise_variant.std_dev

      local feature = feature_map:get(i,j)
      if self:is_water_feature(feature) then
         mean = mean + noise_mean_offset.water
      end
      if not self:_is_flat(elevation_map, i, j, 1) then
         mean = mean + noise_mean_offset.boundary
      end
      --note this is not the terrain boundary in the json file, but the edge of the noise map/feature map.
      if noise_map:is_boundary(i, j) then
         mean = mean - 20
      end
      return rng:get_gaussian(mean, std_dev)
   end
   --fill map and smooth out boundaries
   noise_map:fill(noise_fn)
   FilterFns.filter_2D_0125(density_map, noise_map, noise_map.width, noise_map.height, 10)

   --determine trees based on density function and terrain
   for j=1, density_map.height do
      for i=1, density_map.width do
         occupied = feature_map:get(i, j) ~= nil

         if not occupied then
            value = density_map:get(i, j)
            if value > 0 then
               elevation = elevation_map:get(i, j)
               tree_types = self:_get_variant(self._tree_data.types, elevation)
               noise_variant = self:_get_variant(self._tree_data.noise_map_parameters, elevation)

               tree_type = tree_types:choose_random()
               tree_size = self:_get_tree_size(tree_type, value)
               tree_name = get_tree_name(tree_type, tree_size)
               tree_density = noise_variant.density
               --determine density cutoff
               --set trees based on density cutoff
               if rng:get_real(0, 1) < tree_density then
                  feature_map:set(i, j, tree_name)
               else
                  -- the higher tree densities are used to thin the trees so that they are not visually too dense
                  -- we plug the holes from this 'thinning' so that tree-loving flora don't always fill the holes
                  if tree_density > 0.5 then
                     feature_map:set(i, j, generic_vegetation_name)
                  end
               end
            end
         end
      end
   end
end

function Landscaper:_get_tree_size(tree_type, value)
   local size_data = self._tree_size_data[tree_type]
   local quantized = size_data.quantizer:quantize_down(value)
   local size = size_data.size_map[quantized]

   if size == 'large' then
      local rng = self._rng
      if rng:get_real(0, 100) < size_data.ancient_percentage then
         return 'ancient'
      end
   end
   return size
end

function get_tree_name(tree_type, tree_size)
   return tree_type .. ':' .. tree_size
end

function Landscaper:is_tree_name(feature_name)
   if feature_name == nil then return false end
   -- may need to be more robust later
   local index = feature_name:find(tree_tag)
   return index ~= nil
end

--berries landscaping. Haven't changed that much from previous version,
--doesn't seem necessary as of now.
function Landscaper:mark_berry_bushes(elevation_map, feature_map)
   local rng = self._rng
   local biome = self._biome
   local perturbation_grid = self._perturbation_grid
   local noise_map, density_map = self:_get_filter_buffers(feature_map.width, feature_map.height)
   local value, occupied, elevation
   local config = self._landscape_info.berries.placement
   local berry_bush_uri = config.uri

   local noise_fn = function(i, j)
      local mean = config.mean
      local std_dev = config.std_dev

      local feature = feature_map:get(i, j)
      if self:is_tree_name(feature) then
         mean = mean + config.mean_offset.tree
      end
      if self:is_water_feature(feature) then
         mean = mean + config.mean_offset.water
      end
      if not self:_is_flat(elevation_map, i, j, 1) then
         mean = mean + config.mean_offset.boundary
      end
      return rng:get_gaussian(mean, std_dev)
   end

   noise_map:fill(noise_fn)
   FilterFns.filter_2D_050(density_map, noise_map, noise_map.width, noise_map.height, 6)

   for j=1, density_map.height do
      for i=1, density_map.width do
         value = density_map:get(i, j)

         if value > 0 then
            occupied = feature_map:get(i, j) ~= nil

            if not occupied then
               elevation = elevation_map:get(i, j)
               if self:_valid_berry_placement(elevation) then
                  feature_map:set(i, j, berry_bush_uri)
               end
            end
         end
      end
   end
end

function Landscaper:_valid_berry_placement(elevation)
   local terrain_type, step = self._biome:get_terrain_type_and_step(elevation)
   if terrain_type == 'mountains' then
      return false
   end
   if terrain_type == 'plains' and step == 1 then
      return false
   end
   return true
end

--plant landscaping

function Landscaper:mark_plants(elevation_map, feature_map)
   local rng = self._rng
   local biome = self._biome
   local config = self._landscape_info.scattered.plants
   local occupied, elevation, noise_variant, value, plant_types, plant_name, plant_type
   --density_map = noise_map
   --determine trees based on density function and terrain
   for j=1, feature_map.height do
      for i=1, feature_map.width do
         occupied = feature_map:get(i, j) ~= nil
         if not occupied then
            elevation = elevation_map:get(i, j)
            noise_variant = self:_get_variant(self._plant_data.noise_map_parameters, elevation)
            value = self:_scattered_noise_function(i,j,noise_variant.probability)
            if value > 0 and rng:get_real(0, 1) < noise_variant.density then
               plant_types = self:_get_variant(self._plant_data.types, elevation)
               plant_name = plant_types:choose_random()
               feature_map:set(i, j, plant_name)
            end
         end
      end
   end
end

--boulder placement
function Landscaper:mark_boulders(elevation_map, feature_map)
   local rng = self._rng
   local boulder_namespace = self._landscape_info.scattered.boulders.namespace
   local occupied, elevation, noise_variant, value, boulder_uri

   --our raw simplex noise goes from -0.5 to 0.5
   for j=1, feature_map.height do
      for i=1, feature_map.width do
         occupied = feature_map:get(i, j) ~= nil
         if not occupied then
            elevation = elevation_map:get(i, j)
            noise_variant = self:_get_variant(self._noise_map_params, elevation)
            value = self:_scattered_noise_function(i,j,noise_variant.probability)
            if value > 0 and rng:get_real(0, 1) < noise_variant.density then
               local boulder_postfix = self:_get_boulder_postfix(noise_variant.probability, value)
               boulder_uri = string.format('%s:%s', boulder_namespace, boulder_postfix)
               feature_map:set(i, j, boulder_uri)
            end
         end
      end
   end
end

function Landscaper:_scattered_noise_function(i,j,probability)
   local x,y = self._perturbation_grid:get_perturbed_coordinates(i, j, 1)
   local value = (probability - 0.5) + SimplexNoise.raw_proportional_simplex_noise(3,0.2,4,1, self._seed,x,y)
   return value
end

--currently hardcoded in a rather bad way
function Landscaper:_get_boulder_postfix(max, value)
   local cutoffs = self._landscape_info.scattered.boulders.cutoffs
   if value < max*cutoffs.small then return 'small' end
   if value < max*cutoffs.medium then
      local variant = self._rng:get_int(1,3)
      return 'medium_' .. variant
   end
   local variant =  self._rng:get_int(1,2)
   return 'large_' .. variant
end

--general functions

--gets variant based on terrain step
function Landscaper:_get_variant(variant_maps, elevation)
   local terrain_type, step = self._biome:get_terrain_type_and_step(elevation)
   local variant_map = variant_maps[terrain_type]
   local dependent_step = variant_map.step_map[step]
   local variant = variant_map.step_variants[dependent_step]
   return variant
end

--get feature type from weighted set, used by trees and plants
function Landscaper:_get_feature_type(type_variant, step)
   local feature_type = type_variant:choose_random()
   return feature_type
end

function Landscaper:_get_placement_info(feature_name, terrain_type)
   local entry = self._placement_table[feature_name]
   if not entry then
      return nil, nil
   end

   -- parse optional terrain_type (e.g. small trees have different placement functions in mountains)
   if entry[terrain_type] then
      entry = entry[terrain_type]
   end

   local placement_type = entry.placement_type
   local parameters = entry.parameters
   -- TODO: perform validation here

   return placement_type, parameters
end

--used by plants and boulders
function Landscaper:_place_feature(feature_name, i, j, tile_map, place_item)
   local perturbation_grid = self._perturbation_grid
   local x, y = perturbation_grid:get_unperturbed_coordinates(i, j)
   local elevation = tile_map:get(x, y)
   local terrain_type = self._biome:get_terrain_type(elevation)
   local placement_type, params = self:_get_placement_info(feature_name, terrain_type)

   if not placement_type then
      if not feature_name then
         log:spam('no feature to place exists at %d, %d', i, j)
      else
         log:spam('%s not in feature table', feature_name)
      end
      return
   end

   if placement_type == 'single' then
      local x, y = perturbation_grid:get_perturbed_coordinates(i, j, params.exclusion_radius)
      if self:_is_flat(tile_map, x, y, params.ground_radius) then
         place_item(feature_name, x, y)
      end
      return
   end

   local try_place_item = function(x, y)
      place_item(feature_name, x, y)
      return true
   end

   if placement_type == 'dense' then
      local x, y, w, h = perturbation_grid:get_cell_bounds(i, j)
      local nested_grid_spacing = math.floor(perturbation_grid.grid_spacing / params.grid_multiple)

      self:_place_dense_items(tile_map, x, y, w, h, nested_grid_spacing,
         params.exclusion_radius, params.item_density, try_place_item)
      return
   end

   if placement_type == 'pattern' then
      local x, y, w, h = perturbation_grid:get_cell_bounds(i, j)
      local rows, columns = self:_random_pattern(params.min_rows, params.max_rows, params.min_cols, params.max_cols)
      self:_place_pattern(tile_map, x, y, w, h, rows, columns, params.item_spacing, params.item_density, try_place_item)
      return
   end

   assert(false, 'unknown placement_type ' .. placement_type .. ' for ' .. feature_name)
end

function Landscaper:_random_pattern(min_rows, max_rows, min_cols, max_cols)
   local x = self._rng:get_int(min_rows, max_rows)
   local y = self._rng:get_int(min_cols, max_cols)
   local orientation = self._rng:get_int(0, 1)
   if orientation == 0 then
      return x, y
   else
      return y, x
   end
end

function Landscaper:_get_filter_buffers(width, height)
   if self._noise_map_buffer == nil or
      self._noise_map_buffer.width ~= width or
      self._noise_map_buffer.height ~= height then

      self._noise_map_buffer = Array2D(width, height)
      self._density_map_buffer = Array2D(width, height)
   end

   assert(self._density_map_buffer.width == self._noise_map_buffer.width)
   assert(self._density_map_buffer.height == self._noise_map_buffer.height)

   return self._noise_map_buffer, self._density_map_buffer
end

function Landscaper:place_features(tile_map, feature_map, place_item)
   for j=1, feature_map.height do
      for i=1, feature_map.width do
         local feature_name = feature_map:get(i, j)
         self:_place_feature(feature_name, i, j, tile_map, place_item)
      end
   end
end

function Landscaper:place_flora(tile_map, feature_map, tile_offset_x, tile_offset_y)
   local place_item = function(uri, x, y)
      local entity = radiant.entities.create_entity(uri)
      self:_set_random_facing(entity)

      local elevation = tile_map:get(x, y)
      local world_x, world_z = self:_to_world_coordinates(x, y, tile_offset_x, tile_offset_y)
      local location = radiant.terrain.get_point_on_terrain(Point3(world_x, elevation, world_z))

      if radiant.terrain.is_standable(entity, location) then
         radiant.terrain.place_entity(entity, location, {force_iconic=false})
      end

      return entity
   end

   self:place_features(tile_map, feature_map, place_item)
end

function Landscaper:_place_pattern(tile_map, x, y, w, h, columns, rows, spacing, density, try_place_item)
   local rng = self._rng
   local i, j, result
   local x_offset = math.floor((w - spacing*(columns-1)) * 0.5)
   local y_offset = math.floor((h - spacing*(rows-1)) * 0.5)
   local x_start = x + x_offset
   local y_start = y + y_offset
   local placed = false

   for j=1, rows do
      for i=1, columns do
         if rng:get_real(0, 1) <= density then
            result = try_place_item(x_start + (i-1)*spacing, y_start + (j-1)*spacing)
            if result then
               placed = true
            end
         end
      end
   end

   return placed
end

function Landscaper:_place_dense_items(tile_map, cell_origin_x, cell_origin_y, cell_width, cell_height, grid_spacing, exclusion_radius, probability, try_place_item)
   local rng = self._rng
   -- consider removing this memory allocation
   local perturbation_grid = PerturbationGrid(cell_width, cell_height, grid_spacing, rng)
   local grid_width, grid_height = perturbation_grid:get_dimensions()
   local i, j, dx, dy, x, y, result
   local placed = false

   for j=1, grid_height do
      for i=1, grid_width do
         if rng:get_real(0, 1) < probability then
            if exclusion_radius >= 0 then
               dx, dy = perturbation_grid:get_perturbed_coordinates(i, j, exclusion_radius)
            else
               dx, dy = perturbation_grid:get_unperturbed_coordinates(i, j)
            end

            -- -1 becuase get_perturbed_coordinates returns base 1 coords and cell_origin is already at 1,1 of cell
            x = cell_origin_x + dx-1
            y = cell_origin_y + dy-1

            result = try_place_item(x, y)
            if result then
               placed = true
            end
         end
      end
   end

   return placed
end

-- checks if the rectangular region centered around x,y is flat
function Landscaper:_is_flat(tile_map, x, y, distance)
   if distance == 0 then return true end

   local start_x, start_y = tile_map:bound(x-distance, y-distance)
   local end_x, end_y = tile_map:bound(x+distance, y+distance)
   local block_width = end_x - start_x + 1
   local block_height = end_y - start_y + 1
   local height = tile_map:get(x, y)
   local is_flat = true

   tile_map:visit_block(start_x, start_y, block_width, block_height, function(value)
         if value ~= height then
            is_flat = false
            -- return true to terminate iteration
            return true
         end
      end)

   return is_flat
end

function Landscaper:_is_peninsula(feature_map, i, j)
   local feature_name = feature_map:get(i, j)
   if self:is_water_feature(feature_name) then
      return false
   end

   local water_count = 0

   feature_map:each_neighbor(i, j, false, function(value)
         if self:is_water_feature(value) then
            water_count = water_count + 1
         end
      end)

   local result = water_count == 3
   return result
end

function Landscaper:_set_random_facing(entity)
   entity:add_component('mob'):turn_to(90*self._rng:get_int(0, 3))
end

-- switch from lua height_map base 1 coordinates to c++ base 0 coordinates
-- swtich from tile coordinates to world coordinates
function Landscaper:_to_world_coordinates(x, y, tile_offset_x, tile_offset_y)
   local world_x = x-1+tile_offset_x
   local world_z = y-1+tile_offset_y
   return world_x, world_z
end

return Landscaper
