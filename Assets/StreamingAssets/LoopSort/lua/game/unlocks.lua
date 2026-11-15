local First=require('game.guide_levels')
local Unlocks={}
function Unlocks.level(p) return math.max(1,p.highestUnlocked or 1,p.currentLevel or 1) end
function Unlocks.mechanic(p,id)
    if id==0 then return false end
    local learned=p.learnedFeatures
    if learned then
        if type(learned)=='table' then for _,value in ipairs(learned) do if value==id then return true end end
        elseif learned:Contains(id) then return true end
    end
    return (First[id] or math.huge)<=Unlocks.level(p)
end
return Unlocks
