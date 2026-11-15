local C=require('game.activity_config')
local A={config=C}
local dirty=setmetatable({},{__mode='k'})
local counters={'login','win','winhard','winsuperhard','useboosters','usecapacity','useshuffle','useundo','useconveyorcapacity','spendgolds','earngolds','revive','winstreak','streak','reveal','multiplier','blocks'}
local names={login='累计登录',win='完成关卡',winhard='完成困难关卡',winsuperhard='完成超难关卡',useboosters='使用道具',usecapacity='使用加车道具',useshuffle='使用打乱道具',useundo='使用撤回道具',useconveyorcapacity='使用扩容道具',spendgolds='花费金币',earngolds='获得金币',revive='失败后继续',winstreak='最高连胜',reveal='揭示隐藏方块',multiplier='领取广告加倍奖励'}
-- String keys on C# userdata resolve members; use the generated dictionary indexer.
local function get(p,key,default) return p.luaData:ContainsKey(key) and (tonumber(p.luaData:get_Item(key)) or default or 0) or default or 0 end
local function put(p,key,value) p.luaData:set_Item(key,tostring(value)) end
local function prefix(p) return 'weekly.'..get(p,'weekly.start')..'.' end
local function pass_prefix(p) return 'pass.'..get(p,'pass.start')..'.' end
function A.tick(game)
    if game.IsTestRun then return false end
    local p,now=game.Progress,game.UtcSeconds;local changed=false
    local start=get(p,'weekly.start')
    if start<=0 or now>=start+C.week_seconds then
        put(p,'weekly.start',now)
        for _,key in ipairs(counters) do put(p,'weekly.stat.'..key,0) end
        p.luaData:set_Item('weekly.login','');changed=true
    end
    if not p.luaData:ContainsKey('weekly.login') or p.luaData:get_Item('weekly.login')~=game.TodayKey then
        p.luaData:set_Item('weekly.login',game.TodayKey);put(p,'weekly.stat.login',get(p,'weekly.stat.login')+1);changed=true
    end
    start=get(p,'pass.start')
    if start<=0 or now>=start+C.pass_seconds then
        put(p,'pass.start',now);put(p,'pass.level',1);put(p,'pass.keys',0);changed=true
    end
    if changed then dirty[game]=true end
    return changed
end
function A.quantity(game,key) return get(game.Progress,'weekly.stat.'..key) end
function A.add(game,key,amount)
    if game.IsTestRun then return end
    local p=game.Progress;put(p,'weekly.stat.'..key,A.quantity(game,key)+(amount or 1))
end
function A.record(game,key,amount)
    if game.IsTestRun then return end
    A.tick(game);A.add(game,key,amount);dirty[game]=true
end
function A.flush(game,force)
    if game.IsTestRun then return end
    if force or dirty[game] then game:SaveProgress();dirty[game]=nil end
end
function A.on_result(game,won)
    A.tick(game)
    if not won then put(game.Progress,'weekly.stat.streak',0);return end
    A.add(game,'win',1);A.add(game,'streak',1)
    put(game.Progress,'weekly.stat.winstreak',math.max(A.quantity(game,'winstreak'),A.quantity(game,'streak')))
    if game.Level.theme=='Hard' then A.add(game,'winhard',1) elseif game.Level.theme=='SuperHard' then A.add(game,'winsuperhard',1) end
    local p=game.Progress;local keys=get(p,'pass.keys')+(C.pass_tokens[game.Level.theme] or C.pass_tokens.Default)
    local level=get(p,'pass.level',1)
    -- Native RefreshProgress consumes each row's KeyCount before advancing.
    while level<#C.pass-1 and keys>=C.pass[level+1].keys do keys=keys-C.pass[level+1].keys;level=level+1 end
    put(p,'pass.level',level);put(p,'pass.keys',keys)
end
function A.booster(game,id)
    local map={undo='useundo',shuffle='useshuffle',belt='useconveyorcapacity',truck='usecapacity'}
    if map[id] then A.add(game,map[id],1);A.add(game,'useboosters',1) end
end
function A.day(game) return math.max(1,math.min(#C.missions,math.ceil((game.UtcSeconds-get(game.Progress,'weekly.start'))/86400))) end
function A.mission_name(row) return names[row.kind] or row.kind end
function A.claimed_mission(game,day,index) return game.Progress.luaData:ContainsKey(prefix(game.Progress)..'mission.'..day..'.'..index) end
function A.claim_mission(game,day,index)
    A.tick(game);local row=C.missions[day] and C.missions[day][index]
    if not row or day>A.day(game) or A.claimed_mission(game,day,index) or A.quantity(game,row.kind)<row.goal then return end
    game.Progress.luaData:set_Item(prefix(game.Progress)..'mission.'..day..'.'..index,'1')
    require('game.peripheral').reward(game,row.reward);game:SaveProgress();game.Hud:PeripheralPage('missions',tostring(day))
end
function A.claimed_milestone(game,index) return game.Progress.luaData:ContainsKey(prefix(game.Progress)..'milestone.'..index) end
function A.claim_milestone(game,index)
    A.tick(game);local row=C.milestones[index]
    if not row or A.claimed_milestone(game,index) or A.quantity(game,'blocks')<row.goal then return end
    game.Progress.luaData:set_Item(prefix(game.Progress)..'milestone.'..index,'1')
    require('game.peripheral').reward(game,row.reward);game:SaveProgress();game.Hud:PeripheralPage('mission_rewards')
end
function A.pass_level(game)
    local level=get(game.Progress,'pass.level',1)
    -- Native stops on the last row. Its claim unlocks when the remaining keys
    -- reach that row's cost; FreeClaim still makes the terminal reward one-shot.
    if level==#C.pass-1 and get(game.Progress,'pass.keys')>=C.pass[#C.pass].keys then return #C.pass end
    return level
end
function A.pass_keys(game) return get(game.Progress,'pass.keys') end
function A.claimed_pass(game,index) return game.Progress.luaData:ContainsKey(pass_prefix(game.Progress)..'free.'..index) end
function A.claim_pass(game,index,page)
    A.tick(game);local row=C.pass[index]
    if not row or index>A.pass_level(game) or A.claimed_pass(game,index) then return end
    game.Progress.luaData:set_Item(pass_prefix(game.Progress)..'free.'..index,'1')
    require('game.peripheral').reward(game,row.free);game:SaveProgress();game.Hud:PeripheralPage('pass',tostring(page or 0))
end
function A.pass_identity(game) A.tick(game);return tostring(get(game.Progress,'pass.start')) end
function A.premium(game) return game.Progress.luaData:ContainsKey(pass_prefix(game.Progress)..'premium') end
function A.purchase(game,transaction,cycle)
    A.tick(game)
    local p=game.Progress;local purchase='purchase.'..transaction
    if p.luaData:ContainsKey(purchase) then return end
    p.luaData:set_Item(purchase,cycle)
    if cycle==A.pass_identity(game) then
        p.luaData:set_Item(pass_prefix(p)..'premium','1');game.Hud:Toast('高级轨道已解锁')
    else
        p.luaData:set_Item('pending.'..purchase,cycle);game.Hud:Toast('活动周期已变化，购买凭据已保存，等待支付服务处理')
    end
    game:SaveProgress()
end
function A.claimed_paid(game,index) return game.Progress.luaData:ContainsKey(pass_prefix(game.Progress)..'paid.'..index) end
function A.claim_paid(game,index,page)
    A.tick(game);local row=C.pass[index]
    if not row or not A.premium(game) or index>A.pass_level(game) or A.claimed_paid(game,index) then return end
    game.Progress.luaData:set_Item(pass_prefix(game.Progress)..'paid.'..index,'1')
    require('game.peripheral').reward(game,row.paid);game:SaveProgress();game.Hud:PeripheralPage('pass',tostring(page or 0))
end
function A.remaining(game,pass)
    local duration=pass and C.pass_seconds or C.week_seconds
    local start=get(game.Progress,pass and 'pass.start' or 'weekly.start')
    local seconds=math.max(0,math.ceil(start+duration-game.UtcSeconds))
    return math.floor(seconds/86400)..'天 '..math.floor(seconds%86400/3600)..'小时'
end
return A
