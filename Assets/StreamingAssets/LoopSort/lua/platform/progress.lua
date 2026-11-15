-- All persisted state belongs to the host's current user and game identity.
-- A failed read is never treated as a new account, and never overwrites a save.
local Json = require('game.json')
local Peripheral = require('game.peripheral_config')
local Signin = require('game.signin')
local Activity = require('game.activity_config')
local Cadence = require('game.ad_cadence')
local Progress = {}
Progress.__index = Progress
Progress.prices = {undo=300,shuffle=300,belt=800,truck=900}
local KEY = 'LoopSort0.Player.v1'
local array_fields = {'dailyCompleted','dailyFailed','dailyClaimed','boosterLessons','learnedFeatures','completedLevels','skippedLevels'}
local function contains(list,value)
    for _,v in ipairs(list) do if v==value then return true end end
    return false
end
local function insert_once(list,value)
    if contains(list,value) then return false end
    list[#list+1]=value;return true
end
local function integer(v,fallback,min,max)
    if type(v)~='number' or v~=v or math.abs(v)==math.huge then return fallback end
    return math.max(min or 0,math.min(max or 9007199254740991,math.floor(v)))
end
local function valid_day(value)
    if type(value)~='string' then return false end
    local year,month,day=value:match('^(%d%d%d%d)%-(%d%d)%-(%d%d)$')
    year,month,day=tonumber(year),tonumber(month),tonumber(day)
    if not year or year<1 or month<1 or month>12 or day<1 then return false end
    local days={31,28,31,30,31,30,31,31,30,31,30,31}
    if year%4==0 and (year%100~=0 or year%400==0) then days[2]=29 end
    return day<=days[month]
end
local function clone(value)
    if type(value)~='table' or Json.is_null(value) then return value end
    local output={};for k,v in pairs(value) do output[k]=clone(v) end;return output
end
local escapes={['"']='\\"',['\\']='\\\\',['\b']='\\b',['\f']='\\f',['\n']='\\n',['\r']='\\r',['\t']='\\t'}
local function quote(value)
    return '"'..value:gsub('[%z\1-\31\\"]',function(c) return escapes[c] or string.format('\\u%04x',c:byte()) end)..'"'
end
local function encode(value,active,depth)
    if value==nil or Json.is_null(value) then return 'null' end
    local kind=type(value)
    if kind=='boolean' then return value and 'true' or 'false' end
    if kind=='string' then return quote(value) end
    if kind=='number' then
        assert(value==value and math.abs(value)<math.huge,'Non-finite save number')
        return string.format('%.17g',value)
    end
    assert(kind=='table' and depth<64 and not active[value],'Unsupported or cyclic save value')
    active[value]=true
    local count,max,is_array=0,0,true
    for k in pairs(value) do count=count+1;if type(k)~='number' or k<1 or k%1~=0 then is_array=false else max=math.max(max,k) end end
    is_array=is_array and count>0 and count==max
    local parts={}
    if is_array then
        for i=1,max do parts[i]=encode(value[i],active,depth+1) end
    else
        local keys={};for k in pairs(value) do assert(type(k)=='string','Save object key is not a string');keys[#keys+1]=k end
        table.sort(keys)
        for _,k in ipairs(keys) do parts[#parts+1]=quote(k)..':'..encode(value[k],active,depth+1) end
    end
    active[value]=nil
    return (is_array and '[' or '{')..table.concat(parts,',')..(is_array and ']' or '}')
end
function Progress.encode(value) return encode(value,{},0) end
function Progress.now() return tonumber(tostring(CS.System.DateTimeOffset.UtcNow:ToUnixTimeSeconds())) end
function Progress.day() return CS.System.DateTime.UtcNow:ToString('yyyy-MM-dd') end
function Progress.month() return CS.System.DateTime.UtcNow:ToString('yyyy-MM') end
function Progress.new(host)
    local self=setmetatable({host=host,storage=host.Services.Storage,data=nil,dirty=false,closed=false,read_failed=false,
        persistent=false,status='',results={}},Progress)
    local source,record
    if self.storage and self.storage.IsAvailable then
        local ok,value=pcall(function() return self.storage:Read(KEY) end)
        if ok then record=value else self.read_failed=true;self.status='存档读取失败，本次更改不会覆盖原存档。' end
        if record and record~='' then
            local parsed,value=pcall(Json.decode,record,{max_bytes=1024*1024,max_depth=64})
            if parsed and type(value)=='table' and not Json.is_null(value) and (value.version==nil or (type(value.version)=='number' and value.version<=2)) then source=value
            else self.read_failed=true;self.status='存档格式无法读取，已保留原存档。' end
        end
        self.persistent=not self.read_failed
    else self.status='当前账号存储不可用，本次进度仅在本次会话保留。' end
    local fresh=source==nil
    source=source or {}
    self.data=source
    -- Preserve unknown activity/cloud fields when migrating older records.
    source.version=2
    source.currentLevel=integer(source.currentLevel,1,1)
    source.highestUnlocked=integer(source.highestUnlocked,1,1)
    source.coins=integer(source.coins,0)
    source.startingCoinsGranted=true
    for id in pairs(Progress.prices) do source[id]=integer(source[id],2) end
    for _,id in ipairs({'sound','music','haptics'}) do if type(source[id])~='boolean' then source[id]=true end end
    source.playerName=type(source.playerName)=='string' and source.playerName~='' and source.playerName or '玩家'
    source.profileId=type(source.profileId)=='string' and source.profileId~='' and source.profileId or CS.System.Guid.NewGuid():ToString('N')
    source.homeArea=integer(source.homeArea,0,0,2)
    source.areaId=integer(source.areaId,1,1,#Peripheral.areas)
    source.avatarIndex=integer(source.avatarIndex,0,0,8)
    source.lives=integer(source.lives,5,0,5)
    source.nextLifeAt=integer(source.nextLifeAt,0)
    source.unlimitedLifeUntil=integer(source.unlimitedLifeUntil,0)
    source.lastDailyRewardDay=type(source.lastDailyRewardDay)=='string' and source.lastDailyRewardDay or ''
    source.dailyRewardCount=Signin.count(source)
    source.peripheralVersion=1
    for _,field in ipairs(array_fields) do
        local old=source[field];source[field]={}
        if type(old)=='table' and not Json.is_null(old) then
            for _,v in ipairs(old) do if type(v)=='number' or type(v)=='string' then insert_once(source[field],v) end end
        end
    end
    for _,field in ipairs({'luaData','platformActivities'}) do if type(source[field])~='table' or Json.is_null(source[field]) then source[field]={} end end
    Cadence.normalize(source,#source.completedLevels)
    self.dirty=true
    self:tick()
    self:save()
    return self
end
function Progress:save()
    if self.closed then return false,'存档已关闭' end
    if self.locked then return false,'账号已失效，请重新进入游戏' end
    if not self.dirty then return true end
    if not self.persistent or self.read_failed then return false,self.status end
    if not self.storage.IsAvailable then self.persistent=false;self.status='账号存储已失效，请重新进入游戏。';return false,self.status end
    local ok,error=pcall(function() self.storage:Write(KEY,Progress.encode(self.data)) end)
    if not ok then self.status='进度保存失败，请稍后重试。';return false,tostring(error) end
    self.dirty=false;return true
end
function Progress:changed() self.dirty=true;return self:save() end
function Progress:seen(id) return contains(self.data.learnedFeatures,id) end
function Progress:learn(id) if insert_once(self.data.learnedFeatures,id) then self:changed() end end
function Progress:booster_seen(id) return contains(self.data.boosterLessons,id) end
function Progress:learn_booster(id) if Progress.prices[id] and insert_once(self.data.boosterLessons,id) then self:changed() end end
function Progress:toggle(id)
    if id~='sound' and id~='music' and id~='haptics' then return false end
    self.data[id]=not self.data[id];self:changed();return self.data[id]
end
function Progress:can_spend(id) return not self.locked and Progress.prices[id]~=nil and self.data[id]>0 end
function Progress:gm_unlock_all(last)
    if not(CS.UnityEngine.Application.isEditor or CS.UnityEngine.Debug.isDebugBuild) then return false,'GM 仅限开发版本' end
    if self.closed or self.locked then return false,'账号不可用，请重新进入游戏' end
    self.data.highestUnlocked=math.max(self.data.highestUnlocked,last);return self:changed()
end
function Progress:gm_boosters(amount)
    if not(CS.UnityEngine.Application.isEditor or CS.UnityEngine.Debug.isDebugBuild) then return false,'GM 仅限开发版本' end
    if self.closed or self.locked then return false,'账号不可用，请重新进入游戏' end
    for _,id in ipairs({'undo','shuffle','belt','truck'}) do self.data[id]=amount==0 and 0 or 99 end
    return self:changed()
end
function Progress:spend(id)
    if not self:can_spend(id) then return false,'道具不足，可在每日礼包中领取' end
    self.data[id]=self.data[id]-1;self:event('useboosters',1);self:event('use'..(id=='truck' and 'capacity' or id),1)
    self:changed();return true
end
function Progress:buy(id) return false,'请在每日礼包中领取道具' end
function Progress:reward(rewards)
    if self.locked then return false end
    local map={undobooster='undo',shufflebooster='shuffle',capacitybooster='truck',conveyorcapacitybooster='belt'}
    for kind,amount in tostring(rewards):gmatch('([%w_]+):(%d+)') do
        local count=tonumber(amount)
        if kind=='gold' then -- currency retired
        elseif kind=='life' then -- energy removed
        elseif kind=='block' then local a=self.data.platformActivities;a.blocks=integer(a.blocks,0)+count
        elseif map[kind] then self.data[map[kind]]=self.data[map[kind]]+count end
    end
    self.dirty=true
end
function Progress.reward_label(rewards)
    local names={gold='金币',life='分钟无限生命',undobooster='撤回',shufflebooster='打乱',capacitybooster='加车',conveyorcapacitybooster='扩容',block='任务积分'}
    local parts={};for kind,count in tostring(rewards):gmatch('([%w_]+):(%d+)') do parts[#parts+1]=(names[kind] or kind)..' × '..count end
    return table.concat(parts,'  ')
end
function Progress:tick()
    if self.closed or self.locked then return end
    local p,now=self.data,Progress.now()
    local a=p.platformActivities;local week=math.floor(now/Activity.week_seconds)
    if a.week~=week then a.week=week;a.counts={};a.claimed={};a.blocks=0;a.milestones={};a.loginDays={};self.dirty=true end
    local cycle=math.floor(now/Activity.pass_seconds)
    if a.passCycle~=cycle then a.passCycle=cycle;a.passKeys=0;a.passClaimed={};self.dirty=true end
    for _,field in ipairs({'counts','claimed','milestones','loginDays','passClaimed'}) do
        if type(a[field])~='table' or Json.is_null(a[field]) then a[field]={};self.dirty=true end
    end
    for key,value in pairs(a.counts) do a.counts[key]=integer(value,0) end
    a.blocks=integer(a.blocks,0);a.passKeys=integer(a.passKeys,0)
    if insert_once(a.loginDays,Progress.day()) then a.counts.login=(a.counts.login or 0)+1;self.dirty=true end
    self:save()
end
function Progress:life_label() return self.data.unlimitedLifeUntil>Progress.now() and '∞' or tostring(self.data.lives) end
function Progress:life_detail()
    local p,now=self.data,Progress.now()
    if p.unlimitedLifeUntil<=now and p.lives>=5 then return '生命已满' end
    local remaining=math.max(0,math.ceil((p.unlimitedLifeUntil>now and p.unlimitedLifeUntil or p.nextLifeAt)-now))
    return (p.unlimitedLifeUntil>now and '无限生命剩余 ' or '下一颗生命 ')..string.format('%02d:%02d',remaining//60,remaining%60)
end
function Progress:can_start() return not self.locked end
function Progress:refill() return false,'无需体力，可直接开始关卡' end
function Progress:event(kind,count)
    if self.locked then return end
    local a=self.data.platformActivities;a.counts=a.counts or {};a.counts[kind]=integer(a.counts[kind],0)+integer(count,1);self.dirty=true
end
function Progress:complete(level,kind,won,next_id,run_key,daily_day)
    if self.locked then return 0 end
    local key=tostring(run_key or '')..':'..tostring(kind)..':'..tostring(level.id)..':'..tostring(won)
    if self.results[key] then return 0 end
    self.results[key]=true
    local p,a=self.data,self.data.platformActivities
    -- A daily run keeps its launch date through midnight and retries. Older
    -- callers without that snapshot retain their existing current-day behavior.
    local day=valid_day(daily_day) and daily_day or Progress.day()
    if not won then
        if kind=='daily' then insert_once(p.dailyFailed,day) end
        a.counts.winstreak=0;self:changed();return 0
    end
    if kind=='daily' and not insert_once(p.dailyCompleted,day) then return 0 end
    if kind=='daily' then for i=#p.dailyFailed,1,-1 do if p.dailyFailed[i]==day then table.remove(p.dailyFailed,i) end end end
    if kind~='daily' then p.highestUnlocked=math.max(p.highestUnlocked,level.id,next_id or level.id);p.currentLevel=next_id or level.id;insert_once(p.completedLevels,level.id) end
    local reward=0
    if kind~='daily' then for i=#p.skippedLevels,1,-1 do if p.skippedLevels[i]==level.id then table.remove(p.skippedLevels,i) end end end
    self:event('win',1);self:event('winstreak',1)
    if level.theme=='Hard' then self:event('winhard',1) elseif level.theme=='SuperHard' then self:event('winsuperhard',1) end
    a.passKeys=integer(a.passKeys,0)+(Activity.pass_tokens[level.theme] or Activity.pass_tokens.Default)
    self:changed();return reward
end
function Progress:claim_daily()
    if self.locked then return false,'账号已失效，请重新进入游戏' end
    if self.data.lastDailyRewardDay==Progress.day() then return false,'今日礼包已经领取' end
    local state=Signin.state(self.data,Progress.day());local rewards=Signin.rewards(state.day)
    for _,item in ipairs(rewards) do self.data[item.id]=self.data[item.id]+item.count end
    self.data.dailyRewardCount=state.count+1
    self.data.lastDailyRewardDay=Progress.day();self:changed();return true,Signin.label(rewards)
end
function Progress:daily_count()
    local count=0;local month=Progress.month();for _,day in ipairs(self.data.dailyCompleted) do if day:sub(1,7)==month then count=count+1 end end;return count
end
function Progress:claim_daily_milestone(index)
    local row=Peripheral.daily_milestones[index];if not row then return false,'奖励不存在' end
    local key=Progress.month()..':'..row.days
    if contains(self.data.dailyClaimed,key) then return false,'已领取' end
    if self:daily_count()<row.days then return false,'完成天数不足' end
    self:reward(row.reward);insert_once(self.data.dailyClaimed,key);self:changed();return true,Progress.reward_label(row.reward)
end
function Progress:select_area(index)
    local row=Peripheral.areas[index];if not row then return false,'地区不存在' end
    if self.data.highestUnlocked<row.unlock then return false,'到达第 '..row.unlock..' 关后解锁' end
    self.data.areaId=index;self:changed();return true
end
function Progress:claim_mission(group,index)
    local row=Activity.missions[group] and Activity.missions[group][index];if not row then return false,'任务不存在' end
    local a=self.data.platformActivities;local key=group..':'..index
    if contains(a.claimed,key) then return false,'已领取' end
    if (a.counts.login or 0)<group or (a.counts[row.kind] or 0)<row.goal then return false,'尚未完成任务' end
    self:reward(row.reward);insert_once(a.claimed,key);self:changed();return true,Progress.reward_label(row.reward)
end
function Progress:claim_mission_milestone(index)
    local row=Activity.milestones[index];local a=self.data.platformActivities
    if not row or (a.blocks or 0)<row.goal then return false,'任务积分不足' end
    if contains(a.milestones,index) then return false,'已领取' end
    self:reward(row.reward);insert_once(a.milestones,index);self:changed();return true,Progress.reward_label(row.reward)
end
function Progress:claim_pass(index)
    local row=Activity.pass[index];if not row then return false,'奖励不存在' end
    local a=self.data.platformActivities;local required=0;for i=1,index do required=required+Activity.pass[i].keys end
    if (a.passKeys or 0)<required then return false,'通行证钥匙不足' end
    if contains(a.passClaimed,index) then return false,'已领取' end
    self:reward(row.free);insert_once(a.passClaimed,index);self:changed();return true,Progress.reward_label(row.free)
end
function Progress:skip_level(id)
    if self.locked then return false end
    if not contains(self.data.completedLevels,id) then insert_once(self.data.skippedLevels,id) end
    self:changed();return true
end
function Progress:reset_local()
    if self.locked or self.read_failed or not self.persistent then return false,'当前存储不可用，未清除任何数据' end
    local ok=pcall(function() self.storage:Write(KEY,'') end)
    if not ok then return false,'无法清除进度，请稍后重试' end
    local fresh=Progress.new(self.host)
    self.data=fresh.data;self.results={};self.dirty=true;fresh.closed=true
    return self:save()
end
function Progress:snapshot() return clone(self.data) end
function Progress:dispose() if self.closed then return end;self:save();self.closed=true;self.host=nil;self.storage=nil end
Progress.contains=contains
return Progress
