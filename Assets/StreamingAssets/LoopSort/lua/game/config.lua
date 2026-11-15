local Config = {
    starting_coins = 500000,
    booster_prices = { undo=300, shuffle=300, belt=800, truck=900 },
}
function Config.configure_engine()
    local set=CS.LoopSort0.HotUpdate.RuntimeTuning.Set
    local values={
        ['belt.speed']=5.5,['belt.force']=12,['belt.initial_impulse']=20,['belt.max_speed']=20,
        ['physics.fixed_step']=.0333,['physics.maximum_step']=.05,
        ['piece.belt_size']=.51,['receive.radius']=1.15,['receive.cooldown']=.25,
        ['receive.lap_margin']=1.2,['emission.animation_multiplier']=.5,['emission.duration']=.35,
        ['emission.slot_offset']=.5,['emission.collision_distance']=.25,
    }
    for key,value in pairs(values) do set(key,value) end
end
return Config
