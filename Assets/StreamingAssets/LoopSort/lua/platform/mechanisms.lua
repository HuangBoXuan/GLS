-- Original mechanism timings and mesh channels, driven by the module's own clock.
-- All scene objects belong to this instance; no MonoBehaviour or global coroutine is added.
local N = require('platform.native')
local U,V,Q = N.U,N.V,N.Q
local M = {}; M.__index=M
local function clamp(x) return N.clamp(x,0,1) end
local function visible(go,on) if N.alive(go) and go.activeSelf~=on then go:SetActive(on) end end
local function contains(list,value) return list and list:Contains(value) end
local function morph(root,path)
    local t=root:Find(path)
    return t and t:GetComponent(typeof(U.SkinnedMeshRenderer)) or nil
end
local function weight(r,i,value) if r and i<r.sharedMesh.blendShapeCount then r:SetBlendShapeWeight(i,value) end end
local function reset_morph(r)
    if r then for i=0,r.sharedMesh.blendShapeCount-1 do r:SetBlendShapeWeight(i,0) end;r.enabled=true end
end
local function curve(keys,time)
    local i=1
    while i<#keys and keys[i+1].time<=time do i=i+1 end
    local key=keys[i];local dt=math.max(0,time-key.time);local c=key.coefficients
    return ((c[1]*dt+c[2])*dt+c[3])*dt+c[4]
end
local function out_back(t) t=t-1;return 1+2.70158*t*t*t+1.70158*t*t end
local function in_out_back(t)
    local c=1.70158*1.525;t=t*2
    if t<1 then return .5*(t*t*((c+1)*t-c)) end
    t=t-2;return .5*(t*t*((c+1)*t+c)+2)
end
local function gate_art(g,progress)
    if not g.art then return end
    g.art.localScale=V(g.artSize.x,g.artSize.y*(1-progress),g.artSize.z)
    g.art.localPosition=g.artPosition+V.forward*(g.artSize.y*progress*.5)
    visible(g.art.gameObject,progress<.999)
end
local function tile(t,x,y)
    if not t then return end
    local r=t:GetComponent(typeof(U.Renderer));if not r then return end
    local b=U.MaterialPropertyBlock();b:SetVector('_MainTex_ST',U.Vector4(x,y,0,0));r:SetPropertyBlock(b)
end

function M.new(p)
    local self=setmetatable({p=p,assets=p.assets,features={},gates={},motions={},particles={},owned={},closed=false,entrance_hidden=true},M)
    self.catalog={}
    for _,v in ipairs(self.assets:data('feature-visuals').visuals) do self.catalog[v.id]=v end
    for _,clip in ipairs(self.assets:animation('curtain-clips').clips) do if clip.id==558 then self.curtain_clip=clip end end
    return self
end
function M:own(go) self.owned[#self.owned+1]=go;return go end
function M:create(id,parent)
    local go=self:own(N.clone(self.assets:prefab(id),parent))
    if id=='carpet' or id=='carpet-divider' then self.assets:style_volcanic(go) end
    return go
end
function M:animate(owner,key,duration,pose,finish,delay)
    self:stop(owner,key)
    self.motions[#self.motions+1]={owner=owner,key=key,time=-(delay or 0),duration=math.max(.0001,duration),pose=pose,finish=finish}
end
function M:stop(owner,key)
    for i=#self.motions,1,-1 do
        local m=self.motions[i]
        if m.owner==owner and (not key or m.key==key) then table.remove(self.motions,i) end
    end
end
function M:clip(go,id,finish)
    local clip=self.assets:animation(id);local targets={}
    for i,binding in ipairs(clip.bindings) do targets[i]=binding.path=='' and go.transform or go.transform:Find(binding.path) end
    local function pose(t)
        local time=t*clip.duration
        for i,binding in ipairs(clip.bindings) do
            local target=targets[i]
            if target then
                local c=binding.curves
                local value=V(curve(c[1].keys,time),curve(c[2].keys,time),curve(c[3].keys,time))
                if binding.attribute==1 then target.localPosition=value
                elseif binding.attribute==3 then target.localScale=value
                elseif binding.attribute==4 then target.localEulerAngles=value
                elseif binding.attribute==2 then target.localRotation=Q(value.x,value.y,value.z,curve(c[4].keys,time)) end
            end
        end
    end
    pose(0);self:animate(go,id,clip.duration,pose,finish)
end
function M:particle(id,position,rotation)
    local go=self:create('particle-'..id,self.p.board);go.transform:SetPositionAndRotation(position,rotation or Q.identity)
    local systems=go:GetComponentsInChildren(typeof(U.ParticleSystem),true)
    local item={go=go,systems=systems,paused={},stopped=false};self.particles[#self.particles+1]=item
    for i=0,systems.Length-1 do systems[i]:Play(false) end
    if self.paused then for i=0,systems.Length-1 do item.paused[i]=systems[i].isPlaying;systems[i]:Pause(false) end end
    return item
end
function M:stop_particle(item)
    if not item or item.stopped then return end;item.stopped=true
    for i=0,item.systems.Length-1 do
        local s=item.systems[i];local main=s.main;main.loop=false;s:Stop(false,U.ParticleSystemStopBehavior.StopEmitting)
    end
end
function M:key(parent,color,position)
    local wrapper=self:own(N.node('Collected key',parent))
    local go=self:create('key',wrapper.transform);local root=go.transform
    local renderers=go:GetComponentsInChildren(typeof(U.MeshRenderer),true)
    local first=true;local min,max=V.zero,V.zero
    for i=0,renderers.Length-1 do
        local r=renderers[i];local b=r.localBounds
        for x=0,1 do for y=0,1 do for z=0,1 do
            local corner=V(x==0 and b.min.x or b.max.x,y==0 and b.min.y or b.max.y,z==0 and b.min.z or b.max.z)
            local p=root:InverseTransformPoint(r.transform:TransformPoint(corner))
            if first then min,max,first=p,p,false else min=V.Min(min,p);max=V.Max(max,p) end
        end end end
    end
    local size=max-min;local scale=1.18/math.max(.01,math.max(size.x,size.z))
    root.localScale=V.one*scale;root.localPosition=(position or V.zero)-(max+min)*(.5*scale)
    N.tint(go,self.assets:color(color))
    local filters=go:GetComponentsInChildren(typeof(U.MeshFilter),true)
    for i=0,(self.assets.index.materials['volcano-source-hidden'] and -1 or filters.Length-1) do
        local f=filters[i];local edge,r=N.model('Key black outline',f.sharedMesh,self.assets:material('keyOutline'),f.transform)
        local materials=N.array(U.Material,f.sharedMesh.subMeshCount)
        for j=0,materials.Length-1 do materials[j]=self.assets:material('keyOutline') end
        r.sharedMaterials=materials
    end
    return wrapper
end
function M:load()
    local d=self.p.context.rules.d
    for i=0,d.carriers.Count-1 do self:add_carrier(i) end
    local groups={}
    for i=0,d.carriers.Count-1 do
        local placement=self.p.trucks[i].data
        for _,kind in ipairs({'lock','rope','carpet'}) do
            -- Initial rules can collect an exposed key before the first frame.
            -- Keep original membership so its door and unlock animation still exist.
            local list=kind=='lock' and placement.lockColors or kind=='rope' and placement.ropeLocks or placement.carpetLocks
            for j=0,list.Length-1 do
                local value=list[j];local id=kind..':'..tostring(value)
                if not groups[id] then groups[id]={kind=kind,value=value,members={}} end
                local members=groups[id].members;members[#members+1]=i
            end
        end
    end
    local ids={};for id in pairs(groups) do ids[#ids+1]=id end;table.sort(ids)
    for _,id in ipairs(ids) do self:add_gate(groups[id]) end
    self:refresh(true)
end
function M:add_carrier(index)
    local truck=self.p.trucks[index];local state=self.p.context.rules.d.carriers[index];local root=truck.root
    local f={index=index,truck=truck,keys={},keyColors={},screws={},covers={},coverSizes={},counts={},count=state.cargo.Count,
        frozen=state.Frozen,packed=state.packed,generatorPhase=1,moving=false,awaiting=false}
    self.features[index]=f
    f.visuals=N.node('Carrier mechanisms',root);visible(f.visuals,not self.entrance_hidden)
    local visuals=f.visuals.transform
    if state.generator then
        f.generator=self:create('generator',visuals)
        local vertical_offset=self.p.presentation.generatorVisualOffset or 0
        f.generator.transform.localPosition=f.generator.transform.localPosition+V.up*vertical_offset
        local nodes=f.generator:GetComponentsInChildren(typeof(U.Transform),true)
        for i=0,nodes.Length-1 do
            local t=nodes[i]
            if t.name=='Cover1' or t.name=='Cover2' then f.covers[#f.covers+1]=t;f.coverSizes[#f.coverSizes+1]=t.localScale end
        end
        local template=self.catalog.generator;f.generatorCenter=truck.front
        truck.outlet=root:TransformPoint(N.vec(template.outlet)+V.up*vertical_offset)
        local pivot=f.generator.transform:Find('Pivot')
        local labels=self:own(N.node('Generator base counts',pivot));labels.transform.localPosition=V(0,0,.491);labels.transform.localRotation=Q.Euler(90,0,0)
        local tilt=Q.Euler(-30,0,0)
        for i=1,2 do
            -- World-space UGUI is part of the platform's retained native API;
            -- TextMesh would require an extra TextRenderingModule reflection root.
            local go=U.GameObject('Remaining groups '..i,typeof(U.RectTransform));go.layer=N.layer
            local rect=go:GetComponent(typeof(U.RectTransform));rect:SetParent(labels.transform,false)
            local canvas=go:AddComponent(typeof(U.Canvas));canvas.renderMode=U.RenderMode.WorldSpace;canvas.worldCamera=self.p.camera
            rect.anchorMin=U.Vector2(.5,.5);rect.anchorMax=U.Vector2(.5,.5);rect.pivot=U.Vector2(.5,.5);rect.sizeDelta=U.Vector2(200,100)
            local rotation=i==1 and tilt or Q.Euler(0,0,180)*tilt
            rect.localRotation=rotation;rect.localPosition=(i==1 and V(0,-.24,-.885) or V(0,1.321,-.898))+rotation*V.back*.01
            rect.localScale=V.one*.005
            local text=go:AddComponent(typeof(U.UI.Text));text.alignment=U.TextAnchor.MiddleCenter
            text.fontSize=90;text.color=U.Color.black;text.font=self.assets:font();text.supportRichText=false;text.raycastTarget=false
            text.horizontalOverflow=U.HorizontalWrapMode.Overflow;text.verticalOverflow=U.VerticalWrapMode.Overflow;f.counts[i]=text
        end
    end
    if state.barrier then
        f.barrier=self:create('barrier',visuals);f.barrierModel=f.barrier.transform:Find('Pivot/Model Rock') or f.barrier.transform
        f.barrierPosition=f.barrierModel.localPosition
    end
    if state.iceRequires.Length>0 then f.ice=self:create('ice',visuals) end
    if state.curtainColor>0 then
        local go=self:own(N.node('Colored curtain',visuals));local r=go:AddComponent(typeof(U.SkinnedMeshRenderer))
        r.sharedMesh=self.assets:mesh(395);r.sharedMaterial=self.assets:surface(state.curtainColor);r.updateWhenOffscreen=true;r.localBounds=r.sharedMesh.bounds
        go.transform.localPosition=V(0,.181,-2.7);go.transform.localRotation=Q.Euler(-90,0,0)
        f.curtain={go=go,renderer=r,open=state.curtainOpen,moving=false}
        if self.assets.index.prefabs['volcano-lid'] then
            local lid=self:create('volcano-lid',visuals);N.tint(lid,self.assets:color(state.curtainColor))
            f.curtain.hinge=lid.transform:Find('Lid hinge')
            if not f.curtain.hinge then
                f.curtain.lid=lid.transform:Find('Volcano art');f.curtain.lidSize=f.curtain.lid.localScale
            end
            r.sharedMaterial=self.assets:material('volcano-source-hidden')
        end
        self:sample_curtain(f.curtain,state.curtainOpen and self.curtain_clip.duration or 0)
    end
    f.top=morph(root,'Pivot/TopPart1');f.rear=morph(root,'Pivot/TopPart2V2')
    self:back(f,true,true)
    local check=self:own(N.node('Completed check',root));check.transform.localPosition=V(0,3.1,(state.capacityGroups>4 and ((state.capacityGroups-4)*.5-.15) or -.15))
    local r=check:AddComponent(typeof(U.SpriteRenderer));r.sprite=assert(self.assets:sprite('check'));r.sortingOrder=0
    check.transform.localScale=V.one*(1.1/r.sprite.bounds.size.x);f.check=check;f.checkSize=check.transform.localScale;f.checkAngle=0
    visible(check,state.packed);visible(f.ice,state.Frozen)
end
function M:sample_curtain(c,time)
    local curves=self.curtain_clip.curves
    c.go.transform.localScale=V(curve(curves[1],time),curve(curves[2],time),curve(curves[3],time))
    weight(c.renderer,0,curve(curves[4],time));weight(c.renderer,1,curve(curves[5],time));c.renderer.enabled=curve(curves[6],time)>.5
    local progress=clamp(time/self.curtain_clip.duration)
    if c.hinge then
        local eased=progress*progress*(3-2*progress)
        c.hinge.localRotation=Q.Euler(0,0,90*eased)
        visible(c.hinge.gameObject,progress<1)
    elseif c.lid then
        visible(c.lid.gameObject,progress<.999)
        c.lid.localScale=V(c.lidSize.x,c.lidSize.y*(1-progress),c.lidSize.z)
        c.lid.localPosition=V(0,2.65,-.2+c.lidSize.y*progress*.5)
    end
end
function M:back(f,open,immediate,on_complete)
    if not f.top or not f.rear then if on_complete then on_complete() end;return end
    local owner=f.truck.root.gameObject;self:stop(owner,'truck-back');reset_morph(f.top);reset_morph(f.rear)
    visible(f.top.gameObject,true);visible(f.rear.gameObject,true)
    if immediate then weight(f.top,0,100);weight(f.rear,0,100);visible(f.top.gameObject,not open);visible(f.rear.gameObject,not open);return end
    if open then
        weight(f.top,0,100);weight(f.rear,0,100);weight(f.rear,2,100)
        self:animate(owner,'truck-back',.85,function(t)
            local time=t*.85;weight(f.rear,0,100*(1-clamp(time/.3)))
            weight(f.rear,2,time<.2 and 100*clamp(time/.2) or 100*(1-clamp((time-.2)/.05)))
            weight(f.top,0,100*(1-clamp((time-.25)/.6)))
        end,function() visible(f.top.gameObject,false);visible(f.rear.gameObject,false) end)
    else
        visible(f.rear.gameObject,false)
        self:animate(owner,'truck-back',.95,function(t)
            local time=t*.95;weight(f.top,0,100*clamp(time/.6))
            if time>=.6 then
                visible(f.rear.gameObject,true);weight(f.rear,2,time<.65 and 100*clamp((time-.6)/.05) or 100*(1-clamp((time-.65)/.3)))
                weight(f.rear,0,100*clamp((time-.65)/.2))
            end
        end,function()
            weight(f.top,0,100);weight(f.rear,0,100);weight(f.rear,2,0)
            if on_complete then on_complete() end
        end)
    end
end
function M:pivot(index) return self.p.trucks[index].root:TransformPoint(self.p.pivot or V.zero) end
function M:add_gate(g)
    g.root=self:own(N.node('Group '..g.kind..' '..tostring(g.value),self.p.board)).transform
    g.model=self:create(g.kind,g.root).transform;g.layout=assert(self.catalog[g.kind].layout)
    g.left=g.model:Find('Left');g.right=g.model:Find('Right');g.width=g.model:Find(g.kind=='lock' and 'Center' or 'Scale')
    g.door=g.model:Find('Door');g.padlock=g.model:Find('Lock Pivot');g.roll=g.model:Find('Right/Roll');g.dividers={};g.revealing={};g.progress=1;g.closed=true
    g.art=g.door and g.door:Find('Volcano art')
    if g.art then g.artSize=g.art.localScale;g.artPosition=g.art.localPosition end
    if g.kind=='carpet' then
        g.leftRoll=g.left:Find('Volcano art')
        table.sort(g.members,function(a,b) return (self.p.trucks[a].data.carpetOrder or 0)<(self.p.trucks[b].data.carpetOrder or 0) end)
        if not g.leftRoll then for i=1,#g.members-1 do g.dividers[i]=self:create('carpet-divider',g.model).transform end end
        self:carpet_span(g,g.members,true)
    else
        local axis=self.p.trucks[g.members[1]].root.right
        table.sort(g.members,function(a,b) return V.Dot(self:pivot(a),axis)<V.Dot(self:pivot(b),axis) end)
        local a,b=self:pivot(g.members[1]),self:pivot(g.members[#g.members]);local truck=self.p.trucks[g.members[1]].root
        g.root:SetPositionAndRotation((a+b)*.5,truck.rotation);g.left.position=a;g.right.position=b
        local distance=V.Distance(a,b);g.width.localScale=V(distance+(g.kind=='lock' and g.layout.centerScaleOffset or g.layout.widthOffset),1,1)
        if g.kind=='lock' then
            g.door.localScale=V(distance+g.layout.doorScaleOffset,1,1);g.root.position=g.root.position+g.root.forward*g.layout.forwardOffset
            if V.Angle(truck.forward,V.forward)>=91 then g.root:Rotate(0,180,0,U.Space.Self) end
            local body=g.model:Find('Lock Pivot/Lock'):GetComponent(typeof(U.MeshRenderer));local tint=U.MaterialPropertyBlock();tint:SetColor('_Color',self.assets:color(g.value));body:SetPropertyBlock(tint,0)
            local art=g.model:Find('Lock Pivot/Lock/Volcano art')
            if art then art:GetComponent(typeof(U.MeshRenderer)):SetPropertyBlock(tint) end
            g.front=morph(g.door,'BlendBottom');g.top=morph(g.door,'BlendTop')
        else
            tile(g.model:Find('Scale/RopeMid'),1,#g.members+1)
            local chain=g.width:Find('Volcano art')
            if chain and self.assets.index.materials['volcano-surface-iron'] then
                local low,high
                for _,index in ipairs(g.members) do
                    local t=self.p.trucks[index];local cart=t.root:Find('Volcano art')
                    local b=cart and cart:GetComponent(typeof(U.Renderer)).bounds or t.collider.bounds
                    for x=0,1 do for y=0,1 do for z=0,1 do
                        local p=g.model:InverseTransformPoint(V(x==0 and b.min.x or b.max.x,y==0 and b.min.y or b.max.y,z==0 and b.min.z or b.max.z))
                        low=low and V.Min(low,p) or p;high=high and V.Max(high,p) or p
                    end end end
                end
                local gap=self.assets.index.materials['volcano-surface-chain-metal'] and .38 or .86
                local width,depth=high.x-low.x+gap,high.z-low.z+gap
                chain:SetParent(g.model,false);chain.localPosition=(low+high)*.5+V.up*.22
                chain.localRotation=Q.Euler(90,0,0);chain.localScale=V(width,depth,1);chain:SetParent(g.width,true)
                chain:GetComponent(typeof(U.MeshFilter)).sharedMesh=self.assets:chain_mesh(width,depth)
            end
        end
    end
    g.closedBounds=N.bounds(g.root.gameObject)
    visible(g.model.gameObject,not self.entrance_hidden)
    self.gates[#self.gates+1]=g
end
function M:bounds()
    local result
    for _,g in ipairs(self.gates) do
        if not result then result=U.Bounds(g.closedBounds.center,g.closedBounds.size) else result:Encapsulate(g.closedBounds) end
    end
    return result
end
function M:carpet_span(g,covered,immediate)
    g.covered=#covered
    for i,t in ipairs(g.dividers) do visible(t.gameObject,i<#covered) end
    local first,last=covered[1],covered[#covered];local carrier=self.p.trucks[first].root
    local reversed=V.Angle(carrier.forward,V.forward)>=90;local offset=reversed and g.layout.backOffset or g.layout.forwardOffset
    local rotation=carrier.rotation*Q.Euler(0,reversed and 180 or 0,0)
    local a=self:pivot(first)+carrier.forward*offset;local b=self:pivot(last)+self.p.trucks[last].root.forward*offset;local position=(a+b)*.5
    g.root.rotation=rotation;g.fromPosition=g.root.position;g.toPosition=position;g.fromWidth=g.width.localScale.x;g.toWidth=V.Distance(a,b)+g.layout.widthOffset
    g.fromLeft=g.left.localPosition;g.fromRight=g.right.localPosition;g.toLeft=Q.Inverse(rotation)*(a-position);g.toRight=Q.Inverse(rotation)*(b-position)
    if not g.leftRoll then
        local carpet=g.model:Find('Scale/Carpet')
        carpet:GetComponent(typeof(U.MeshFilter)).sharedMesh=self.assets:carpet_mesh(#covered)
        tile(carpet,#covered,1)
    end
    g.progress=immediate and 1 or 0;self:apply_carpet(g)
end
function M:apply_carpet(g)
    local t=g.progress;g.root.position=V.Lerp(g.fromPosition,g.toPosition,t);g.width.localScale=V(g.fromWidth+(g.toWidth-g.fromWidth)*t,1,1)
    if g.roll then visible(g.roll.gameObject,g.leftRoll~=nil or t<1 or g.removing or g.rollRemoval~=nil) end
    if g.leftRoll then visible(g.leftRoll.gameObject,g.width.localScale.x>.1)
    else for i=1,g.covered-1 do g.dividers[i].localPosition=V.right*g.width.localScale.x*(i/g.covered-.5) end end
    g.left.localPosition=V.Lerp(g.fromLeft,g.toLeft,t);g.right.localPosition=V.Lerp(g.fromRight,g.toRight,t)
    if g.roll then g.roll.localRotation=Q.Euler(0,0,360*t) end
end
function M:unlock(g,from,rotation)
    if g.unlockTime and g.unlockTime<2.18 then return end
    N.destroy(g.flyingKey);g.flyingKey=nil;g.entranceTime=nil;g.closed=false;g.unlockTime=0
    g.root.localScale=V.one;g.model.localScale=V.one;g.padlock.localScale=V.one;g.padlock.localRotation=Q.identity
    reset_morph(g.front);reset_morph(g.top);visible(g.root.gameObject,true)
    gate_art(g,0)
    g.flyingKey=self:key(self.p.board,g.value,V.zero);g.keyOrigin=from;g.keyRotation=rotation or g.root.rotation
    g.flyingKey.transform:SetPositionAndRotation(from,g.keyRotation)
end
function M:apply_door(g)
    local time=g.unlockTime;local point=g.model:Find('Lock Pivot/Lock/Unlock Point');local target=point and point.position or g.padlock.position
    gate_art(g,clamp((time-1.2)/.7))
    if N.alive(g.flyingKey) then
        local fly=clamp(time/.7);local turn=out_back(clamp((time-.8)/.2));local socket=point and point.rotation or g.padlock.rotation
        local transform=g.flyingKey.transform
        transform.position=V.Lerp(g.keyOrigin,target,fly)+V.up*(fly*15.7079639*math.sin(fly*math.pi))
        transform.rotation=fly<1 and Q.Lerp(g.keyRotation,socket,fly) or Q.LerpUnclamped(socket,socket*Q.AngleAxis(90,V.up),turn)
        transform.localScale=V.one*((1+.83*fly)*(1-clamp((time-1)/.1)))
        if time>=1.1 then N.destroy(g.flyingKey);g.flyingKey=nil end
    end
    g.padlock.localScale=V.one*(1-clamp((time-1.1)/.1))
    if g.front then for i=0,g.front.sharedMesh.blendShapeCount-1 do weight(g.front,i,100*clamp((time-1.2-(g.front.sharedMesh.blendShapeCount-1-i)*.04)/.04)) end end
    weight(g.top,0,100*clamp((time-1.48)/.45));g.model.localScale=V(1,1,1-clamp((time-1.93)/.25))
    if time>=2.18 then visible(g.root.gameObject,false) end
end
function M:apply_gate_entrance(g)
    local time=g.entranceTime or 0;g.root.localScale=V(1,1,clamp(time/.25))
    gate_art(g,1-clamp((time-.25)/.7))
    weight(g.top,0,100*(1-clamp((time-.25)/.45)))
    if g.front then for i=0,g.front.sharedMesh.blendShapeCount-1 do weight(g.front,i,100*(1-clamp((time-.70-i*.04)/.04))) end end
    g.padlock.localScale=V.one*clamp((time-.98)/.1)
end
function M:refresh_gates(immediate)
    local states=self.p.context.rules.d.carriers
    for _,g in ipairs(self.gates) do
        if g.kind=='carpet' then
            local remaining={}
            for _,i in ipairs(g.members) do if contains(states[i].carpetLocks,g.value) then remaining[#remaining+1]=i end end
            if immediate then
                g.removing=false;g.revealing={};g.progress=1;g.rollRemoval=nil;visible(g.root.gameObject,#remaining>0)
                if #remaining>0 then g.roll.localScale=V.one;self:carpet_span(g,remaining,true) end
            elseif #remaining==0 and not g.removing then
                g.removing=true;visible(g.root.gameObject,true);for _,i in ipairs(g.members) do g.revealing[i]=true end
                g.covered=0;g.fromPosition=g.root.position;g.toPosition=g.fromPosition;g.fromWidth=g.width.localScale.x;g.toWidth=0
                g.fromLeft=g.left.localPosition;g.toLeft=g.fromLeft;g.fromRight=g.right.localPosition;g.toRight=g.fromLeft;g.progress=0
            elseif #remaining>0 and #remaining~=g.covered then
                local retained={};for _,i in ipairs(remaining) do retained[i]=true end
                for _,i in ipairs(g.members) do if not retained[i] then g.revealing[i]=true end end
                visible(g.root.gameObject,true);self:carpet_span(g,remaining,false)
            end
        else
            local locked=false
            for _,i in ipairs(g.members) do if contains(g.kind=='lock' and states[i].lockColors or states[i].ropeLocks,g.value) then locked=true;break end end
            if immediate then
                g.closed=locked;g.ropeTime=nil;g.unlockTime=nil;g.root.localScale=V.one;visible(g.root.gameObject,locked)
                if g.kind=='lock' then
                    N.destroy(g.flyingKey);g.flyingKey=nil;g.entranceTime=nil
                    g.model.localScale=V.one;g.padlock.localScale=V.one;g.padlock.localRotation=Q.identity
                    reset_morph(g.front);reset_morph(g.top);gate_art(g,0)
                end
            elseif g.kind=='rope' then
                if g.closed and not locked then g.ropeTime=0;visible(g.root.gameObject,true) end;g.closed=locked
            else
                if g.closed and not locked and not g.unlockTime then self:unlock(g,g.root.position+V.up*4,g.root.rotation) end;g.closed=locked
            end
        end
    end
end
function M:refresh(immediate)
    local d=self.p.context.rules.d
    for i=0,d.carriers.Count-1 do if not self.features[i] then self:add_carrier(i) end end
    -- Apply initial closed/open state before replaying initial key events. In
    -- later frames the actual key socket must win over the fallback position.
    if immediate then self:refresh_gates(true) end
    -- These queued events contain the original cargo socket, before rule code clears key colors.
    if not self.entrance_hidden then
        for k=0,d.keyCollections.Count-1 do
            local event=d.keyCollections[k];local truck=self.p.trucks[event.carrier];local f=self.features[event.carrier]
            local center=f.generatorCenter or V(0,1.69,1.29+math.max(0,d.carriers[event.carrier].capacityGroups-4))
            local from=truck.root:TransformPoint(center+V.up*.96-V.forward*math.floor(event.slot/self.p.group_size));local used=false
            for _,g in ipairs(self.gates) do if g.kind=='lock' and g.value==event.color then self:unlock(g,from,truck.root.rotation);used=true end end
            if not used then
                local pickup=self:key(self.p.board,event.color,V.zero);pickup.transform.position=from
                self:animate(pickup,'pickup',.5,function(t) pickup.transform.position=from+V.up*t;pickup.transform.localScale=V.one*(1-t) end,function() N.destroy(pickup) end)
            end
        end
        d.keyCollections:Clear()
    end
    if not immediate then self:refresh_gates(false) end
    for i=0,d.carriers.Count-1 do
        local state=d.carriers[i];local f=self.features[i];local root=f.truck.root
        if f.frozen and not state.Frozen and not immediate and f.ice then
            -- The trigger is already satisfied: remove the shell and play
            -- debris now, without a separate wait or interaction lock.
            visible(f.ice,false);self:particle('ice',root:TransformPoint(V(0,.84,.05)),root.rotation)
        end
        if state.Frozen then visible(f.ice,true) elseif immediate then visible(f.ice,false) end
        f.frozen=state.Frozen
        if f.curtain and f.curtain.open~=state.curtainOpen then
            local c=f.curtain;c.open=state.curtainOpen;c.moving=true;visible(c.go,true)
            self:animate(c.go,'curtain',self.curtain_clip.duration,function(t) self:sample_curtain(c,(c.open and t or 1-t)*self.curtain_clip.duration) end,
                function() c.moving=false;self.p:mark_dirty(i) end)
        end
        if not f.packed and state.packed and not immediate then
            f.packed=true;f.completing=true
            -- Packing is authoritative only once all incoming blocks settle.
            -- Start the check and closure together; settlement waits for both.
            local check_done,back_done=false,false
            self:back(f,false,false,function()
                back_done=true;if check_done then f.completing=false end
            end)
            visible(f.check,true);f.check.transform.localScale=f.checkSize*2;f.checkAngle=10
            self:particle('complete',f.check.transform.position,root.rotation);self.p:play('complete')
            self:animate(f.check,'check',.5,function(t) local ease=in_out_back(t);f.check.transform.localScale=f.checkSize*(2-ease);f.checkAngle=10*(1-ease) end,
                function() f.check.transform.localScale=f.checkSize;f.checkAngle=0;check_done=true;if back_done then f.completing=false end end)
            if f.barrier then self:animate(f.barrier,'barrier',.5,function(t) f.barrierModel.localPosition=V(f.barrierPosition.x,f.barrierPosition.y+(-.2-f.barrierPosition.y)*t,f.barrierPosition.z) end,function() visible(f.barrier,false) end) end
        elseif immediate then
            f.completing=false;self:stop(f.check,'check');self:back(f,not state.packed,true)
            visible(f.check,state.packed)
            -- Undo reconstructs visuals from a snapshot; already-completed
            -- trucks must keep their removed barrier hidden.
            visible(f.barrier,not state.packed)
        end
        f.packed=state.packed
        if not state.packed then visible(f.barrier,true) end
        f.awaiting=state.generator and state.releasing>0 and f.count>state.cargo.Count
        if state.generator and state.releasing==0 and f.count~=state.cargo.Count then
            if f.count>state.cargo.Count and not immediate then
                f.moving=true;f.generatorPhase=0
                self:animate(f.generator,'generator',.5,function(t)
                    local show=f.generatorPhase<.5 and t>=.5;f.generatorPhase=t
                    for j,cover in ipairs(f.covers) do cover.localScale=V.Scale(f.coverSizes[j],V(math.abs(2*t-1),1,1)) end
                    if show then self.p:mark_dirty(i) end
                end,function() f.moving=false;for j,cover in ipairs(f.covers) do cover.localScale=f.coverSizes[j] end;self.p:mark_dirty(i) end)
            end
            f.count=state.cargo.Count
        end
        local count=math.ceil(state.cargo.Count/self.p.group_size)
        if count~=f.displayedCount then for _,label in ipairs(f.counts) do label.text=tostring(count) end;f.displayedCount=count end
        for _,label in ipairs(f.counts) do visible(label.gameObject,state.Accessible) end
        for group=0,math.max(4,state.capacityGroups)-1 do
            local slot=group*self.p.group_size;local piece=slot<state.cargo.Count and d.pieces[state.cargo[slot]] or nil
            local active=state.Accessible and not state.packed and piece~=nil and (not state.generator or (not self:conceals(i) and group==math.floor((state.cargo.Count-1)/self.p.group_size)))
            local center=f.generatorCenter or V(0,1.69,1.29+math.max(0,state.capacityGroups-4)-group)
            local showKey=active and not piece.hidden and piece.keyColor>0
            if showKey and not f.keys[group] then f.keys[group]=self:key(f.visuals.transform,piece.keyColor,center+V.up*.96);f.keyColors[group]=piece.keyColor end
            visible(f.keys[group],showKey)
            if showKey and f.keyColors[group]~=piece.keyColor then
                local renderers=f.keys[group]:GetComponentsInChildren(typeof(U.MeshRenderer),true)
                local tint=U.MaterialPropertyBlock();tint:SetColor('_Color',self.assets:color(piece.keyColor))
                for j=0,renderers.Length-1 do if renderers[j].gameObject.name~='Key black outline' then renderers[j]:SetPropertyBlock(tint) end end
                f.keyColors[group]=piece.keyColor
            end
            local showScrew=active and piece.sticky
            if showScrew and not f.screws[group] then f.screws[group]=self:create('sticky',f.visuals.transform);f.screws[group].transform.localPosition=center end
            visible(f.screws[group],showScrew)
        end
    end
end
function M:shake(index)
    local f=self.features[index];if not f then return end
    for _,go in pairs(f.screws) do if N.alive(go) and go.activeSelf then
        local rotation=go.transform.localRotation
        self:animate(go,'jiggle',.325,function(t) go.transform.localRotation=rotation*Q.Euler(0,0,math.sin(t*10*math.pi)*12*(1-t)) end,function() go.transform.localRotation=rotation end)
    end end
end
function M:release(index,firstGroup)
    local f=self.features[index];if not f then return end
    for group,go in pairs(f.screws) do if group>=firstGroup and N.alive(go) and go.activeSelf then
        f.screws[group]=nil;self:stop(go);go.transform:SetParent(self.p.board,true)
        self:clip(go,'sticky-complete',function() N.destroy(go) end)
    end end
end
function M:entrance_start()
    for _,f in pairs(self.features) do
        f.arrived=false
        self:back(f,false,true)
    end
end
function M:carrier_arrived(index)
    local f=self.features[index]
    if not f or f.arrived then return end;f.arrived=true
    self:back(f,true,false)
end
function M:entrance_done()
    self.entrance_hidden=false
    for index,f in pairs(self.features) do self:carrier_arrived(index);visible(f.visuals,true) end
    for _,g in ipairs(self.gates) do
        if g.kind=='lock' and g.root.gameObject.activeSelf then g.entranceTime=0;self:apply_gate_entrance(g) end
        visible(g.model.gameObject,true)
    end
end
function M:blocked(index,forTransfer)
    local f=self.features[index]
    if f and (f.moving or (f.curtain and f.curtain.moving)) then return true end
    for _,g in ipairs(self.gates) do
        local member=false;for _,i in ipairs(g.members) do if i==index then member=true;break end end
        if member and (g.entranceTime or (g.kind=='lock' and g.unlockTime and g.unlockTime<2.18) or (g.kind=='rope' and g.ropeTime and g.ropeTime<.25) or (g.kind=='carpet' and g.progress<1 and g.revealing[index])) then return true end
    end
    return false
end
function M:conceals(index)
    local f=self.features[index];if not f then return false end
    return f.awaiting or (f.moving and f.generatorPhase<.5)
end
function M:busy()
    if #self.motions>0 or #self.particles>0 then return true end
    for _,g in ipairs(self.gates) do if g.entranceTime or (g.unlockTime and g.unlockTime<2.18) or (g.ropeTime and g.ropeTime<.25) or g.progress<1 or g.rollRemoval then return true end end
    return false
end
function M:completion_busy()
    for _,f in pairs(self.features) do if f.completing then return true end end
    return false
end
function M:set_paused(value)
    if self.paused==value then return end;self.paused=value
    for _,item in ipairs(self.particles) do for i=0,item.systems.Length-1 do
        local system=item.systems[i]
        if value then item.paused[i]=system.isPlaying;if item.paused[i] then system:Pause(false) end
        elseif item.paused[i] then system:Play(false) end
    end end
end
function M:update(dt)
    if self.closed or self.paused then return end
    for i=#self.motions,1,-1 do
        local m=self.motions[i]
        if not N.alive(m.owner) then table.remove(self.motions,i)
        else
            m.time=m.time+dt
            if m.time>=0 then
                local t=clamp(m.time/m.duration);m.pose(t)
                if t>=1 then table.remove(self.motions,i);if m.finish then m.finish() end end
            end
        end
    end
    for _,g in ipairs(self.gates) do
        if g.entranceTime then g.entranceTime=math.min(1.08,g.entranceTime+dt);self:apply_gate_entrance(g);if g.entranceTime>=1.08 then g.entranceTime=nil end end
        if g.kind=='lock' and g.unlockTime and g.unlockTime<2.18 then g.unlockTime=math.min(2.18,g.unlockTime+dt);self:apply_door(g)
        elseif g.kind=='rope' and g.ropeTime and g.ropeTime<.25 then g.ropeTime=math.min(.25,g.ropeTime+dt);g.root.localScale=V(1,1-g.ropeTime/.25,1);if g.ropeTime>=.25 then visible(g.root.gameObject,false) end
        elseif g.kind=='carpet' and g.progress<1 then
            g.progress=math.min(1,g.progress+dt*2);self:apply_carpet(g)
            if g.progress>=1 then g.revealing={};if g.removing then g.rollRemoval=0 end end
        elseif g.kind=='carpet' and g.rollRemoval then
            g.rollRemoval=math.min(.25,g.rollRemoval+dt);local t=g.rollRemoval/.25;g.roll.localScale=V.one*(1-t*t*(2.70158*t-1.70158))
            if g.rollRemoval>=.25 then g.rollRemoval=nil;visible(g.root.gameObject,false) end
        end
    end
    local camera_rotation
    for _,f in pairs(self.features) do
        if f.packed and (f.check_camera~=self.p.frame_revision or f.check_last_angle~=f.checkAngle) and N.alive(f.check) then
            camera_rotation=camera_rotation or self.p.camera.transform.rotation
            f.check.transform.rotation=camera_rotation*Q.Euler(0,0,f.checkAngle)
            f.check_camera,f.check_last_angle=self.p.frame_revision,f.checkAngle
        end
    end
    for i=#self.particles,1,-1 do
        local item=self.particles[i];local alive=false
        if N.alive(item.go) then for j=0,item.systems.Length-1 do if item.systems[j]:IsAlive(false) then alive=true;break end end end
        if not alive then N.destroy(item.go);table.remove(self.particles,i) end
    end
end
function M:dispose()
    if self.disposed then return end;self.closed=true;self.motions={}
    for _,item in ipairs(self.particles) do if N.alive(item.go) then for i=0,item.systems.Length-1 do item.systems[i]:Stop(false,U.ParticleSystemStopBehavior.StopEmittingAndClear) end end end
    for i=#self.owned,1,-1 do N.destroy(self.owned[i]);self.owned[i]=nil end
    self.particles={};self.features={};self.gates={};self.p=nil;self.assets=nil;self.disposed=true
end
return M
