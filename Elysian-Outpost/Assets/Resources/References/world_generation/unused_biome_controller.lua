--[[
This is an unused controller that we keep around for save load compatibility
]]

local UnusedBiomeController = class()

function UnusedBiomeController:initialize()
   self._sv.uri = nil
end

function UnusedBiomeController:get_uri()
   return self._sv.uri
end

return UnusedBiomeController
