-- APK 4.6.0: ShuffleGroup MoveNext 0x23D9B98, ShuffleSphere 0x23DA6B0,
-- Capacity 0x23D09DC, capacity icon 0x23D17A4, Undo 0x23DC940.
-- Reconstruct the motion in the module clock; never start Unity coroutines.
local N=require('platform.native')
local U,V,Q=N.U,N.V,N.Q
local B={};B.__index=B
function B.new(p) return setmetatable({p=p},B) end
function B:busy() return self.active~=nil end
function B:owns_truck(index) return self.active and self.active.trucks[index]==true end
function B:begin(kind,duration,pose,finish,trucks,objects,start)
    assert(not self.active,'A booster animation is already running')
    self.active={kind=kind,time=0,duration=duration,pose=pose,finish=finish,trucks=trucks or {},objects=objects or {},start=start,started=start==nil}
    self.p.context.booster_busy=true
    self.p.accumulator=0
    for _,view in pairs(self.p.belt) do
        view.render_from_position=view.render_to_position;view.render_from_rotation=view.render_to_rotation
    end
    -- Select first, then present the accepted use. Selecting a shuffle target
    -- alone neither consumes the item nor blocks the board with this overlay.
    local target,index
    for candidate in pairs(self.active.trucks) do if not index or candidate<index then index=candidate end end
    if index and self.p.trucks[index] then
        local truck=self.p.trucks[index]
        target=self.p.camera:WorldToScreenPoint(truck.root.position+V.up*.5)
    end
    self.p.ui:begin_booster_effect(kind,target)
    if not start then pose(0) end
end
function B:cancel(settle)
    local a=self.active;if not a then return end
    self.active=nil;self.p.context.booster_busy=false
    if self.p.ui then self.p.ui:end_booster_effect() end
    for _,go in ipairs(a.objects) do N.destroy(go) end
    -- Appearance caches must not retain the renderers of destroyed shuffle copies.
    if #a.objects>0 then self.p.appearances={} end
    if settle and a.started and a.finish then a.finish() end
end
function B:update(dt)
    local a=self.active;if not a then return end
    local intro=.97
    local next_time=a.time+dt
    if next_time>=intro and not a.started then
        if a.start and a.start()==false then self:cancel(false);return end
        a.started=true
    end
    local total=intro+math.max(.15,a.duration)
    a.time=math.min(total,next_time)
    -- Keep pieces in their starting pose until the icon reaches its target.
    -- This clock is independent of menu pause, which must not stall the effect.
    if a.time>=intro then a.pose(math.min(a.duration,a.time-intro)) end
    self.p.ui:pose_booster_effect(a.time)
    if a.time>=total then self:cancel(true) end
end

-- Move a complete group (and bonded neighbouring groups) with the same offset.
-- The original group path lifts 2 units, crosses .75 sideways, travels at
-- .16 seconds per unit, aligns, then lands: .2/.3/travel/.15/.1 seconds.
local function shuffle_path(from,to,up,time)
    local delta=to-from
    local side=delta.sqrMagnitude>.000001 and V.Cross(delta.normalized,up.normalized)*.75 or V.zero
    local travel=math.max(.05,delta.magnitude*.16)
    if time<.2 then return V.Lerp(from,from+up,N.sine(time/.2)) end
    time=time-.2
    if time<.3 then return V.Lerp(from+up,from+up+side,N.sine(time/.3)) end
    time=time-.3
    if time<travel then return V.Lerp(from+up+side,to+up+side,N.sine(time/travel)) end
    time=time-travel
    if time<.15 then return V.Lerp(to+up+side,to+up,N.sine(time/.15)) end
    return V.Lerp(to+up,to,N.sine((time-.15)/.1))
end
function B:shuffle(index)
    local p=self.p;local rules=p.context.rules;local d=rules.d;local t=p.trucks[index]
    if self:busy() then return false,'请等待道具动画完成' end
    if not p.progress:can_spend('shuffle') then return false,'打乱道具不足，可在每日礼包中领取' end
    if not t or not rules:can_shuffle(index) then return false,'请选择可以打乱的货车' end
    p.shuffle_selected=false
    local moves,objects={},{};local up=t.root.up*2
    local function start()
        -- The original cargo, including hidden covers and complete groups,
        -- stays untouched throughout the icon flight. Prepare motion only now.
        if not p.progress:can_spend('shuffle') then p.ui:toast('打乱道具不足，可在每日礼包中领取');return false end
        local old={}
        for slot=0,t.state.cargo.Count-1 do
            local id=t.state.cargo[slot];local piece=d.pieces[id]
            old[#old+1]={id=id,slot=slot,color=piece.color,hidden=piece.hidden}
        end
        local random={Next=function(_,a,b) return math.random(a,b-1) end}
        if not rules:shuffle(index,random) then p.ui:toast('请选择可以打乱的货车');return false end
        -- A shuffle cannot be undone, including by an earlier release snapshot.
        -- Failed selection and cancellation before arrival keep history intact.
        p.history={};p.context.ui_state.undo_depth=0
        p.progress:spend('shuffle')
        local slots={};for slot=0,t.state.cargo.Count-1 do slots[t.state.cargo[slot]]=slot end
        local duration=0
        local function add(mesh,scale,from,to,piece)
            local go,r=N.model('Shuffle unit',mesh,p.material,p.board)
            go.transform.localScale=scale;go.transform:SetPositionAndRotation(from,t.rotation)
            p:appearance(r,piece);objects[#objects+1]=go
            moves[#moves+1]={tr=go.transform,from=from,to=to}
            duration=math.max(duration,.75+math.max(.05,(to-from).magnitude*.16))
        end
        local i=1
        while i<=#old do
            local first=old[i];local count=math.min(p.group_size,#old-i+1)
            local whole=count==p.group_size and slots[first.id]%p.group_size==0
            for j=0,count-1 do
                local member=old[i+j]
                if member.color~=first.color or member.hidden~=first.hidden or slots[member.id]~=slots[first.id]+j then whole=false end
                local view=p.pieces[member.id];if view then p:show_piece(view,false) end
            end
            if whole then
                local from=t.cargo:TransformPoint(t.front-V.forward*math.floor(first.slot/p.group_size))
                local to=t.cargo:TransformPoint(t.front-V.forward*math.floor(slots[first.id]/p.group_size))
                add(p.cargo,V.one,from,to,first)
            else
                for j=0,count-1 do local member=old[i+j]
                    add(p.cell_mesh,p.cell,p:cargo_position(index,member.slot),p:cargo_position(index,slots[member.id]),member)
                end
            end
            i=i+count
        end
        t.cargo.gameObject:SetActive(false)
        self.active.duration=duration
        return true
    end
    self:begin('shuffle',.15,function(time)
        for _,move in ipairs(moves) do move.tr.position=shuffle_path(move.from,move.to,up,time) end
    end,function()
        p:mark_dirty(index);p:refresh_cargo(true);p:play('receive')
    end,{[index]=true},objects,start)
    p:play('click');return true
end

function B:undo()
    local p=self.p
    if self:busy() then return false,'请等待道具动画完成' end
    if p.entering or #p.history==0 then return false,'没有可以撤销的操作' end
    local snapshot=p.history[#p.history];local sources={}
    for id,view in pairs(p.visible) do
        sources[id]={position=p.belt[id] and view.body.position or view.tr.position,rotation=view.tr.rotation}
    end
    -- Whole blocks have a single renderer, so their fragments are absent from
    -- p.visible. Still capture moved cargo when undoing a previous shuffle.
    local destinations={}
    for index=0,snapshot.state.carriers.Length-1 do
        local cargo=snapshot.state.carriers[index].cargo
        for slot=0,cargo.Count-1 do destinations[cargo[slot]]={index=index,slot=slot} end
    end
    for index,truck in pairs(p.trucks) do
        local cargo=p.context.rules.d.carriers[index].cargo
        for slot=0,cargo.Count-1 do
            local id=cargo[slot];local target=destinations[id]
            if not sources[id] and target and (target.index~=index or target.slot~=slot) then
                sources[id]={position=p:cargo_position(index,slot),rotation=truck.rotation}
            end
        end
    end
    if not p:restore_undo_view() then return false end
    local d=p.context.rules.d;local targets,groups,trucks={},{},{}
    for index,t in pairs(p.trucks) do
        local cargo=d.carriers[index].cargo
        for slot=0,cargo.Count-1 do
            local id=cargo[slot];local source=sources[id]
            if source and (source.position-p:cargo_position(index,slot)).sqrMagnitude>.0001 then
                groups[index]=groups[index] or {};groups[index][math.floor(slot/p.group_size)]=true
            end
        end
        if groups[index] then
            trucks[index]=true
            for slot=0,cargo.Count-1 do if groups[index][math.floor(slot/p.group_size)] then
                local id=cargo[slot]
                targets[id]={position=p:cargo_position(index,slot),rotation=t.rotation}
                t.groups[math.floor(slot/p.group_size)].go:SetActive(false)
            end end
        end
    end
    for id,record in pairs(snapshot.pieces) do
        targets[id]={position=record.position,rotation=record.rotation,record=record}
    end
    for id,target in pairs(targets) do
        local view=p:piece(id);local source=sources[id]
        target.view=view;target.from=source and source.position or target.position;target.from_rotation=source and source.rotation or target.rotation
        p:animated(view,target.from,target.from_rotation,target.record~=nil)
    end
    self:begin('undo',.45,function(time)
        local t=N.sine(time/.45)
        for _,target in pairs(targets) do
            local position=V.Lerp(target.from,target.position,t)+V.up*(math.sin(math.pi*t)*.8)
            p:animated(target.view,position,Q.Slerp(target.from_rotation,target.rotation,t),target.record~=nil)
        end
    end,function()
        for _,target in pairs(targets) do
            local record=target.record;local view=target.view
            if record then
                p:start_physics(view,record.position,record.rotation,record.velocity);view.body.angularVelocity=record.angular
            else p:show_piece(view,false) end
        end
        p.accumulator=0;p:refresh_cargo(true);p:play('receive')
    end,trucks)
    return true
end
function B:expand()
    local p=self.p
    if self:busy() then return false,'请等待道具动画完成' end
    if p.entering then return false,'请等待关卡入场' end
    if p.context.rules.d.status==2 and p.context.rules.d.failReason==2 then return false,'时间已用尽，请撤回或重新挑战' end
    -- Commit once at acceptance. HUD keeps its old value until the icon arrives;
    -- no gameplay step can use the extra capacity during the transition.
    p.context.rules:expand_belt();p.result_at=nil;p.result_done=false;p.context.ui_state.result_ready=false
    p:preserve_booster_history()
    p.context:set_menu_paused(false)
    self:begin('belt',.75,function(time)
        if time>=.5 then p.context.ui_state.total=math.floor(p.context.rules.d.beltCapacity/p.group_size) end
    end,function()
        p.context.ui_state.total=math.floor(p.context.rules.d.beltCapacity/p.group_size)
        p:refresh_cargo(true);p:play('receive')
    end)
    p:play('click');return true
end
function B:add_truck()
    local p=self.p
    if self:busy() then return false,'请等待道具动画完成' end
    if p.entering then return false,'请等待关卡入场' end
    local placed={};for _,t in pairs(p.trucks) do placed[t.data.id]=true end
    local spare
    for i=0,p.context.level.spares.Length-1 do
        local data=p.context.level.spares[i];if not placed[data.id] then spare=data;break end
    end
    if not spare then return false,'本关没有可以加车的空位' end
    local state=p.context.rules:add_carrier_data(spare);p:create_truck(spare,state.index)
    p:preserve_booster_history()
    p.mechanics:add_carrier(state.index);p:refresh_cargo(true);p:frame()
    local truck=p.trucks[state.index];local origin=truck.origin;local center=N.bounds(truck.root.gameObject).center
    self:begin('truck',.4,function(time)
        local t=N.clamp(time/.4,0,1)-1;local scale=1+2.70158*t*t*t+1.70158*t*t
        truck.root.localScale=V.one*math.max(0,scale);truck.root.position=center+(origin-center)*scale
    end,function()
        truck.root.localScale=V.one;truck.root.position=origin
        p:refresh_cargo(true);p:frame();p.mechanics:particle('spawn',origin,Q.identity);p:play('receive')
    end,{[state.index]=true})
    p:play('click');return true
end
return B
