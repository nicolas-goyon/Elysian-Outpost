local HabitatManager = require 'services.server.world_generation.habitat_manager'
local ScenarioSelector = require 'services.server.world_generation.scenario_selector'
local log = radiant.log.create_logger('scenario_index')

local ScenarioIndex = class()

local activation_types = {
   immediate = true,
   revealed  = true
}

local location_types = {
   surface     = true,
   underground = true
}

function ScenarioIndex:__init(biome,rng)
   self._biome = biome
   self._rng = rng

   local json = radiant.resources.load_json('stonehearth:scenarios:scenario_index')
   self._categories = self:_parse_scenario_index(json)
end

function ScenarioIndex:get_category(name)
   return self._categories[name]
end

-- get a list of scenarios from all the categories
function ScenarioIndex:select_scenarios(location_type, activation_type, habitat_volumes)
   local selected_scenarios = {}
   local category, selector, list

   for name, category in pairs(self._categories) do
      if category.location_type == location_type and category.activation_type == activation_type then
         -- select using scenario selector
         list = category.selector:select_scenarios(category.density, habitat_volumes)

         for _, properties in pairs(list) do
            table.insert(selected_scenarios, properties)
         end
      end
   end

   return self:_sort_scenarios(selected_scenarios)
end

function ScenarioIndex:remove_scenario(properties)
   local scenario_name = properties.name
   local category_name = properties.category

   -- just remove from future selection, don't remove from master index
   self._categories[category_name].selector:remove(scenario_name)
end

function ScenarioIndex:contains(category_name, scenario_name)
   local category = self._categories[category_name]
   return category ~= nil and category.selector:contains(scenario_name)
end

-- order first by priority, then by area, then by weight
function ScenarioIndex:_sort_scenarios(scenarios)
   local categories = self._categories

   -- Split scenarios that want to be ordered from those that want to be shuffled.
   local sorted_scenarios = {}
   local shuffled_scenarios = {}
   for _, scenario in ipairs(scenarios) do
      if categories[scenario.category].priority == 'random' then
         table.insert(shuffled_scenarios, scenario)
      else
         table.insert(sorted_scenarios, scenario)
      end
   end

   local comparator = function(a, b)
      local category_a = a.category
      local category_b = b.category

      if category_a ~= category_b then
         local priority_a = categories[category_a].priority
         local priority_b = categories[category_b].priority
         -- higher priority sorted to lower index
         return priority_a > priority_b
      end

      local area_a = a.size.width * a.size.length
      local area_b = b.size.width * b.size.length
      if area_a ~= area_b then
         -- larger area sorted to lower index
         return area_a > area_b
      end

      -- rare scenarios sorted to lower index
      return a.weight < b.weight
   end
   
   table.sort(sorted_scenarios, comparator)
   radiant.util.shuffle(shuffled_scenarios, self._rng)

   local result = {}
   radiant.array_append(result, sorted_scenarios)
   radiant.array_append(result, shuffled_scenarios)
   return result
end

function ScenarioIndex:_parse_scenario_index(json)
   -- load all the categories
   local categories = {}
   for name, properties in pairs(json.static.categories) do
      -- parse activation type
      if not self:_is_valid_activation_type(properties.activation_type) then
         log:error('Error parsing "%s": Invalid activation_type "%s".', file, tostring(properties.activation_type))
      end
      if not self:_is_valid_location_type(properties.location_type) then
         log:error('Error parsing "%s": Invalid location_type "%s".', file, tostring(properties.location_type))
      end
      local category = {
         selector = ScenarioSelector(self._biome,self._rng)
      }
      -- propagate the other category properties into the category
      for key, value in pairs(properties) do
         category[key] = value
      end
      -- convert density from percent
      category.density = category.density / 100
      
      if type(category.max_count) == 'table' then
         category.max_count = category.max_count[self._rng:get_int(1, #category.max_count)]
      end

      categories[name] = category
   end

   -- load the scenarios into the categories
   for _, file in pairs(json.static.scenarios) do
      local properties = radiant.resources.load_json(file)

      -- parse category
      local category = categories[properties.category]
      if category then
         properties = self:_construct_map_from_type_arrays(properties, file)
         category.selector:add(properties)
      else
         log:error('Error parsing "%s": Category "%s" has not been defined.', file, tostring(properties.category))
      end
   end

   return categories
end

-- parse the habitat_types and biomes array into a set so we can index by key
function ScenarioIndex:_construct_map_from_type_arrays(properties, file)
   local habitat_types = {}
   local error_message = nil

   -- Parse habitat types
   for _, value in pairs(properties.habitat_types) do
      if HabitatManager.is_valid_habitat_type(value) then
         habitat_types[value] = value
      else
         -- concatenate multiple errors into a single string
         if error_message == nil then
            error_message = ''
         end
         error_message = string.format('%s Invalid habitat type "%s".', error_message, tostring(value))
      end
   end

   if error_message then
      log:error('Error parsing "%s": "%s"', file, error_message)
   end
   
   properties.habitat_types = habitat_types

   -- Parse biomes
   if properties.biomes then
      local biomes = {}
      for _, value in pairs(properties.biomes) do
         biomes[value] = value
      end
      properties.biomes = biomes
   end

   return properties
end

function ScenarioIndex:_is_valid_activation_type(activation_type)
   return activation_types[activation_type]
end

function ScenarioIndex:_is_valid_location_type(location_type)
   return location_types[location_type]
end

function ScenarioIndex:_is_valid_habitat_type(habitat_type)
   return habitat_types[habitat_type]
end

return ScenarioIndex
