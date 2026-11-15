-- One reward schedule for both presentation/runtime paths.
local S={ids={'undo','belt','shuffle','truck'},names={undo='撤回',belt='扩容',shuffle='打乱',truck='加车'}}
S.days={
    {undo=2,belt=2,shuffle=2,truck=2},
    {undo=2,belt=2},
    {shuffle=2,truck=2},
    {undo=1,belt=1,shuffle=1,truck=1},
    {undo=2,shuffle=2},
    {belt=2,truck=2},
    {undo=2,belt=2,shuffle=2,truck=2},
}
function S.count(progress)
    local count=tonumber(progress.dailyRewardCount) or 0
    if count~=count or count==math.huge or count==-math.huge then count=0 end
    count=math.max(0,math.floor(count))
    -- Old saves only recorded the last claim. Preserve that claim without
    -- inventing an unknown history or allowing a second reward today.
    if count==0 and progress.lastDailyRewardDay and progress.lastDailyRewardDay~='' then count=1 end
    return count
end
function S.state(progress,today)
    local count=S.count(progress);local claimed=progress.lastDailyRewardDay==today
    local day=claimed and ((math.max(1,count)-1)%7+1) or (count%7+1)
    return {day=day,claimed=claimed,completed=claimed and day or count%7,count=count}
end
function S.rewards(day,multiplier)
    local rewards={};local row=assert(S.days[day],'Invalid sign-in day')
    for _,id in ipairs(S.ids) do
        if row[id] then rewards[#rewards+1]={id=id,count=row[id]*(multiplier or 1)} end
    end
    return rewards
end
function S.label(rewards)
    local text={}
    for _,item in ipairs(rewards) do text[#text+1]=S.names[item.id]..' × '..item.count end
    return table.concat(text,'  ')
end
return S
