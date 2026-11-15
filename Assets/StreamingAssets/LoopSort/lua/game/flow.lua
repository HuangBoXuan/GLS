local config = require('game.config')
local Status = CS.LoopSort0.Core.RunStatus
local first_levels=require('game.guide_levels')
local Flow = {}
local peripheral=require('game.peripheral')
local activities=require('game.activities')
local guide_queues=setmetatable({},{__mode='k'})
local guide_attempts=setmetatable({},{__mode='k'})
local function price(id) return config.booster_prices[id] or 0 end
local function next_level_id(game)
    local selected
    for i=0,game.Catalog.levels.Length-1 do
        local id=game.Catalog.levels[i].id
        if id>game.Level.id and (not selected or id<selected) then selected=id end
    end
    return selected
end
function Flow.booster_price(id) return price(id) end
function Flow.grant_coins(progress) progress.startingCoinsGranted=true;return false end
function Flow.normalize_progress(p)
    p:EnsureContainers();p.currentLevel=math.max(1,p.currentLevel)
    if not p.playerName or p.playerName:match('^%s*$') then p.playerName='玩家' end
    Flow.grant_coins(p)
    p.coins=math.max(0,p.coins);p.homeArea=math.max(0,math.min(2,p.homeArea))
    for _,id in ipairs({'undo','shuffle','belt','truck'}) do p[id]=math.max(0,p[id]) end
    peripheral.normalize(p)
    require('game.ad_cadence').normalize(p,p.completedLevels.Count)
end
function Flow.pause(game) game:SetPaused(true); game.Hud:Pause(game.Progress) end
function Flow.retry(game)
    if game.IsDailyRun then game:StartDaily(game.DailyDate) else game:StartMainLevel(game.Level.id) end
end
function Flow.home(game)
    if game.IsDailyRun then game:LeaveDailyForHome() end
    game:PrepareHome()
    local selected = game.Level.id
    if game.State.Status == Status.Won then selected=next_level_id(game) or selected end
    game.Hud:Home(game.Progress, selected);game:RefreshMusic()
end
function Flow.play(game)
    if not peripheral.can_start(game) then return end
    if game.State.Status == Status.Won then
        if next_level_id(game) then return Flow.next_level(game) end
        return Flow.retry(game)
    end
    if game.State.Status == Status.Failed then return Flow.retry(game) end
    game.Hud:ShowGameplay();game:SetPaused(false);game:ShowNewMechanics()
end
function Flow.cancel(game)
    if game.Hud.HasDialog and game.Hud.DialogTitle=='矿车有危险！' then game:ShowFailurePage();return end
    if game.Hud.HasDialog and game.Hud.DialogTitle=='选择语言' then game.Hud:Pause(game.Progress);return end
    if guide_queues[game] then Flow.acknowledge_guide(game);return end
    game:ClearMenuSelection()
    if game.State.Status ~= Status.Playing and not game.Hud.IsHome then return Flow.home(game) end
    game.Hud:Close();game:SetPaused(game.Hud.IsHome)
end
function Flow.help(game) game:SetPaused(true);game.Hud:Mechanics() end
function Flow.levels(game)
    local index=0
    for i=0,game.Catalog.levels.Length-1 do if game.Catalog.levels[i].id==game.Level.id then index=i;break end end
    game:SetPaused(true);game.Hud:Levels(game.Catalog,game.Level.id,math.floor(index/20))
end
function Flow.select_level(game,id) game:StartMainLevel(id) end
function Flow.undo(game)
    if game.IsEnteringLevel then return false end
    if not Flow.can_afford(game,'undo') then game:OfferBooster('undo');return false end
    if game.UndoDepth==0 then game.Hud:Toast('暂时没有可以撤回的操作');return false end
    if not peripheral.booster_ready(game,'undo') then return false end
    if not Flow.spend(game,'undo') then return false end
    local restored=game:RestoreUndoView()
    if restored then game:PlaySound('click');if not game.IsDailyRun then game.Progress.currentLevel=game.Level.id end;game:SaveProgress() end
    return restored
end
function Flow.belt(game)
    if game.IsEnteringLevel then return end
    if not Flow.can_afford(game,'belt') then game:OfferBooster('belt');return end
    local state=game.State
    if not peripheral.booster_ready(game,'belt') then return end
    if (game.Paused and state.Status~=Status.Failed) or state.Status==Status.Won or not Flow.spend(game,'belt') then return end
    -- The controller applies capacity when the use presentation has arrived.
    local revive=state.Status==Status.Failed
    if revive then activities.add(game,'revive',1);game:SaveProgress() end
    game.Hud:Toast('传送带容量增加一组');game:PlaySound('click');game:AfterBeltExpansion(revive)
end
function Flow.truck(game)
    if game.IsEnteringLevel or game.Paused then return end
    if not Flow.can_afford(game,'truck') then game:OfferBooster('truck');return end
    if not peripheral.booster_ready(game,'truck') then return end
    local spare;local list=game.Level.spares
    if list then for i=0,list.Length-1 do if not game:IsCarrierPlaced(list[i].id) then spare=list[i];break end end end
    if not spare then game.Hud:Toast('本关没有可以加车的空位');return end
    if not Flow.spend(game,'truck') then return end
    game:PlaySound('click');game:AddTruckView(spare);game.Hud:Toast('已添加一个空托盘')
end
function Flow.shuffle(game,carrier)
    if game.IsEnteringLevel then return end
    game:SetShuffleSelection(false)
    if not Flow.can_afford(game,'shuffle') then game:OfferBooster('shuffle');return end
    if not game:ShuffleView(carrier) then game.Hud:Toast('请选择装有多种颜色的托盘');return end
    Flow.spend(game,'shuffle');game:PlaySound('click')
end
function Flow.shuffle_select(game)
    if game.IsEnteringLevel then return end
    if not Flow.can_afford(game,'shuffle') then game:OfferBooster('shuffle');return end
    if not peripheral.booster_ready(game,'shuffle') then return end
    game:SetShuffleSelection(not game.SelectingShuffle)
    game.Hud:Toast(game.SelectingShuffle and '请选择需要打乱的托盘' or '')
end
function Flow.name(game,value) game.Progress.playerName=value;game:SaveProgress() end
function Flow.area(game,value) game.Progress.homeArea=value;game:SaveProgress() end
function Flow.toggle(game,key)
    local p=game.Progress
    if key=='music' then p.music=not p.music;game:RefreshMusic()
    elseif key=='sound' then p.sound=not p.sound
    elseif key=='haptics' then p.haptics=not p.haptics end
    game:SaveProgress();game.Hud:Pause(p)
end
function Flow.buy(game,id) game.Hud:PeripheralPage('daily_reward');return false end
function Flow.spend(game,id)
    local p=game.Progress
    if not config.booster_prices[id] then return false end
    if p[id]<=0 then game:OfferBooster(id);return false end
    p[id]=p[id]-1;activities.booster(game,id);game:SaveProgress();return true
end
function Flow.can_afford(game,id) return config.booster_prices[id]~=nil and game.Progress[id]>0 end
function Flow.skip(game)
    if game.IsDailyRun then return Flow.home(game) end
    local id=next_level_id(game);if not id then game.Hud:Toast('已是最后一关');return end
    local p=game.Progress
    if not p.completedLevels:Contains(game.Level.id) and not p.skippedLevels:Contains(game.Level.id) then p.skippedLevels:Add(game.Level.id) end
    p.currentLevel=id;game:SaveProgress();game:StartMainLevel(id)
end
function Flow.next_level(game)
    if game.IsDailyRun then Flow.home(game);return end
    local id=next_level_id(game)
    if id then game:StartMainLevel(id);return end
    Flow.home(game);game.Hud:Toast('所有关卡均已完成，可再次挑战本关')
end
function Flow.won(game)
    game:CompletePeripheralRun(true)
    if game.IsDailyRun then game:PlaySound('complete');return end
    local p=game.Progress;p.highestUnlocked=math.max(p.highestUnlocked,game.Level.id+1)
    p.currentLevel=next_level_id(game) or game.Level.id
    if not p.completedLevels:Contains(game.Level.id) then p.completedLevels:Add(game.Level.id) end
    p.skippedLevels:Remove(game.Level.id)
    game:SaveProgress();game:PlaySound('complete')
end
function Flow.lost(game) game:CompletePeripheralRun(false) end
function Flow.peripheral_tick(game) peripheral.tick(game) end
function Flow.peripheral_flush(game) activities.flush(game) end
function Flow.peripheral_can_start(game) return peripheral.can_start(game) end
function Flow.peripheral_complete(game,won) return peripheral.complete(game,won) end
function Flow.activity_event(game,key,amount) activities.record(game,key,amount) end
function Flow.daily_reward_video(game) peripheral.claim_daily_reward(game,true) end
function Flow.pass_identity(game) return activities.pass_identity(game) end
function Flow.premium_purchased(game,transaction,cycle) activities.purchase(game,transaction,cycle) end
function Flow.guide_ids(level)
    local ids={};if not level.closed then ids[20]=true end
    local function any(a) return a and a.Length>0 end
    for i=0,level.carriers.Length-1 do
        local c=level.carriers[i]
        if c.curtainColor>0 then ids[21]=true end
        if c.barrier then ids[2]=true end
        if any(c.iceRequires) then ids[4]=true end
        if any(c.lockColors) then ids[23]=true end
        if any(c.ropeLocks) then ids[25]=true end
        if any(c.carpetLocks) then ids[26]=true end
        if c.tagColor>0 then ids[24]=true end
        if c.generator then ids[27]=true end
        if c.groups then for j=0,c.groups.Length-1 do
            local g=c.groups[j];if g.hidden then ids[22]=true end;if g.sticky then ids[28]=true end;if g.keyColor>0 then ids[23]=true end
        end end
    end
    local result=CS.LoopSort0.Core.RuleArrays.IntList()
    for _,id in ipairs({21,22,20,2,4,23,25,26,24,27,28}) do if ids[id] then result:Add(id) end end
    return result:ToArray()
end
function Flow.drop_tutorials(game) guide_queues[game]=nil end
function Flow.acknowledge_guide(game)
    local queue=guide_queues[game];if not queue then return end
    local guide=queue.current
    if guide and not game.Progress.learnedFeatures:Contains(guide.id) then
        game.Progress.learnedFeatures:Add(guide.id);game:SaveProgress()
    end
    queue.current=nil;Flow.show_next_guide(game)
end
function Flow.show_next_guide(game)
    local queue=guide_queues[game]
    if not queue or #queue==0 then guide_queues[game]=nil;game.Hud:Close();game:SetPaused(false);return end
    local guide=table.remove(queue,1);queue.current=guide;game:SetPaused(true)
    game.Hud:FeatureTutorial(guide,function()
        if guide_queues[game]==queue and queue.current==guide then Flow.acknowledge_guide(game) end
    end,true)
end
function Flow.show_new_mechanics(game)
    if game.IsEnteringLevel or game.IsTestRun or guide_queues[game] then return end
    local attempt=guide_attempts[game]
    if not attempt or attempt.state~=game.State then attempt={state=game.State,offered={}};guide_attempts[game]=attempt end
    guide_queues[game]={}
    local ids=Flow.guide_ids(game.Level);local guides=CS.LoopSort0.MechanicGuide.All
    for i=0,ids.Length-1 do
        local id=ids[i]
        if not attempt.offered[id] and (first_levels[id]==game.Level.id or not game.Progress.learnedFeatures:Contains(id)) then
            for j=0,guides.Length-1 do if guides[j].id==id then
                attempt.offered[id]=true;table.insert(guide_queues[game],guides[j]);break
            end end
        end
    end
    Flow.show_next_guide(game)
end
local function basic_tutorial(game)
    return not game.IsEnteringLevel and not game.IsTestRun and not game.IsDailyRun and game.Level.id>=1 and game.Level.id<=2 and game.State.Status==Status.Playing
end
function Flow.tutorial_allows_release(game,carrier)
    if game.IsEnteringLevel then return false end
    return not basic_tutorial(game) or Flow.tutorial_target(game)==carrier
end
function Flow.tutorial_target(game)
    if not basic_tutorial(game) or
        game.State.BeltCount>0 or game.State.PendingTransfers>0 then return -1 end
    local selected,best=-1,-math.huge
    for i=0,game.State.Carriers.Count-1 do
        local c=game.State.Carriers[i]
        if c.CanEmit and not c.packed and c.releasing==0 and game.State:ReleasableCount(i)>0 then
            local colors,count={},0
            for slot=0,c.cargo.Count-1,game.State.GroupSize do
                local value=game.State.Pieces[c.cargo[slot]].color
                if not colors[value] then colors[value]=true;count=count+1 end
            end
            local score=(count>1 and 1000 or 0)-c.cargo.Count
            if score>best then selected,best=i,score end
        end
    end
    return selected
end
function Flow.tutorial_instruction(game)
    if game.IsEnteringLevel or game.State.Status~=Status.Playing then return '' end
    if not game.IsDailyRun and game.Level.id<=2 then
        if game.State.BeltCount>0 or game.State.PendingTransfers>0 then return '方块沿传送带移动，靠近同色托盘后自动收集' end
        if game.Level.id==1 then return '点击手指指向的托盘，将红色方块收集到另一个托盘' end
        return game.State.PackedCount==0 and '先点击混色托盘，收集最外侧的黄色方块' or '继续收集红色方块，装满剩余托盘'
    end
    return game.MechanicHint or ''
end
function Flow.hint(level)
    -- Entry explanations belong to the first-discovery dialog, not a bottom toast.
    return ''
end
return Flow
