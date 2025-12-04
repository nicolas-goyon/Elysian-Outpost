local ScenarioSelector = require 'services.server.world_generation.scenario_selector'
local Histogram = require 'lib.algorithms.histogram'
local log = radiant.log.create_logger('surface_scenario_selector')

local SurfaceScenarioSelector = class()

function SurfaceScenarioSelector:__init(scenario_index, biome,rng)
   self._biome = biome
   self._rng = rng
   self._scenario_index = scenario_index
   self._category_placed_counts = {}
end

function SurfaceScenarioSelector:place_immediate_scenarios(habitat_map, elevation_map, tile_offset_x, tile_offset_y)
   local habitat_volumes = self:_calculate_habitat_volumes(habitat_map)
   local scenarios = self._scenario_index:select_scenarios('surface', 'immediate', habitat_volumes)
   self:_place_scenarios(scenarios, habitat_map, elevation_map, tile_offset_x, tile_offset_y, true)
end

function SurfaceScenarioSelector:place_revealed_scenarios(habitat_map, elevation_map, tile_offset_x, tile_offset_y)
   local habitat_volumes = self:_calculate_habitat_volumes(habitat_map)
   local scenarios = self._scenario_index:select_scenarios('surface', 'revealed', habitat_volumes)
   self:_place_scenarios(scenarios, habitat_map, elevation_map, tile_offset_x, tile_offset_y, false)
end

function SurfaceScenarioSelector:_calculate_habitat_volumes(habitat_map)
   local histogram = Histogram()

   habitat_map:visit(function(habitat_type)
         histogram:increment(habitat_type)
      end)

   local habitat_volumes = histogram:get_counts()
   return habitat_volumes
end

function SurfaceScenarioSelector:_place_scenarios(scenarios, habitat_map, elevation_map, tile_offset_x, tile_offset_y, activate_now)
   local rng = self._rng
   local feature_size = self._biome:get_feature_block_size()
   local feature_width, feature_length
   local feature_offset_x, feature_offset_y, intra_cell_offset_x, intra_cell_offset_y
   local site, sites, num_sites, roll, habitat_types, residual_x, residual_y
   local x, y, width, length
   local categories_prevented_in_tile = {}

   for _, properties in pairs(scenarios) do
      local category = self._scenario_index:get_category(properties.category)

      local placed_count = self._category_placed_counts[properties.category] or 0
      local global_count_satisfied = not category.max_count or placed_count < category.max_count

      local global_uniqueness_satisfied = self._scenario_index:contains(properties.category, properties.name)
      
      local local_uniqueness_satisfied = not categories_prevented_in_tile[properties.category]

      local biome_satisfied = not properties.biome_alias or properties.biome_alias == self._biome:get_biome_alias()
      
      if global_count_satisfied and global_uniqueness_satisfied and local_uniqueness_satisfied and biome_satisfied then
         habitat_types = properties.habitat_types

         -- dimensions of the scenario in voxels
         width = properties.size.width
         length = properties.size.length

         -- get dimensions of the scenario in feature cells
         feature_width, feature_length = self._biome:get_feature_dimensions(width, length)

         -- get a list of valid locations
         sites, num_sites = self:_find_valid_sites(habitat_map, elevation_map, habitat_types, feature_width, feature_length, properties.allow_nonflat, properties.overwrite_occupied_size)

         if num_sites > 0 then
            -- pick a random location
            roll = rng:get_int(1, num_sites)
            site = sites[roll]

            feature_offset_x = (site.i-1)*feature_size
            feature_offset_y = (site.j-1)*feature_size

            residual_x = feature_width*feature_size - width
            residual_y = feature_length*feature_size - length

            intra_cell_offset_x = rng:get_int(0, residual_x)
            intra_cell_offset_y = rng:get_int(0, residual_y)

            -- these are in C++ base 0 array coordinates
            x = tile_offset_x + feature_offset_x + intra_cell_offset_x
            y = tile_offset_y + feature_offset_y + intra_cell_offset_y
            
            stonehearth.static_scenario:add_scenario(properties, nil, x, y, width, length, activate_now)

            self:_mark_habitat_map(habitat_map, site.i, site.j, feature_width, feature_length)
            
            for _, prevented in ipairs(category.prevents_categories_in_same_tile or {}) do
               categories_prevented_in_tile[prevented] = true
            end
            self._category_placed_counts[properties.category] = placed_count + 1
            if properties.unique then
               self._scenario_index:remove_scenario(properties)
            end
         end
      end
   end
end

function SurfaceScenarioSelector:_find_valid_sites(habitat_map, elevation_map, habitat_types, width, length, allow_nonflat, overwrite_occupied_size)
   local sites = {}
   local num_sites = 0

   for j=1, habitat_map.height-(length-1) do
      for i=1, habitat_map.width-(width-1) do
         local is_suitable_habitat = self:_is_suitable_habitat(habitat_map, habitat_types, i, j, width, length, overwrite_occupied_size)

         if is_suitable_habitat then
            if not allow_nonflat then
               local is_flat = self:_is_flat(elevation_map, i, j, width, length)

               if is_flat then
                  num_sites = num_sites + 1
                  sites[num_sites] = { i = i, j = j }
               end
            end
         end
      end
   end

   return sites, num_sites
end

function SurfaceScenarioSelector:_is_suitable_habitat(habitat_map, habitat_types, i, j, width, length, overwrite_occupied_size)
   local is_suitable_habitat = true

   habitat_map:visit_block(i, j, width, length, function(value, sub_i, sub_j)
         if not habitat_types[value] then
            if value == 'occupied' and overwrite_occupied_size and overwrite_occupied_size > 1 then
               if self:_is_occupied_size_above(habitat_map, i + sub_i, j + sub_j, overwrite_occupied_size) then
                  is_suitable_habitat = false
                  return true  -- terminate iteration
               end
               return nil
            end
            is_suitable_habitat = false
            return true  -- terminate iteration
         end
      end)
      
   return is_suitable_habitat
end

function SurfaceScenarioSelector:_is_occupied_size_above(habitat_map, i, j, min_size)
   -- Bounded flood fill in X+, Y+.
   local function count_occupied(x, y)
      if x > min_size or y > min_size or habitat_map:get(i + x, j + y) ~= 'occupied' then
         return 0
      else
         return 1 + count_occupied(x + 1, y) + count_occupied(x, y + 1)
      end
   end
   return count_occupied(0, 0) > min_size
end

function SurfaceScenarioSelector:_is_flat(elevation_map, i, j, width, length)
   local is_flat = true
   local elevation = elevation_map:get(i, j)

   elevation_map:visit_block(i, j, width, length, function(value)
         if value ~= elevation then
            is_flat = false
            -- return true to terminate iteration
            return true
         end
      end)

   return is_flat
end

function SurfaceScenarioSelector:_mark_habitat_map(habitat_map, i, j, width, length)
   habitat_map:set_block(i, j, width, length, 'occupied')
end

return SurfaceScenarioSelector
