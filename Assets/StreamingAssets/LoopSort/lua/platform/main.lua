-- Module entry. The build rewrites these namespaces to games.<gameId>.*.
local Runtime=require('game.module_runtime')
local Presenter=require('platform.presenter')
return Runtime.create(Presenter.new,{allow_fallback=false})
