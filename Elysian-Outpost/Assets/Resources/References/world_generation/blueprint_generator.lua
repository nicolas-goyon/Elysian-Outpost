local Array2D = require 'services.server.world_generation.array_2D'
local FilterFns = require 'services.server.world_generation.filter.filter_fns'
local Histogram = require 'lib.algorithms.histogram'
local RandomNumberGenerator = _radiant.math.RandomNumberGenerator
local log = radiant.log.create_logger('world_generation')

local BlueprintGenerator = class()

--used in tile based map storing
function BlueprintGenerator:__init(biome)
   self._terrain_info = biome:get_terrain_info()
end

function BlueprintGenerator:get_empty_blueprint(width, height, terrain_type)
   if terrain_type == nil then terrain_type = 'plains' end

   local blueprint = Array2D(width, height)
   local i, j, tile_info

   for j=1, blueprint.height do
      for i=1, blueprint.width do
         tile_info = {}
         tile_info.generated = false
         blueprint:set(i, j, tile_info)
      end
   end

   return blueprint
end

function BlueprintGenerator:store_micro_map(blueprint, key, full_micro_map, macro_blocks_per_tile)
   local local_micro_map

   for j=1, blueprint.height do
      for i=1, blueprint.width do
         -- +1 for the margins
         local_micro_map = Array2D(macro_blocks_per_tile+1, macro_blocks_per_tile+1)

         Array2D.copy_block(local_micro_map, full_micro_map, 1, 1,
            (i-1)*macro_blocks_per_tile+1, (j-1)*macro_blocks_per_tile+1,
            macro_blocks_per_tile+1, macro_blocks_per_tile+1)

         local e = blueprint:get(i, j)
         e[key] = local_micro_map
      end
   end
end

function BlueprintGenerator:shard_and_store_map(blueprint, key, full_map)
   local features_per_tile, local_map

   features_per_tile = full_map.width / blueprint.width
   assert(features_per_tile == full_map.height / blueprint.height)
   assert(features_per_tile % 1 == 0) -- assert is integer

   for j=1, blueprint.height do
      for i=1, blueprint.width do
         local_map = Array2D(features_per_tile, features_per_tile)

         Array2D.copy_block(local_map, full_map, 1, 1,
            (i-1)*features_per_tile+1, (j-1)*features_per_tile+1,
            features_per_tile, features_per_tile)

         local e = blueprint:get(i, j)
         e[key] = local_map
      end
   end
end

return BlueprintGenerator
