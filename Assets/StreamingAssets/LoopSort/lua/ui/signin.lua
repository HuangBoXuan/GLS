local Signin=require('game.signin')
local View={}
-- Adapters retain the two hosts' native image geometry and button lifetimes.
function View.draw(a,root,state,claim,close,busy)
    local view={cards={}}
    a.label(root,'Signin subtitle','累计签到 7 天，天天领道具！',34,{.04,.851,.96,.901},0x80513C)
    for day=1,7 do
        local wide=day==7
        local x=wide and .035 or (.035+((day-1)%2)*.475)
        local y=wide and .130 or (.675-math.floor((day-1)/2)*.185)
        local card,surface=a.card(root,'Signin day '..day,{x,y,wide and .965 or x+.455,y+(wide and .165 or .175)})
        local title=a.label(card,'Day','第 '..day..' 天',34,{.06,.81,.94,.99},0x704332)
        local status=a.label(card,'Status','',27,{.06,.67,.94,.83},0x9C8065)
        view.cards[day]={surface=surface,title=title,status=status}
        local rewards=Signin.rewards(day)
        for index,item in ipairs(rewards) do
            local left,bottom,width,height
            if wide then
                width,height=.23,.58;left=.04+(index-1)*.23;bottom=.05
            elseif #rewards<=2 then
                width,height=.45,.58;left=.04+(index-1)*.47;bottom=.05
            else
                width,height=.45,.30;left=.04+((index-1)%2)*.47;bottom=.36-math.floor((index-1)/2)*.32
            end
            local iconWidth=width*.52
            local inset=item.id=='truck' and .15 or 0
            a.art(card,'Reward '..item.id,item.id,{left+iconWidth*inset,bottom+height*inset,
                left+iconWidth*(1-inset),bottom+height*(1-inset)},true)
            a.label(card,'Quantity '..item.id,'× '..item.count,34,{left+width*.50,bottom,left+width*1.02,bottom+height},0x80513C)
        end
    end
    view.button,view.buttonLabel=a.button(root,'Signin claim','领取今日奖励',{.18,.035,.82,.113},function()
        if view.state.claimed then close() else claim() end
    end,not busy)
    function view.update(nextState,nextBusy)
        view.state=nextState
        for day,card in ipairs(view.cards) do
            local ready=day==nextState.day and not nextState.claimed
            local claimed=day<=nextState.completed
            local status=claimed and '✓ 已领取' or ready and '今日可领' or '待签到'
            if card.status.text~=status then card.status.text=status end
            a.paint(card.surface,claimed and 0x7DA9EB or ready and 0xFFAE3F or 0xD9BD94)
            local ink=(claimed or ready) and 0xFFFFFF or 0x794820
            a.paint(card.title,ink)
            a.paint(card.status,ink)
        end
        view.buttonLabel.text=nextState.claimed and '继续游戏' or '领取今日奖励'
        view.button.interactable=not nextBusy
    end
    view.update(state,busy)
    return view
end
return View
