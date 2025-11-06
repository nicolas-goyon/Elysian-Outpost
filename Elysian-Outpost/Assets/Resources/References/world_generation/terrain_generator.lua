local Biome = require 'services.server.world_generation.biome'
local Array2D = require 'services.server.world_generation.array_2D'
local NonUniformQuantizer = require 'lib.math.non_uniform_quantizer'
local FilterFns = require 'services.server.world_generation.filter.filter_fns'
local Wavelet = require 'services.server.world_generation.filter.wavelet'
local WaveletFns = require 'services.server.world_generation.filter.wavelet_fns'
local TerrainDetailer = require 'services.server.world_generation.terrain_detailer'
local Timer = require 'services.server.world_generation.timer'
local log = radiant.log.create_logger('world_generation')

local TerrainGenerator = class()

-- Definitions
-- Block = atomic unit of terrain that cannot be subdivided
-- macro_block = square unit of flat land, 32x32, but can shift a bit due to toplogy
-- Tile = 2D array of macro_blocks
--        These 256x256 terrain tiles are different from nav grid tiles which are 16x16.
-- World = the entire playspace of a game

function TerrainGenerator:__init(biome,rng, seed)
   self._biome = biome
   self._seed = seed
   self._tile_size = self._biome:get_tile_size()
   self._macro_block_size = self._biome:get_macro_block_size()
   self._rng = rng

   self._wavelet_levels = 4
   self._frequency_scaling_coeff = 0.69

   local oversize_tile_size = self._tile_size + self._macro_block_size
   self._oversize_map_buffer = Array2D(oversize_tile_size, oversize_tile_size)

   self._terrain_detailer = TerrainDetailer(self._biome, self._rng, self._seed, oversize_tile_size, oversize_tile_size)
end

function TerrainGenerator:generate_tile(i, j, micro_map)
   local oversize_map = self._oversize_map_buffer
   local tile_map

   self:_create_oversize_map_from_micro_map(oversize_map, micro_map)
   self:_quantize_height_map(oversize_map, false)
   -- copy the offset tile map from the oversize map
   tile_map = self:_extract_tile_map(oversize_map)
   self:_add_additional_details(i, j, tile_map, micro_map)

   return tile_map
end

function TerrainGenerator:generate_underground_tile(underground_micro_map)
   local oversize_map = self._oversize_map_buffer
   local underground_tile_map

   self:_create_oversize_map_from_micro_map(oversize_map, underground_micro_map)

   -- copy the offset tile map from the oversize map
   underground_tile_map = self:_extract_tile_map(oversize_map)
   return underground_tile_map
end

function TerrainGenerator:_create_oversize_map_from_micro_map(oversize_map, micro_map)
   local i, j, value
   local micro_width = micro_map.width
   local micro_height = micro_map.height

   for j=1, micro_height do
      for i=1, micro_width do
         value = micro_map:get(i, j)
         oversize_map:set_block((i-1)*self._macro_block_size+1, (j-1)*self._macro_block_size+1,
            self._macro_block_size, self._macro_block_size, value)
      end
   end
end

function TerrainGenerator:_quantize_height_map(height_map, is_micro_map)
   local quantizer = self._biome:get_quantizer()

   height_map:process(
      function(value)
         local quantized_value = quantizer:quantize(value)
         return quantized_value
      end
   )
end

function TerrainGenerator:_add_additional_details(i, j, height_map, micro_map)
   self._terrain_detailer:detail(i,j,height_map,micro_map)
end

-- must return a new tile_map each time
function TerrainGenerator:_extract_tile_map(oversize_map)
   local tile_map_origin = self._macro_block_size/2 + 1
   local tile_map = Array2D(self._tile_size, self._tile_size)
   Array2D.copy_block(tile_map, oversize_map,
      1, 1, tile_map_origin, tile_map_origin, self._tile_size, self._tile_size)

   return tile_map
end

return TerrainGenerator
