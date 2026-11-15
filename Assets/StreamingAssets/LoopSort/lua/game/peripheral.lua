local C=require('game.peripheral_config')
local P={config=C}
local activities=require('game.activities')
local Signin=require('game.signin')
local reward_names={gold='金币',life='分钟无限生命',undobooster='撤回',shufflebooster='打乱',capacitybooster='加车',conveyorcapacitybooster='扩容',block='任务积分'}
local reward_boosters={undobooster='undo',shufflebooster='shuffle',capacitybooster='truck',conveyorcapacitybooster='belt'}
function P.normalize(p)
    p:EnsureContainers()
    p.dailyRewardCount=Signin.count(p)
    if p.peripheralVersion<1 then
        p.areaId=p.homeArea==1 and 3 or p.homeArea==2 and 2 or 1
        p.lives=C.max_lives;p.nextLifeAt=0;p.peripheralVersion=1
    end
    p.areaId=math.max(1,math.min(#C.areas,p.areaId));p.avatarIndex=math.max(0,math.min(8,p.avatarIndex))
    p.lives=math.max(0,math.min(C.max_lives,p.lives))
end
function P.tick(game) activities.flush(game,activities.tick(game)) end
function P.life_label(game)
    local p=game.Progress;local now=game.UtcSeconds
    if p.unlimitedLifeUntil>now then return '∞' end
    return tostring(p.lives)
end
function P.life_detail(game)
    local p,now=game.Progress,game.UtcSeconds
    local seconds=p.unlimitedLifeUntil>now and p.unlimitedLifeUntil-now or p.nextLifeAt-now
    if p.unlimitedLifeUntil<=now and p.lives>=C.max_lives then return '生命已满' end
    seconds=math.max(0,math.ceil(seconds))
    return (p.unlimitedLifeUntil>now and '无限生命剩余 ' or '下一颗生命 ')..string.format('%02d:%02d',math.floor(seconds/60),seconds%60)
end
function P.can_start(game) return true end
function P.refill(game) game.Hud:Toast('无需体力，可直接开始关卡') end
function P.reward_label(rewards)
    local parts={}
    for kind,amount in rewards:gmatch('([%w_]+):(%d+)') do
        parts[#parts+1]=(reward_names[kind] or kind)..' × '..amount
    end
    return table.concat(parts,'  ')
end
function P.reward(game,rewards)
    activities.tick(game)
    local p=game.Progress
    for kind,amount in rewards:gmatch('([%w_]+):(%d+)') do
        local count=tonumber(amount)
        if kind=='gold' then -- retired currency; preserve existing save value
        elseif kind=='block' then activities.add(game,'blocks',count)
        elseif kind=='life' then -- energy removed
        elseif reward_boosters[kind] then local id=reward_boosters[kind];p[id]=p[id]+count end
    end
end
function P.complete(game,won)
    if game.IsTestRun then return 0 end
    local p=game.Progress
    if game.IsDailyRun then
        local list=won and p.dailyCompleted or p.dailyFailed
        if not list:Contains(game.DailyDate) then list:Add(game.DailyDate) end
        if won then p.dailyFailed:Remove(game.DailyDate) end
    end
    activities.on_result(game,won);game:SaveProgress();return 0
end
function P.daily_count(game,month)
    local count=0
    for i=0,game.Progress.dailyCompleted.Count-1 do if game.Progress.dailyCompleted[i]:sub(1,7)==month then count=count+1 end end
    return count
end
function P.claim_daily_milestone(game,index)
    local row=C.daily_milestones[index];if not row then return end
    local key=game.CurrentMonth..':'..row.days;local p=game.Progress
    if p.dailyClaimed:Contains(key) or P.daily_count(game,game.CurrentMonth)<row.days then return end
    P.reward(game,row.reward);p.dailyClaimed:Add(key);game:SaveProgress();game.Hud:PeripheralPage('calendar');game.Hud:Toast('已领取：'..P.reward_label(row.reward))
end
function P.claim_daily_reward(game,doubled)
    local p=game.Progress;if p.lastDailyRewardDay==game.TodayKey then return end
    local state=Signin.state(p,game.TodayKey)
    local rewards=Signin.rewards(state.day,doubled and 2 or 1)
    for _,item in ipairs(rewards) do p[item.id]=p[item.id]+item.count end
    p.dailyRewardCount=state.count+1
    p.lastDailyRewardDay=game.TodayKey;game:SaveProgress();game.Hud:PeripheralPage('daily_reward')
    game.Hud:Toast('已领取：'..Signin.label(rewards))
end
function P.area(game) return C.areas[game.Progress.areaId] or C.areas[1] end
function P.select_area(game,id)
    local area=C.areas[id];if not area then return false end
    if game.Progress.highestUnlocked<area.unlock then game.Hud:Toast('到达第 '..area.unlock..' 关后解锁');return false end
    game.Progress.areaId=id;game:SaveProgress();return true
end
function P.booster_unlocked(game,id)
    local row=C.boosters[id];return row and game.Progress.highestUnlocked>=row.unlock
end
function P.booster_ready(game,id)
    if not P.booster_unlocked(game,id) then game.Hud:Toast('此道具尚未解锁');return false end
    if game.Progress.boosterLessons:Contains(id) then return true end
    game:SetPaused(true);game.Hud:PeripheralPage('booster_lesson',id);return false
end
function P.accept_booster(game,id)
    if not C.boosters[id] then return end
    local p=game.Progress
    if not p.boosterLessons:Contains(id) then p.boosterLessons:Add(id);game:SaveProgress() end
    game.Hud:Close();game:SetPaused(false)
    local flow=require('game.flow')
    if id=='shuffle' then flow.shuffle_select(game) else flow[id=='belt' and 'belt' or id=='truck' and 'truck' or 'undo'](game) end
end
return P
