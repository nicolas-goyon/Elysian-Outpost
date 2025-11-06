local Array2D = require 'services.server.world_generation.array_2D'
local Biome = require 'services.server.world_generation.biome'
local log = radiant.log.create_logger('world_generation')

local HabitatManager = class()

local habitat_types = {
   none      = true,
   occupied  = true,
   plains    = true,
   foothills = true,
   mountains = true,
   forest    = true,
   water     = true,
}

function HabitatManager.is_valid_habitat_type(habitat_type)
   return habitat_types[habitat_type]
end

function HabitatManager:__init(biome, landscaper)
   self._biome = biome
   self._landscaper = landscaper
end

function HabitatManager:derive_habitat_map(elevation_map, feature_map)
   local biome = self._biome
   local habitat_map = Array2D(feature_map.width, feature_map.height)
   local i, j, feature_name, elevation, terrain_type, habitat_type

   for j=1, habitat_map.height do
      for i=1, habitat_map.width do
         elevation = elevation_map:get(i, j)
         terrain_type = biome:get_terrain_type(elevation)

         feature_name = feature_map:get(i, j)
         habitat_type = self:_get_habitat_type(terrain_type, feature_name)

         habitat_map:set(i, j, habitat_type)
      end
   end

   return habitat_map
end

-- This is likely to change
function HabitatManager:_get_habitat_type(terrain_type, feature_name)
   if terrain_type == 'mountains' then
      return 'mountains'
   end
   if self._landscaper:is_water_feature(feature_name) then
      return 'water'
   end
   if self._landscaper:is_forest_feature(feature_name) then
      return 'forest'
   end
   if feature_name ~= nil then
      return 'occupied'
   end
   if terrain_type == 'plains' then
      return 'plains'
   end
   if terrain_type == 'foothills' then
      return 'foothills'
   end
   log:error('Unable to derive habitat_type')
   return 'none'
end

return HabitatManager
