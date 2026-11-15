-- One opportunity at the next main-level entrance after a completed round.
-- A skipped opportunity is consumed, never carried into the middle of a round.
local M={policy={first_entry_level=18}}
local first_levels=require('game.guide_levels')
local prefix='ads.entry.v2.'
local function get(p,key,default)
    local name=prefix..key
    if not p.luaData:ContainsKey(name) then return default end
    return tonumber(p.luaData:get_Item(name)) or default
end
local function put(p,key,value) p.luaData:set_Item(prefix..key,tostring(value)) end
function M.normalize(p,completed)
    p.adCadenceVersion=2
end
function M.time(p,seconds) end -- Legacy caller compatibility; time is not an ad trigger.
function M.begin(p,level)
    local source=get(p,'completed',-1);local skip=get(p,'skip',0)
    put(p,'completed',-1);put(p,'skip',0);put(p,'rewarded_level',-1)
    return level>=M.policy.first_entry_level and source==level-1 and skip==0
end
function M.round(p,level,won,seconds,discovered)
    if not won then return end
    local mechanism=discovered==true
    for _,first in pairs(first_levels) do if first==level then mechanism=true;break end end
    local rewarded=get(p,'rewarded_level',-1)==level
    put(p,'completed',level);put(p,'skip',(mechanism or rewarded) and 1 or 0)
    p.adTotalWins=math.min(1e9,math.max(0,p.adTotalWins or 0)+1)
end
function M.gameplay_rewarded(p,level) put(p,'rewarded_level',level) end
function M.eligible(p,level,won,seconds,now) return false end -- No settlement/failure ads.
function M.interstitial_closed(p,now) p.adLastInterstitialUtc=now end
function M.rewarded_closed(p,now) p.adLastRewardedClosedUtc=now end
return M
