local Array2D = require 'services.server.world_generation.array_2D'
local FilterFns = require 'services.server.world_generation.filter.filter_fns'
local Biome = require 'services.server.world_generation.biome'
local BlueprintGenerator = require 'services.server.world_generation.blueprint_generator'
local MicroMapGenerator = require 'services.server.world_generation.micro_map_generator'
local Landscaper = require 'services.server.world_generation.landscaper'
local TerrainGenerator = require 'services.server.world_generation.terrain_generator'
local HeightMapRenderer = require 'services.server.world_generation.height_map_renderer'
local HabitatManager = require 'services.server.world_generation.habitat_manager'
local OverviewMap = require 'services.server.world_generation.overview_map'
local ScenarioIndex = require 'services.server.world_generation.scenario_index'
local OreScenarioSelector = require 'services.server.world_generation.ore_scenario_selector'
local SurfaceScenarioSelector = require 'services.server.world_generation.surface_scenario_selector'
local DetachedRegionSet = require 'services.server.world_generation.detached_region_set'
local Timer = require 'services.server.world_generation.timer'
local RandomNumberGenerator = _radiant.math.RandomNumberGenerator
local Point2 = _radiant.csg.Point2
local Rect2 = _radiant.csg.Rect2
local Region2 = _radiant.csg.Region2
local Point3 = _radiant.csg.Point3
local Cube3 = _radiant.csg.Cube3
local Region3 = _radiant.csg.Region3
local log = radiant.log.create_logger('world_generation')

local WorldGenerationService = class()

local VERSIONS = {
   ZERO = 0,
   MAKE_BIOME_PUBLIC = 1,
   MAKE_BIOME_NOT_CONTROLLER = 2,
   POPULATION_FACTION_ISOLATION = 3
}

function WorldGenerationService:get_version()
   return VERSIONS.POPULATION_FACTION_ISOLATION
end

function WorldGenerationService:initialize()
   self._sv = self.__saved_variables:get_data()

   if not self._sv._initialized then
      self._sv._initialized = true
      self._sv.version = self:get_version()
      self.__saved_variables:mark_changed()
   else
      -- TODO: support tile generation after load
      -- TODO: make sure all rngs dependent on the tile seed
      self:_fixup_post_load()
      self:_setup_biome_data(self._sv.biome_alias)
   end
end

function WorldGenerationService:create_new_game(seed, biome_src, async)
   self:set_seed(seed)
   self._async = async
   self._enable_scenarios = radiant.util.get_config('enable_scenarios', true)

   self:_setup_biome_data(biome_src)

   local biome_generation_data = self._biome_generation_data

   self._micro_map_generator = MicroMapGenerator(biome_generation_data, self._rng, seed)
   self._terrain_generator = TerrainGenerator(biome_generation_data, self._rng, seed)
   self._height_map_renderer = HeightMapRenderer(biome_generation_data)

   self._landscaper = Landscaper(biome_generation_data, self._rng, seed)
   self._habitat_manager = HabitatManager(biome_generation_data, self._landscaper)
   self.overview_map = OverviewMap(biome_generation_data, self._landscaper)

   self._scenario_index = ScenarioIndex(biome_generation_data, self._rng)
   self._ore_scenario_selector = OreScenarioSelector(self._scenario_index, biome_generation_data, self._rng)
   self._surface_scenario_selector = SurfaceScenarioSelector(self._scenario_index, biome_generation_data, self._rng)

   stonehearth.static_scenario:create_new_game(seed)
   stonehearth.dynamic_scenario:start()

   self.blueprint_generator = BlueprintGenerator(biome_generation_data)

   self._sv._starting_location = nil
   self.__saved_variables:mark_changed()
end

function WorldGenerationService:create_empty_world(biome_src)
   if not biome_src then
      biome_src = radiant.util.get_config('world_generation.default_biome', 'stonehearth:biome:temperate')
   end
   self:_setup_biome_data(biome_src)
end

function WorldGenerationService:_setup_biome_data(biome_src)
   self._sv.biome_alias = biome_src
   self._biome = radiant.resources.load_json(self._sv.biome_alias)
   if self._biome.applied_manifests then
      for name, file in pairs(self._biome.applied_manifests) do
         _radiant.res.apply_manifest(file)
      end
   end
   self._biome_generation_data = Biome(self._sv.biome_alias, self._biome.generation_file)
   radiant.terrain.get_terrain_component():set_generation_file(self._biome.generation_file)
   stonehearth.static_scenario:set_biome(self._biome_generation_data)

   radiant.events.trigger_async(self, 'stonehearth:world_generation:biome_initialized')
   self.__saved_variables:mark_changed()
   
   radiant.events.trigger(radiant, 'stonehearth:biome_set', { biome_uri = self._sv.biome_alias })
end

function WorldGenerationService:get_biome_alias()
   return self._sv.biome_alias
end

function WorldGenerationService:get_biome()
   return self._biome
end

function WorldGenerationService:get_biome_generation_data()
   return self._biome_generation_data
end

function WorldGenerationService:set_seed(seed)
   log:warning('WorldGenerationService using seed %d', seed)
   self._sv.seed = seed
   self.__saved_variables:mark_changed()

   self._rng = RandomNumberGenerator(self._sv.seed)
end

function WorldGenerationService:get_seed()
   return self._sv.seed
end

function WorldGenerationService:_report_progress(progress)
   radiant.events.trigger(radiant, 'stonehearth:generate_world_progress', {
      progress = progress * 100
   })
end

-- set and populate the blueprint
-- Only down on world generation
function WorldGenerationService:set_blueprint(blueprint)
   assert(self._biome_generation_data, "cannot find biome_generation_data")
   local seconds = Timer.measure(
      function()
         local tile_size = self._biome_generation_data:get_tile_size()
         local macro_blocks_per_tile = tile_size / self._biome_generation_data:get_macro_block_size()
         local blueprint_generator = self.blueprint_generator
         local micro_map_generator = self._micro_map_generator
         local landscaper = self._landscaper
         local full_micro_map, full_underground_micro_map
         local full_elevation_map, full_underground_elevation_map, full_feature_map, full_habitat_map

         full_micro_map, full_elevation_map = micro_map_generator:generate_micro_map(blueprint.width, blueprint.height)
         full_underground_micro_map, full_underground_elevation_map = micro_map_generator:generate_underground_micro_map(full_micro_map)

         full_feature_map = Array2D(full_elevation_map.width, full_elevation_map.height)

         -- determine which features will be placed in which cells
         landscaper:mark_water_bodies(full_elevation_map, full_feature_map)
         landscaper:mark_trees(full_elevation_map, full_feature_map)
         landscaper:mark_berry_bushes(full_elevation_map, full_feature_map)
         landscaper:mark_plants(full_elevation_map, full_feature_map)
         landscaper:mark_boulders(full_elevation_map, full_feature_map)

         full_habitat_map = self._habitat_manager:derive_habitat_map(full_elevation_map, full_feature_map)

         -- shard the maps and store in the blueprint
         -- micro_maps are overlapping so they need a different sharding function
         -- these maps are at macro_block_size resolution (32x32)
         blueprint_generator:store_micro_map(blueprint, "micro_map", full_micro_map, macro_blocks_per_tile)
         blueprint_generator:store_micro_map(blueprint, "underground_micro_map", full_underground_micro_map, macro_blocks_per_tile)
         -- these maps are at feature_size resolution (16x16)
         blueprint_generator:shard_and_store_map(blueprint, "elevation_map", full_elevation_map)
         blueprint_generator:shard_and_store_map(blueprint, "underground_elevation_map", full_underground_elevation_map)
         blueprint_generator:shard_and_store_map(blueprint, "feature_map", full_feature_map)
         blueprint_generator:shard_and_store_map(blueprint, "habitat_map", full_habitat_map)

         -- location of the world origin in the coordinate system of the blueprint
         blueprint.origin_x = math.floor(blueprint.width * tile_size / 2)
         blueprint.origin_y = math.floor(blueprint.height * tile_size / 2)

         -- create the overview map
         self.overview_map:derive_overview_map(full_elevation_map, full_feature_map, blueprint.origin_x, blueprint.origin_y)

         self._blueprint = blueprint
      end
   )
   log:info('Blueprint population time: %.3fs', seconds)
end

function WorldGenerationService:get_blueprint()
   return self._blueprint
end

function WorldGenerationService:place_camp_if_needed_command(session, response)
   if self._sv.seed == nil then -- this is for autotests (microworlds)
      if not self._debug_need_camp_filter_fn or
         not self._debug_need_camp_filter_fn(session.player_id) then
         return false
      end
   end

   local pop = stonehearth.population:get_population(session.player_id)

   local need_camp = not pop:is_camp_placed()

   return need_camp -- and self._sv._starting_location == nil
end

function WorldGenerationService:get_world_seed_command(session, response)
   response:resolve({seed=self._sv.seed})
end

function WorldGenerationService:set_starting_location(location)
   self._sv._starting_location = location

   -- clear the starting location of all revealed scenarios
   local exclusion_radius = self._biome_generation_data:get_feature_block_size()
   local rect = Rect2(location):inflated(Point2(exclusion_radius, exclusion_radius))
   local exclusion_region = Region2(rect)

   stonehearth.static_scenario:reveal_region(exclusion_region, function()
         return false
      end)

   if radiant.util.get_config('enable_full_vision', false) then
      local radius = radiant.math.MAX_INT32-1
      local region = Region2(Rect2(
            Point2(-radius,  -radius),
            Point2( radius+1, radius+1)
         ))
      region:translate(self._sv._starting_location)
      stonehearth.static_scenario:reveal_region(region)
   end
end

-- get the (i,j) index of the blueprint tile for the world coordinates (x,y)
function WorldGenerationService:get_tile_index(x, y)
   local blueprint = self._blueprint
   local tile_size = self._biome_generation_data:get_tile_size()
   local i = math.floor((x + blueprint.origin_x) / tile_size) + 1
   local j = math.floor((y + blueprint.origin_y) / tile_size) + 1
   return i, j
end

-- get the world coordinates of the origin (top-left corner) of the tile
function WorldGenerationService:get_tile_origin(i, j, blueprint)
   local x, y
   local tile_size = self._biome_generation_data:get_tile_size()
   x = (i-1)*tile_size - blueprint.origin_x
   y = (j-1)*tile_size - blueprint.origin_y

   return x, y
end

function WorldGenerationService:generate_tiles(i, j, radius)
   self:_run_async(
      function()
         local blueprint = self._blueprint
         local x_min = math.max(i-radius, 1)
         local x_max = math.min(i+radius, blueprint.width)
         local y_min = math.max(j-radius, 1)
         local y_max = math.min(j+radius, blueprint.height)
         local num_tiles = (x_max-x_min+1) * (y_max-y_min+1)
         local n = 0
         local progress = 0.0
         local metadata
         local water_regions = DetachedRegionSet()

         self:_report_progress(progress)

         -- Generate tile offsets.
         local tile_offsets = {}
         for b=y_min, y_max do
            for a=x_min, x_max do
               assert(blueprint:in_bounds(a, b))
               table.insert(tile_offsets, { a, b })
            end
         end
         
         -- Shuffle the tile order, so we don't always generate 100% guaranteed landmarks on the first tile.
         self._rng:set_seed(self._sv.seed)
         radiant.util.shuffle_list(tile_offsets, self._rng)

         -- Actually generate the tiles.
         for _, tile_offset in ipairs(tile_offsets) do
            local a, b = tile_offset[1], tile_offset[2]

            metadata = self:_generate_tile_internal(a, b)
            water_regions:add_region(metadata.water_region)

            n = n + 1
            progress = n / num_tiles

            if progress < 1 then
               self:_report_progress(progress)
            end
         end

         self:_add_water_bodies(water_regions:get_regions())

         self:_report_progress(1.0)
         stonehearth.game_creation:on_world_generation_complete()

         if self._job then
            self._job = nil
         end
      end
   )
end

function WorldGenerationService:_generate_tile_internal(i, j)
   local blueprint = self._blueprint
   local tile_size = self._biome_generation_data:get_tile_size()
   local tile_map, underground_tile_map, tile_info, tile_seed
   local micro_map, underground_micro_map
   local elevation_map, underground_elevation_map, feature_map, habitat_map
   local offset_x, offset_y
   local metadata = {}

   tile_info = blueprint:get(i, j)
   assert(not tile_info.generated)

   log:info('Generating tile (%d,%d)', i, j)

   -- calculate the world offset of the tile
   offset_x, offset_y = self:get_tile_origin(i, j, blueprint)

   -- make each tile deterministic on its coordinates (and game seed)
   tile_seed = self:_get_tile_seed(i, j)
   self._rng:set_seed(tile_seed)

   -- get the various maps from the blueprint
   micro_map = tile_info.micro_map
   underground_micro_map = tile_info.underground_micro_map
   elevation_map = tile_info.elevation_map
   underground_elevation_map = tile_info.underground_elevation_map
   feature_map = tile_info.feature_map
   habitat_map = tile_info.habitat_map

   -- generate the high resolution heightmap for the tile
   local seconds = Timer.measure(
      function()
         tile_map = self._terrain_generator:generate_tile(i,j,micro_map)
         underground_tile_map = self._terrain_generator:generate_underground_tile(underground_micro_map)
      end
   )
   log:info('Terrain generation time: %.3fs', seconds)
   self:_yield()

   -- render heightmap to region3
   local tile_region = self:_render_heightmap_to_region(tile_map, underground_tile_map)
   self:_yield()

   -- place lakes and rivers
   -- do this before adding to terrain so we can get the ring tesselation
   metadata.water_region = self:_place_water_bodies(tile_region, tile_map, feature_map)
   -- translate the water_region to world coordinates
   metadata.water_region:translate(Point3(offset_x, 0, offset_y))
   self:_yield()

   self:_add_region_to_terrain(tile_region, offset_x, offset_y)
   self:_yield()

   -- place flora
   self:_place_flora(tile_map, feature_map, offset_x, offset_y)
   self:_yield()

   -- place scenarios
   -- INCONSISTENCY: Ore veins extend across tiles that are already generated, but are truncated across tiles
   -- that have yet to be generated.
   self:_place_scenarios(habitat_map, elevation_map, underground_elevation_map, offset_x, offset_y)
   self:_yield()

   tile_info.generated = true

   return metadata
end

function WorldGenerationService:_render_heightmap_to_region(tile_map, underground_tile_map)
   local tile_region = Region3()

   local seconds = Timer.measure(
      function()
         self._height_map_renderer:render_height_map_to_region(tile_region, tile_map, underground_tile_map)
      end
   )

   log:info('Height map to region time: %.3fs', seconds)
   return tile_region
end

function WorldGenerationService:_add_region_to_terrain(tile_region, offset_x, offset_y)
   local seconds = Timer.measure(
      function()
         self._height_map_renderer:add_region_to_terrain(tile_region, offset_x, offset_y)
      end
   )

   log:info('Add region to terrain time: %.3fs', seconds)
end

function WorldGenerationService:_place_water_bodies(tile_region, tile_map, feature_map)
   local water_region
   local seconds = Timer.measure(
      function()
         -- 0, 0 for the tile offset since we'll translate later
         water_region = self._landscaper:place_water_bodies(tile_region, tile_map, feature_map, 0, 0)
      end
   )

   log:info('Place water bodies time: %.3fs', seconds)
   return water_region
end

function WorldGenerationService:_place_flora(tile_map, feature_map, offset_x, offset_y)
   local seconds = Timer.measure(
      function()
         self._landscaper:place_flora(tile_map, feature_map, offset_x, offset_y)
      end
   )

   log:info('Landscaper time: %.3fs', seconds)
end

function WorldGenerationService:_place_scenarios(habitat_map, elevation_map, underground_elevation_map, offset_x, offset_y)
   if not self._enable_scenarios then
      return
   end

   local seconds = Timer.measure(
      function()
         self._surface_scenario_selector:place_immediate_scenarios(habitat_map, elevation_map, offset_x, offset_y)

         self._ore_scenario_selector:place_revealed_scenarios(underground_elevation_map, elevation_map, offset_x, offset_y)
         self._surface_scenario_selector:place_revealed_scenarios(habitat_map, elevation_map, offset_x, offset_y)
      end
   )

   log:info('Static scenario time: %.3fs', seconds)
end

function WorldGenerationService:_add_water_bodies(regions)
   local biome_landscape_info = self._biome_generation_data:get_landscape_info()
   local biome_water_height_delta = biome_landscape_info and biome_landscape_info.water and biome_landscape_info.water.water_height_delta
   local water_height_delta = biome_water_height_delta or 1.5

   local seconds = Timer.measure(
      function()
         for _, terrain_region in pairs(regions) do
            terrain_region:force_optimize('add water bodies')

            local terrain_bounds = terrain_region:get_bounds()

            -- Water level is 1.5 blocks below terrain. (by default, can be changed in the biome json)
            -- Avoid filling to integer height so that we can avoid raise and lower layer spam.
            local height = terrain_bounds:get_size().y - water_height_delta

            local water_bounds = Cube3(terrain_bounds)
            water_bounds.max.y = water_bounds.max.y - math.floor(water_height_delta)

            local water_region = terrain_region:intersect_cube(water_bounds)
            stonehearth.hydrology:create_water_body_with_region(water_region, height)
         end
      end
   )

   log:info('Add water bodies time: %.3fs', seconds)
end

function WorldGenerationService:_get_tile_seed(x, y)
   local location_hash = Point2(x, y):hash()
   -- using Point2 as an integer pair hash
   local tile_seed = Point2(self._sv.seed, location_hash):hash()
   return tile_seed
end

function WorldGenerationService:_run_async(fn)
   if self._async then
      self._job = radiant.create_background_task('World Generation', fn)
   else
      fn()
   end
end

function WorldGenerationService:_yield()
   if self._async then
      coroutine.yield()
   end
end

function WorldGenerationService:_fixup_post_load()
   self._sv.version = self._sv.version or VERSIONS.ZERO

   if self._sv.version < VERSIONS.MAKE_BIOME_PUBLIC then
      self._sv.biome = self._sv._biome
      self._sv._biome = nil
   end

   if self._sv.version < VERSIONS.MAKE_BIOME_NOT_CONTROLLER then
      self._sv.biome_alias = self._sv.biome:get_uri()
      self._sv.biome = nil
   end

   if self._sv.version < VERSIONS.POPULATION_FACTION_ISOLATION then
      local players = stonehearth.player:get_non_npc_players()
      for player_id, _ in pairs(players) do
         local population = stonehearth.population:get_population(player_id)
         if population and not (self._sv.seed ~= nil and self._sv._starting_location == nil) then
            population:place_camp()
         end
      end
   end

   self._sv.version = self:get_version()
   self.__saved_variables:mark_changed()
end

return WorldGenerationService
