-- Full native Unity presentation, owned and driven by the module's Lua VM.
local N=require('platform.native')
local Route=require('platform.route')
local Assets=require('platform.assets')
local Progress=require('platform.progress')
local Services=require('platform.services')
local UI=require('platform.ui')
local Mechanics=require('platform.mechanisms')
local Boosters=require('platform.boosters')
local Cadence=require('game.ad_cadence')
local U,V,Q=N.U,N.V,N.Q
local P={};P.__index=P
local function destroy(go) N.destroy(go) end
local function active(go,value) if go.activeSelf~=value then go:SetActive(value) end end
local function setting(data,key,default) return data[key]==nil and default or data[key] end

function P.new(context,host)
    local self=setmetatable({context=context,host=host,time=0,owned={},trucks={},pieces={},flights={},releases={},
        history={},covers={},dirty={},visible={},belt={},closed=false,paused=true,entering=true,accumulator=0,home_visible=false},P)
    -- Construction can fail before Runtime assigns context.view.
    context:track(function() self:dispose() end)
    self.assets=Assets.new(host);self.progress=Progress.new(host)
    self.presentation=self.assets:data('presentation')
    self.services=Services.new(host,self.progress)
    self.root=N.node('LoopSort module physics')
    host:TrackInstance(self.root)
    local S=U.SceneManagement
    self.scene=S.SceneManager.CreateScene('LoopSort_'..tostring(CS.System.DateTime.UtcNow.Ticks),
        S.CreateSceneParameters(S.LocalPhysicsMode.Physics3D))
    S.SceneManager.MoveGameObjectToScene(self.root,self.scene)
    self.physics=U.PhysicsSceneExtensions.GetPhysicsScene(self.scene)
    local camera=N.node('Module camera',self.root.transform):AddComponent(typeof(U.Camera))
    self.camera=camera;camera.orthographic=true;camera.clearFlags=U.CameraClearFlags.SolidColor
    camera.backgroundColor=N.hex(0x49263E);camera.nearClipPlane=.1;camera.farClipPlane=120
    camera.cullingMask=1<<N.layer;camera.depth=20;camera.depthTextureMode=U.DepthTextureMode.Depth
    local light=N.node('Module key light',self.root.transform):AddComponent(typeof(U.Light))
    light.type=U.LightType.Directional;light.intensity=1.1;light.color=U.Color(1,.95,1)
    light.cullingMask=1<<N.layer;light.shadows=U.LightShadows.None;light.transform.rotation=Q.Euler(48,-30,0)
    self.effects=self.root:AddComponent(typeof(U.AudioSource));self.music=self.root:AddComponent(typeof(U.AudioSource))
    self.music.loop=true;self.music.volume=.60;self.music.clip=self.assets:audio('music')
    self.backdrop=N.node('Workshop background',camera.transform)
    self.backdrop.transform.localPosition=V(0,0,95)
    local background=self.backdrop:AddComponent(typeof(U.SpriteRenderer));background.sprite=self.assets:sprite('gameBackground')
    local backdrop_material=self.assets:new_material('backdrop');backdrop_material.mainTexture=background.sprite.texture
    background.sharedMaterial=backdrop_material;self.backdrop_sprite=background.sprite
    self.cargo=self.assets:mesh(421,false);self.fragment=self.assets:mesh(401,false);self.cell_mesh=self.assets:mesh('cell')
    self.size,self.mesh_center,self.mesh_min=self.cargo.bounds.size,self.cargo.bounds.center,self.cargo.bounds.min
    self.material=self.assets:material('cargo');self.hidden=self.assets:new_material('hidden');self.outline=self.assets:material('outline')
    self.property=U.MaterialPropertyBlock();self.color_properties={};self.appearances={}
    self.batch_colors={};self.linear_colors=U.QualitySettings.activeColorSpace==U.ColorSpace.Linear
    self.fragment_scale=V.one*(.51/self.fragment.bounds.size.x)
    self.physics_material=self.assets:physics_material()
    self.source=self.assets:data('carrier-visual');self.long_source=self.assets:data('long-carrier-visual')
    for _,node in ipairs(self.source.nodes) do if node.path=='Carrier/Pivot' then self.pivot=N.vec(node.transform.m_LocalPosition) end end
    assert(self.pivot,'Carrier pivot is missing')
    self.batch=U.SystemInfo.supportsInstancing and self.material.enableInstancing and self.material.shader.isSupported
    self.draw_batches={}
    local actions={progress=self.progress,services=self.services}
    for name,fn in pairs({
        release=function(i) return self:release(i) end,
        tap=function(pos) return self:tap(pos) end,
        pause=function(value) context:set_menu_paused(value) end,
        home=function() self:home() end,
        play=function() return self:start(self.progress.data.currentLevel,'main') end,
        level=function(id,kind) return self:start(id,kind or 'main') end,
        daily=function(id,day) return self:start(id,'daily',day) end,
        retry=function() return self:start(context.level.id,context.kind,self.daily_day) end,
        next=function() return self:start(self:next_id(),context.kind) end,
        skip=function()
            if context.kind~='main' then return self:home() end
            local next=self:next_id();if next<=context.level.id then self.ui:toast('已是最后一关');return end
            self.progress:skip_level(context.level.id);return self:start(next,'main')
        end,
        reset_local=function()
            self.services:cancel_all()
            local ok,message=self.progress:reset_local();if not ok then self.ui:toast(message);return end
            self:start(1,'main');self:home()
        end,
        undo=function() return self.boosters:undo() end,
        shuffle=function()
            if self.boosters:busy() then return false,'请等待道具动画完成' end
            self.shuffle_selected=not self.shuffle_selected
            return false,self.shuffle_selected and '请选择要打乱的货车' or ''
        end,
        belt=function() return self.boosters:expand() end,
        truck=function() return self.boosters:add_truck() end,
        gm_unlock_all=function()local last=1;for _,id in ipairs(context.repository:list('main')) do last=math.max(last,id) end;return self.progress:gm_unlock_all(last) end,
        gm_boosters=function() return self.progress:gm_boosters() end,
        gm_clear_boosters=function() self.shuffle_selected=false;return self.progress:gm_boosters(0) end,
        gm_finish=function(won) return self:gm_finish(won) end,
    }) do actions[name]=context:callback(fn) end
    self.ui=UI.new(host,self.assets,actions)
    self.boosters=Boosters.new(self)
    return self
end
function P:play(name)
    if self.progress.data.sound then local clip=self.assets:audio(name);if clip then self.effects:PlayOneShot(clip,.55) end end
end
function P:initial_level_id(kind)
    return kind=='main' and self.progress.data.currentLevel or nil
end
function P:next_id()
    local ids=self.context.repository:list(self.context.kind)
    for i,id in ipairs(ids) do if id==self.context.level.id then return ids[i+1] or ids[1] end end
    return ids[1]
end
function P:start(id,kind,day)
    if not self.progress:can_start() then return false,'当前账号不可用，请重新进入游戏' end
    self.daily_day=kind=='daily' and (day or Progress.day()) or nil
    self.home_visible=false;self.camera.enabled=true
    self.context:open_level(id,kind)
    return true
end
function P:gm_finish(won)
    if not(U.Application.isEditor or U.Debug.isDebugBuild) then return false,'GM 仅限开发版本' end
    local c=self.context
    if self.closed or self.services.invalidated or c.loading or self.entering or self.boosters:busy() then return false,'请等待关卡或道具动画结束' end
    if self.result_done or c.rules.d.status~=0 then return false,'本关已结束，请重新开始后再测试' end
    self.result_done=true;self.shuffle_selected=false;self.home_visible=false;self.camera.enabled=true
    if self.board then self.board.gameObject:SetActive(true) end
    c:set_menu_paused(true);c.rules.d.status=won and 1 or 2;c.rules.d.failReason=won and -1 or 0
    c.ui_state.reward=self.progress:complete(c.level,c.kind,won,self:next_id(),tostring(c.generation)..':'..tostring(c.rules.d.status),self.daily_day)
    self.ui:close_dialog(true);self.ui:_destroy_scope(self.ui.home_scope);self.ui.home_scope=nil;self.ui.home_visible=false
    self.ui.game_scope.root.gameObject:SetActive(true);self.ui.guides_pending=false;self.ui.guide_queue={}
    c.ui_state.result_ready=true;self.ui:refresh(c);return true
end
function P:home()
    if self.boosters then self.boosters:cancel(true) end
    self.home_visible=true;self.shuffle_selected=false
    self.context:set_menu_paused(true);self.camera.enabled=false
    if self.board then self.board.gameObject:SetActive(false) end
    self.progress:save();self.ui:home()
end
function P:set_paused(paused,entering)
    if self.closed then return end
    self.paused,self.entering=paused,entering
    if self.music then
        -- Host/background pauses still mute audio; menu and booster pauses do not.
        if self.context.closed or self.context.host_paused or not self.progress.data.music then self.music:Pause()
        elseif self.music.clip and not self.music.isPlaying then self.music:UnPause();if not self.music.isPlaying then self.music:Play() end end
    end
    if self.ui then self.ui:set_paused(paused) end
    if self.mechanics then self.mechanics:set_paused(paused) end
    -- Native physics is manually stepped; no Time.timeScale changes or
    -- velocity loss. Renderer fallback keeps a paused frame visible.
    if paused then for _,p in pairs(self.visible) do self:renderer_enabled(p,true) end end
end
function P:clear_level()
    if self.boosters then self.boosters:cancel(false) end
    if self.mechanics then self.mechanics:dispose();self.mechanics=nil end
    if self.board then destroy(self.board.gameObject);self.board=nil end
    if self.arrow_mesh then destroy(self.arrow_mesh);self.arrow_mesh=nil end
    self.trucks,self.pieces,self.flights,self.releases,self.covers,self.dirty={},{},{},{},{},{}
    self.visible,self.belt={},{}
    self.appearances={}
    self.draw_batches={}
    self.tutorial_projection=nil
    self.history={};self.accumulator=0
end
function P:load_level(context)
    self:clear_level();self.context=context;self.route=Route.new(assert(context.spline,'Missing conveyor'))
    local p=self.progress.data
    local function has(list) for _,id in ipairs(list or {}) do if id==context.level.id then return true end end;return false end
    Cadence.normalize(p,#p.completedLevels)
    self.ad_eligible=context.kind=='main' and not has(p.completedLevels) and not has(p.skippedLevels)
    self.ad_attempt_seconds,self.ad_save_seconds=0,0
    if context.kind=='daily' then self.daily_day=self.daily_day or Progress.day() else self.daily_day=nil end
    self.group_size=context.rules.group_size;local f=context.rules.d.fragments
    self.cell=V(self.size.x/f.Columns,self.size.y/f.Rows,self.size.z/f.Depth)
    self.board=N.node('Level '..context.level.id,self.root.transform).transform
    self.track=N.clone(self.assets:track(context.kind,context.level.id),self.board)
    self.track_renderers=self.track:GetComponentsInChildren(typeof(U.Renderer),true)
    self.track_property=U.MaterialPropertyBlock()
    local arrows=self.track.transform:Find('Arrows')
    if arrows then
        local filter=arrows:GetComponent(typeof(U.MeshFilter))
        self.arrow_mesh=U.Object.Instantiate(filter.sharedMesh);self.arrow_mesh:MarkDynamic();filter.sharedMesh=self.arrow_mesh
        self.arrow_vertices=self.arrow_mesh.vertices;self.scaled_arrows=N.array(V,self.arrow_vertices.Length);self.arrow_centers={}
        for i=0,math.floor(self.arrow_vertices.Length/3)-1 do self.arrow_centers[i]=self.route:at(i*1.8)+V.up*.012 end
    end
    self.placements={}
    for i=0,context.level.carriers.Length-1 do self:create_truck(context.level.carriers[i],i) end
    self:reserve_chain_clearance()
    self.mechanics=Mechanics.new(self);self.mechanics:load()
    self:create_portals()
    self:frame();self:refresh_cargo(true)
    self.result_at,self.result_done,self.shuffle_selected=nil,false,false
    context.ui_state={levelId=context.level.id,collected=0,total=math.floor(context.rules.d.beltCapacity/self.group_size)}
    self.ui:load_level(context)
    if context.kind=='main' then self.progress.data.currentLevel=context.level.id;self.progress:changed() end
    self:begin_entrance(context:hold_ready())
    if not self.initialized then
        self.initialized=true
        -- Explicit launches enter that level; normal entry opens the home page.
        if not self.host.Launch or not self.host.Launch.LevelId or self.host.Launch.LevelId=='' then self:home() end
    end
end
function P:reserve_chain_clearance()
    local footprints={};local count=self.context.level.carriers.Length;local needed=false
    for i=0,count-1 do
        local t=self.trucks[i];local art=t.root:Find('Volcano art')
        local b=art and art:GetComponent(typeof(U.Renderer)).bounds or t.collider.bounds
        local ropes={};local locks=t.data.ropeLocks
        if locks then for j=0,locks.Length-1 do ropes[#ropes+1]=locks[j];needed=true end end
        local axis=math.abs(t.root.right.x)>.5 and 0 or 1;local forward=axis==0 and t.root.forward.z or t.root.forward.x
        footprints[i+1]={box={b.min.x,b.min.z,b.max.x,b.max.z},axis=axis,away=forward>=0 and 1 or -1,ropes=ropes}
    end
    if not needed then return end
    local offsets=require('game.visual_clearance').solve(footprints)
    for i=0,count-1 do
        local t=self.trucks[i];local delta=V(offsets[i+1][1],0,offsets[i+1][2])
        t.root.position=t.root.position+delta;t.origin=t.root.position;t.outlet=t.outlet+delta
        t.gate,t.gate_tangent=self.route:nearest(t.outlet);t.gate_x,t.gate_y,t.gate_z=t.gate.x,t.gate.y,t.gate.z
    end
end
function P:create_truck(data,index)
    local long=data.capacityGroups>4
    local go=data.generator and N.node('Generator '..data.id,self.board) or N.clone(self.assets:prefab(long and 'long-carrier' or 'carrier'),self.board)
    local root=go.transform;root.rotation=Q.Euler(0,data.yaw,0)
    root.position=V(data.x,-1,data.z)-root.rotation*self.pivot+V.up*(self.presentation.carrierLift or 0)
    local t={root=root,data=data,state=self.context.rules.d.carriers[index],groups={},remainders={},index=index}
    self.trucks[index]=t;self.placements[index]=data;t.origin=root.position;t.rotation=root.rotation
    if long then self:resize_long(root,data.capacityGroups-12) end
    local color=long and data.targetColor or data.tagColor
    do
        local frame_color=color>0 and N.color(color)*.62 or self.assets:material('plum').color
        local visual=long and self.long_source or self.source
        for _,part in ipairs(visual.parts) do
            if not part.wheel then
                local node=root:Find(part.path:gsub('^Carrier/',''))
                local renderer=node and node:GetComponent(typeof(U.Renderer))
                if renderer then for i,id in ipairs(part.materialIds) do if color>0 or id==32 then
                    local props=U.MaterialPropertyBlock();props:SetColor('_Color',(id==31 or id==32) and frame_color or N.color(color))
                    renderer:SetPropertyBlock(props,i-1)
                end end end
            end
        end
        local deck=root:Find('Cargo deck');if deck then N.tint(deck.gameObject,frame_color) end
        local cart=root:Find('Volcano art')
        if cart and color>0 then N.tint(cart.gameObject,self.assets:color(color)) end
    end
    local center,size=V(0,1.2,0),V(2.15,2.8,4.8)
    if data.curtainColor>0 then center,size=V(0,1.5,.1),V(2.85,3.1,5.65) end
    t.front=V(0,1.69,1.29+(long and data.capacityGroups-4 or 0))
    local outlet=V(0,1.7,-2.7)
    if long then center=N.vec(self.long_source.colliderCenter)+V.forward*(data.capacityGroups-12)*.5
        size=N.vec(self.long_source.colliderSize)+V.forward*(data.capacityGroups-12);outlet=N.vec(self.long_source.outlet) end
    if data.generator then
        for _,v in ipairs(self.assets:data('feature-visuals').visuals) do if v.id=='generator' then
            local offset=self.presentation.generatorVisualOffset or 0
            center,size=N.vec(v.colliderCenter)+V.up*(.96+offset),N.vec(v.colliderSize)+V.up*1.92
            outlet=N.vec(v.outlet)+V.up*offset
            t.front=V(0,3.5+offset,center.z+.05)+root:InverseTransformDirection(V.back*(.8/math.tan(math.rad(self.presentation.pitch or 70))))
        end end
    end
    local collider=go:GetComponent(typeof(U.BoxCollider)) or go:AddComponent(typeof(U.BoxCollider))
    collider.center=center;collider.size=size;collider.isTrigger=true;t.collider=collider
    t.outlet=root:TransformPoint(outlet);t.gate,t.gate_tangent=self.route:nearest(t.outlet)
    t.gate_x,t.gate_y,t.gate_z=t.gate.x,t.gate.y,t.gate.z
    t.depth_direction=outlet.z<t.front.z and -1 or 1
    t.cargo=N.node('Cargo',root).transform
    for g=0,data.capacityGroups-1 do
        local group,renderer=N.model('Group '..g,self.cargo,self.material,t.cargo)
        t.groups[g]={go=group,renderer=renderer};group:SetActive(false)
    end
    for j=1,2 do local go,r=N.model('Peel remainder '..j,self.cargo,self.material,t.cargo);go:SetActive(false);t.remainders[j]={go=go,renderer=r} end
    t.hull=N.model('Cargo outline',self.cargo,self.outline,t.cargo);t.hull:SetActive(false)
    self:mark_dirty(index)
end
function P:resize_long(root,delta)
    if delta==0 then return end
    for _,path in ipairs(self.long_source.frontParts or {}) do local n=root:Find(path:gsub('^Carrier/',''));if n then n.position=n.position+root.forward*delta end end
    for _,path in ipairs(self.long_source.middleParts or {}) do local n=root:Find(path:gsub('^Carrier/',''));if n then n.position=n.position+root.forward*(delta*.5) end end
    for _,path in ipairs(self.long_source.stretchParts or {}) do
        local n=root:Find(path:gsub('^Carrier/',''))
        if n then local f=n:GetComponent(typeof(U.MeshFilter)) or n:GetComponent(typeof(U.SkinnedMeshRenderer));local mesh=f and f.sharedMesh
            if mesh then
                local direction=n:InverseTransformDirection(root.forward);local axis=math.abs(direction.x)>math.abs(direction.y) and 'x' or 'y'
                if math.abs(direction.z)>math.abs(direction[axis]) then axis='z' end
                local length=mesh.bounds.size[axis]*math.abs(n.lossyScale[axis])
                if length>.0001 then
                    local before=n.localScale;local after=V(before.x,before.y,before.z)
                    after[axis]=after[axis]*math.max(.01,(length+delta)/length)
                    local edge=V.zero;edge[axis]=direction[axis]>0 and mesh.bounds.min[axis] or mesh.bounds.max[axis]
                    n.localPosition=n.localPosition+n.localRotation*V.Scale(before-after,edge);n.localScale=after
                end
            end
        end
    end
    local art=root:Find('Volcano art')
    if art then
        local size=art.localScale;art.localScale=V(size.x,size.y+delta,size.z)
        art.localPosition=art.localPosition+V.forward*(delta*.5)
    end
end
function P:cargo_local(index,slot)
    local t=self.trucks[index];local f=self.context.rules.d.fragments
    local group=t.data.generator and 0 or math.floor(slot/self.group_size);local within=slot%self.group_size
    local depth=math.floor(within/(f.Columns*f.Rows));local row=math.floor(within/f.Columns)%f.Rows;local col=within%f.Columns
    if row%2==1 then col=f.Columns-1-col end
    return t.front+self.mesh_center+V((col-(f.Columns-1)*.5)*self.cell.x,(row-(f.Rows-1)*.5)*self.cell.y,
        -group+(depth-(f.Depth-1)*.5)*self.cell.z*t.depth_direction)
end
function P:cargo_position(index,slot) return self.trucks[index].root:TransformPoint(self:cargo_local(index,slot)) end
function P:emission_slot(index,order)
    local f=self.context.rules.d.fragments;local plane=f.Columns*f.Rows
    local depth=math.floor(order/plane);local row=f.Rows-1-math.floor((order%plane)/f.Columns);local col=order%f.Columns
    if self.trucks[index].depth_direction<0 then depth=f.Depth-1-depth end
    if row%2==1 then col=f.Columns-1-col end
    return depth*plane+row*f.Columns+col
end
function P:mark_dirty(index) self.dirty[index]=true end
function P:piece(id)
    local p=self.pieces[id];if p then return p end
    local go,r,f=N.model('Fragment '..id,self.cell_mesh,self.material,self.board)
    p={id=id,go=go,tr=go.transform,renderer=r,filter=f,mesh=self.cell_mesh,renderer_enabled=true,
        visible=false,receive_after=0,last_carrier=-1,travelled=0,distance=0}
    go:SetActive(false);self.pieces[id]=p;return p
end
function P:renderer_enabled(p,value)
    if p.renderer_enabled~=value then p.renderer.enabled=value;p.renderer_enabled=value end
end
function P:show_piece(p,visible)
    p.visible=visible;active(p.go,visible)
    self.visible[p.id]=visible and p or nil
    if not visible and p.body then p.body.isKinematic=true;p.body.detectCollisions=false end
    if not visible then self.belt[p.id]=nil end
end
function P:animated(p,position,rotation,belt)
    self.belt[p.id]=nil
    if p.body and not p.body.isKinematic then p.body.interpolation=U.RigidbodyInterpolation.None;p.body.isKinematic=true;p.body.detectCollisions=false end
    p.tr:SetPositionAndRotation(position,rotation)
    local mesh=belt and self.fragment or self.cell_mesh
    if p.mesh~=mesh then p.filter.sharedMesh=mesh;p.mesh=mesh end
    p.tr.localScale=belt and self.fragment_scale or self.cell
    p.matrix=nil
    p.pose_revision=(p.pose_revision or 0)+1
    self:show_piece(p,true)
end
function P:start_physics(p,position,rotation,velocity)
    if not p.body then
        local c=p.go:AddComponent(typeof(U.SphereCollider));c.radius=.21;c.contactOffset=.01;c.sharedMaterial=self.physics_material
        local b=p.go:AddComponent(typeof(U.Rigidbody));p.body=b;b.mass=4;b.drag=.1;b.angularDrag=10
        b.solverIterations=1;b.solverVelocityIterations=1;b.maxLinearVelocity=20;b.maxAngularVelocity=50
        b.useGravity=false;b.sleepThreshold=.005;b.maxDepenetrationVelocity=10;b.collisionDetectionMode=U.CollisionDetectionMode.Discrete
    end
    self:animated(p,position,rotation,true)
    p.body.position=position;p.body.rotation=rotation;p.body.isKinematic=false;p.body.detectCollisions=true;p.body.velocity=velocity
    -- Manual scenes do not use the default scene's interpolation scheduler.
    p.body.interpolation=U.RigidbodyInterpolation.None
    p.render_from_position,p.render_to_position=position,position
    p.render_from_rotation,p.render_to_rotation=rotation,rotation
    p.render_scale=p.tr.lossyScale
    self.belt[p.id]=p
end

function P:appearance(renderer,piece)
    local group=math.floor(piece.id/self.group_size);local cover=self.covers[group]
    if not cover then cover={hidden=piece.hidden};self.covers[group]=cover end
    if piece.hidden then cover.hidden=true;cover.at=nil
    elseif cover.hidden then
        cover.hidden=false;cover.at=self.time
        self.progress:event('reveal',1)
        if self.mechanics then self.mechanics:particle('hidden',renderer.bounds.center,renderer.transform.rotation) end
    end
    local progress=cover.at and N.clamp(self.time-cover.at,0,1) or 1
    local concealed=piece.hidden or progress<1
    local reveal=piece.hidden and 0 or progress
    local previous=self.appearances[renderer]
    if previous and previous.concealed==concealed and previous.color==piece.color and previous.reveal==reveal then return not concealed end
    if not previous then previous={};self.appearances[renderer]=previous end
    if previous.concealed~=concealed then renderer.sharedMaterial=concealed and self.hidden or self.material end
    previous.concealed,previous.color,previous.reveal=concealed,piece.color,reveal
    if concealed then
        self.property:Clear();self.property:SetFloat('_Reveal',piece.hidden and 0 or N.sine(progress))
        self.property:SetColor('_CargoColor',N.color(piece.color));renderer:SetPropertyBlock(self.property)
        return false
    end
    local props=self.color_properties[piece.color]
    if not props then props=U.MaterialPropertyBlock();props:SetColor('_Color',N.color(piece.color));self.color_properties[piece.color]=props end
    renderer:SetPropertyBlock(props)
    return true
end
function P:remainder(t,view,group,piece,min,max)
    local scale=max-min
    if scale.x<=0 or scale.y<=0 or scale.z<=0 then active(view.go,false);return end
    local center=self.mesh_min+V.Scale(self.size,(min+max)*.5)
    view.go.transform.localScale=scale
    view.go.transform.localPosition=t.front-V.forward*(t.data.generator and 0 or group)+center-V.Scale(self.mesh_center,scale)
    self:appearance(view.renderer,piece);active(view.go,true)
end
function P:refresh_truck(index)
    if self.boosters and self.boosters:owns_truck(index) then return end
    local t=self.trucks[index];local state=self.context.rules.d.carriers[index];t.state=state
    local d=self.context.rules.d;local q=self.releases[index]
    local open=state.curtainOpen and not (t.data.generator and self.mechanics.entrance_hidden)
    active(t.cargo.gameObject,open)
    local front=math.floor((state.cargo.Count-1)/self.group_size)
    if q then for g in pairs(q.groups) do front=math.max(front,g) end end
    for _,v in ipairs(t.remainders) do active(v.go,false) end
    for g=0,t.data.capacityGroups-1 do
        local display=t.groups[g];local queued=q and q.groups[g]
        local shown=open and (not t.data.generator or (g==front and (queued or not self.mechanics:conceals(index))))
        if queued then
            if shown then
                local launched=queued.launched;local f=d.fragments;local plane=f.Columns*f.Rows
                if launched==0 then self:remainder(t,display,g,queued.piece,V.zero,V.one)
                else
                    local depth=math.floor(launched/plane);local row=f.Rows-1-math.floor((launched%plane)/f.Columns);local col=launched%f.Columns
                    local z0,z1=depth/f.Depth,(depth+1)/f.Depth
                    self:remainder(t,display,g,queued.piece,V(0,0,z1),V.one)
                    self:remainder(t,t.remainders[1],g,queued.piece,V(0,0,z0),V(1,row/f.Rows,z1))
                    self:remainder(t,t.remainders[2],g,queued.piece,V(col/f.Columns,row/f.Rows,z0),V(1,(row+1)/f.Rows,z1))
                end
            else active(display.go,false) end
        else
            local first,last=g*self.group_size,math.min((g+1)*self.group_size,state.cargo.Count)-1
            local whole=shown and last-first+1==self.group_size
            local piece=first<=last and d.pieces[state.cargo[first]] or nil
            if whole then for slot=first,last do
                local p=d.pieces[state.cargo[slot]]
                if p.color~=piece.color or (p.location~=0 and p.location~=4) then whole=false;break end
            end end
            active(display.go,whole)
            if whole then
                display.go.transform.localPosition=t.front-V.forward*(t.data.generator and 0 or g)
                display.go.transform.localScale=V.one;self:appearance(display.renderer,piece)
            end
            for slot=first,last do
                local p=d.pieces[state.cargo[slot]]
                if p.location==0 or p.location==4 then
                    local view=self.pieces[p.id]
                    if whole or not shown then if view then self:show_piece(view,false) end
                    else
                        view=view or self:piece(p.id)
                        self:animated(view,self:cargo_position(index,slot),t.root.rotation,false)
                        self:appearance(view.renderer,p)
                    end
                end
            end
        end
    end
    local releasable=self.context.rules:releasable_count(index)
    local outline=open and not self.mechanics:conceals(index) and not q and state.cargo.Count>0 and state.cargo.Count%self.group_size==0
        and releasable>0 and releasable%self.group_size==0
    local bounds
    if outline then
        local front_piece=d.pieces[state.cargo[state.cargo.Count-1]]
        for slot=state.cargo.Count-releasable,state.cargo.Count-1 do
            local member=d.pieces[state.cargo[slot]]
            if member.location~=0 or member.hidden or member.color~=front_piece.color then outline=false;break end
            local cell_bounds=U.Bounds(self:cargo_local(index,slot),self.cell)
            if not bounds then bounds=cell_bounds else bounds:Encapsulate(cell_bounds) end
        end
    end
    active(t.hull,outline)
    if outline then
        local scale=V(bounds.size.x/self.size.x,bounds.size.y/self.size.y,bounds.size.z/self.size.z)
        t.hull.transform.localScale=scale
        t.hull.transform.localPosition=bounds.center-V.Scale(self.mesh_center,scale)
    end
end
function P:refresh_cargo(force)
    if not self.mechanics then return end
    local d=self.context.rules.d
    local changed=force or self.last_revision~=d.featureRevision or self.last_budget~=d.beltOccupiedSlots or next(self.dirty)~=nil
    if not changed then return end
    self.mechanics:refresh()
    if force or self.last_revision~=d.featureRevision or self.last_budget~=d.beltOccupiedSlots then
        for i=0,d.carriers.Count-1 do self.dirty[i]=true end
        self.last_revision,self.last_budget=d.featureRevision,d.beltOccupiedSlots
    end
    for i in pairs(self.dirty) do self:refresh_truck(i) end
    self.dirty={}
end
function P:capture()
    local checkpoint={state=self.context.rules:capture(),pieces={},count=self.context.rules.d.carriers.Count}
    for id,p in pairs(self.belt) do
        if self.context.rules.d.pieces[id].location==2 then
            checkpoint.pieces[id]={position=p.tr.position,rotation=p.tr.rotation,velocity=p.body.velocity,angular=p.body.angularVelocity,
                after=p.receive_after-self.time,last=p.last_carrier,travelled=p.travelled,distance=p.distance}
        end
    end
    return checkpoint
end
function P:release(index)
    if self.boosters:busy() then return false,'请等待道具动画完成' end
    if self.services.invalidated or self.paused or self.entering or not self.trucks[index] or self.mechanics:blocked(index) or not self.ui:allows_release(index) then return false end
    if self.shuffle_selected then
        local ok,message=self.boosters:shuffle(index)
        if not ok and message then self.ui:toast(message) end
        return ok,message
    end
    local rules=self.context.rules;local d=rules.d;local state=d.carriers[index]
    local slots,settled,positions={},{},{}
    for slot=0,state.cargo.Count-1 do
        local id=state.cargo[slot];slots[id]=slot;settled[id]=d.pieces[id].location==0
        local view=self.pieces[id];if view and view.visible then positions[id]=view.tr.position end
    end
    local before=d.pendingTransfers==0 and self:capture() or nil
    local result=rules:release(index)
    if not result.accepted then if self.ui.toast then self.ui:toast(result.reason) end;self.mechanics:shake(index);return false,result.reason end
    if before then self.history[#self.history+1]=before end
    local q={index=index,elapsed=0,next=1,queue={},groups={}}
    local ids={};for i=0,result.pieces.Length-1 do ids[#ids+1]=result.pieces[i] end
    local groups={};for _,id in ipairs(ids) do groups[math.floor(slots[id]/self.group_size)]=true end
    local ordered={};for group in pairs(groups) do ordered[#ordered+1]=group end;table.sort(ordered,function(a,b)return a>b end)
    for _,group in ipairs(ordered) do
        local by_slot={};local complete=true
        for _,id in ipairs(ids) do if math.floor(slots[id]/self.group_size)==group then by_slot[slots[id]%self.group_size]=id end end
        for order=0,self.group_size-1 do if not by_slot[order] or not settled[by_slot[order]] then complete=false end end
        if complete then
            local first=by_slot[0];q.groups[group]={launched=0,piece=d.pieces[first]}
            for order=0,self.group_size-1 do
                local slot=group*self.group_size+self:emission_slot(index,order);local id=by_slot[slot%self.group_size]
                q.queue[#q.queue+1]={id=id,slot=slot,group=group}
                if self.pieces[id] then self:show_piece(self.pieces[id],false) end
            end
        else
            for _,id in ipairs(ids) do if math.floor(slots[id]/self.group_size)==group then q.queue[#q.queue+1]={id=id,slot=slots[id],from=positions[id]} end end
        end
    end
    -- Receiving flights can be interrupted by releasing the same truck. Token
    -- checks below make the old flight inert without teleporting its neighbours.
    self.releases[index]=q
    self.mechanics:release(index,ordered[#ordered] or 0)
    self:mark_dirty(index);self:refresh_cargo();self:play('click')
    return true
end
function P:preserve_booster_history()
    local rules=self.context.rules
    for _,snapshot in ipairs(self.history) do
        rules.rebase_booster_snapshot(snapshot.state,rules.d)
        snapshot.count=rules.d.carriers.Count
    end
end
function P:queue_flight(view,from,to,index,receiving,slot)
    local piece=self.context.rules.d.pieces[view.id]
    self.flights[#self.flights+1]={view=view,from=from,to=to,index=index,receiving=receiving,slot=slot,
        time=0,rotation=view.tr.rotation,target_rotation=receiving and self.trucks[index].root.rotation or Q.Euler(45,45,0),
        epoch=self.context.rules.d.epoch,version=piece.transferVersion}
end
function P:emit(q,entry,order)
    local d=self.context.rules.d;local piece=d.pieces[entry.id];local view=self:piece(entry.id)
    local from=entry.from or (view.visible and view.tr.position or self:cargo_position(q.index,entry.slot))
    self:animated(view,from,self.trucks[q.index].root.rotation,true);self:appearance(view.renderer,piece)
    if entry.group then
        local group=q.groups[entry.group];group.launched=group.launched+1
        if group.launched>=self.group_size then q.groups[entry.group]=nil end
    end
    local truck=self.trucks[q.index];local position,tangent=truck.gate,truck.gate_tangent
    local side=V.Cross(V.up,tangent).normalized
    local target=position+V.up*.25+side*(order%2==0 and -.25 or .25)
    self:queue_flight(view,from,target,q.index,false,entry.slot);self:mark_dirty(q.index)
end
function P:update_transfers(dt)
    local d=self.context.rules.d
    for index,q in pairs(self.releases) do
        q.elapsed=q.elapsed+dt
        while q.next<=#q.queue and q.elapsed>=(q.next-1)*(.25/5.5*.5) do self:emit(q,q.queue[q.next],q.next-1);q.next=q.next+1 end
        if q.next>#q.queue then self.releases[index]=nil end
    end
    for i=#self.flights,1,-1 do
        local flight=self.flights[i];local view=flight.view;local piece=d.pieces[view.id]
        local expected=flight.receiving and 3 or 1
        if d.epoch~=flight.epoch or piece.transferVersion~=flight.version or piece.location~=expected then table.remove(self.flights,i)
        else
            flight.time=flight.time+dt;local t=N.sine(flight.time/.35)
            view.tr:SetPositionAndRotation(N.jump(flight.from,flight.to,t),Q.Slerp(flight.rotation,flight.target_rotation,t))
            view.matrix=nil
            view.pose_revision=(view.pose_revision or 0)+1
            if t>=1 then
                table.remove(self.flights,i)
                if flight.receiving then
                    self.context.rules:finish_receive(view.id,flight.epoch)
                    self:animated(view,flight.to,self.trucks[flight.index].root.rotation,false)
                    self:mark_dirty(flight.index)
                    if flight.slot%self.group_size==self.group_size-1 then self:play('receive') end
                elseif self.context.rules:enter_belt(view.id,flight.epoch) then
                    local sample=self.route:sample(flight.to)
                    self:start_physics(view,flight.to,flight.target_rotation,V.zero)
                    view.body:AddForce(sample.forward*20,U.ForceMode.Impulse)
                    view.last_carrier=flight.index;view.receive_after=self.time+.25;view.distance=sample.distance;view.travelled=0
                    self:mark_dirty(flight.index)
                end
            end
        end
    end
end
function P:physics_step(dt)
    local rules=self.context.rules;local d=rules.d
    if d.status~=0 then return end
    local blocked=self.blocked_carriers or {};self.blocked_carriers=blocked
    for i=0,d.carriers.Count-1 do blocked[i]=self.mechanics:blocked(i,true) end
    for id,view in pairs(self.belt) do if d.pieces[id].location==2 then
        local body=view.body;local position=body.position
        local px,py,pz=position.x,position.y,position.z
        local sample=self.route:sample_xyz(px,py,pz);local length=self.route.length
        local distance=(sample.distance+(px-sample.x)*sample.fx+(py-sample.y)*sample.fy+(pz-sample.z)*sample.fz)%length
        view.travelled=math.max(0,view.travelled+(distance-view.distance+length*.5)%length-length*.5);view.distance=distance
        position,px,py,pz=self.route:step(body,sample,5.5*d.conveyorSpeedScale,position,px,py,pz)
        local piece=d.pieces[id];local owner=piece.receiveTarget
        local first,last=owner>=0 and owner or 0,owner>=0 and owner or d.carriers.Count-1
        local candidate,priority=-1,-1
        if self.time>=view.receive_after then for index=first,last do
            if not blocked[index] and (view.last_carrier~=index or view.travelled>=length-1.2) then
                local truck=self.trucks[index];local dx,dy,dz=px-truck.gate_x,py-truck.gate_y,pz-truck.gate_z
                if math.abs(dy)<=1.11 and dx*dx+dz*dz<=1.15^2 then
                    local value=rules:receive_priority_for_piece(index,id)
                    if value>priority then candidate,priority=index,value end
                end
            end
        end end
        if candidate>=0 and rules:reserve_receive(id,candidate) then
            local slot=d.carriers[candidate].cargo.Count-1
            self:animated(view,position,body.rotation,false)
            self:queue_flight(view,position,self:cargo_position(candidate,slot),candidate,true,slot);self:mark_dirty(candidate)
        end
    end end
    self.physics:Simulate(dt)
    for id,p in pairs(self.belt) do
        local teleported=self.exit_portal and self:portal_step(p)
        local position,rotation=p.body.position,p.body.rotation
        -- Render between physics samples without moving the collider. A portal
        -- jump resets both samples so interpolation never crosses the board.
        p.render_from_position=teleported and position or p.render_to_position
        p.render_from_rotation=teleported and rotation or p.render_to_rotation
        p.render_to_position,p.render_to_rotation=position,rotation
        p.tr:SetPositionAndRotation(position,rotation)
        p.matrix=nil
        p.pose_revision=(p.pose_revision or 0)+1
    end
end
function P:portal_step(view)
    local body=view.body;local position=body.position
    local point=self.exit_portal:InverseTransformPoint(position)-V(.004192114,.30314088,-.16386585)
    local closest=V(N.clamp(point.x,-.8643837,.8643837),N.clamp(point.y,-.8875561,.8875561),N.clamp(point.z,-.5772944,.5772944))
    local radius=.21*(.51/self.fragment.bounds.size.x)
    local inside=(point-closest).sqrMagnitude<=radius*radius
    local teleported=inside and not view.portal_inside
    if teleported then
        body.position=self.route:position(0)+self.portal_rotation*(position-self.exit_portal.position)
        body.velocity=self.route:forward(0)*body.velocity.magnitude
    end
    view.portal_inside=inside
    return teleported
end
function P:restore_undo_view()
    if self.entering or #self.history==0 then return false,'没有可以撤销的操作' end
    local snapshot=table.remove(self.history);self.context.rules:restore(snapshot.state)
    self.flights,self.releases,self.covers={},{},{}
    local removed={};for index in pairs(self.trucks) do if index>=self.context.rules.d.carriers.Count then removed[#removed+1]=index end end
    for _,index in ipairs(removed) do destroy(self.trucks[index].root.gameObject);self.trucks[index]=nil;self.placements[index]=nil end
    for id,p in pairs(self.pieces) do
        self:show_piece(p,false);local record=snapshot.pieces[id]
        if record then
            self:start_physics(p,record.position,record.rotation,record.velocity);p.body.angularVelocity=record.angular
            p.receive_after=self.time+record.after;p.last_carrier=record.last;p.travelled=record.travelled;p.distance=record.distance
        end
    end
    self.mechanics:dispose();self.mechanics=Mechanics.new(self);self.mechanics:load()
    self.result_at,self.result_done=nil,false;self.context.ui_state.result_ready=false;self.context:set_menu_paused(false);self:refresh_cargo(true);self:frame()
    return true
end
function P:create_portals()
    self.exit_portal=nil;self.portal_models={}
    if self.route.closed then return end
    local start=self.route:position(0);local forward=self.route:forward(0)
    for i=0,1 do
        local node=N.node(i==0 and 'Entry portal' or 'Exit portal',self.board)
        node.transform:SetPositionAndRotation(self.route:position(i),Q.LookRotation(self.route:forward(i),V.up)*(i==1 and Q.Euler(0,180,0) or Q.identity))
        local model=N.clone(self.assets:prefab('portal'),node.transform)
        self.assets:style_volcanic(model)
        self.portal_models[#self.portal_models+1]=model.transform
        local wall=N.node('Portal boundary',node.transform)
        wall.transform.localPosition=V(0,.4573,-.558);wall.transform.localScale=V(2.2433,1.9147,1)
        local collider=wall:AddComponent(typeof(U.BoxCollider));collider.size=V(.7697295,.9324083,.92536354)
        collider.center=V(.0014954435,-.08176604,.10076768);collider.contactOffset=.01
        if i==1 then
            self.exit_portal=node.transform
            self.portal_rotation=Q.AngleAxis(V.SignedAngle(node.transform.forward,forward,V.up),V.up)
        end
    end
end
function P:frame()
    self.frame_revision=(self.frame_revision or 0)+1
    local cfg=self.assets:data('presentation');local route=self.route;local camera=self.camera
    local bounds=U.Bounds(route.positions[1],V.zero)
    for _,p in ipairs(route.positions) do bounds:Encapsulate(p) end
    local physical=U.Bounds(route.positions[1],V.zero)
    for _,t in pairs(self.trucks) do
        bounds:Encapsulate(t.origin);bounds:Encapsulate(t.origin+t.rotation*self.pivot)
        local data=t.data
        local w=data.curtainColor>0 and 1.4 or 1.15
        local length=data.generator and 1.2 or data.curtainColor>0 and 2.8 or 2.5
        local center=data.generator and -1.21 or .1
        if data.capacityGroups>4 then length=(data.capacityGroups+.871849)*.5;center=4.122161+(data.capacityGroups-12)*.5 end
        for x=-1,1,2 do for z=-1,1,2 do for y=0,1 do
            physical:Encapsulate(t.origin+t.rotation*V(x*w,y*2.8,center+z*length))
        end end end
    end
    if self.mechanics and self.mechanics.bounds then local b=self.mechanics:bounds();if b then physical:Encapsulate(b) end end
    local min,max=bounds.min,bounds.max
    min=min-V(setting(cfg,'left',0),0,setting(cfg,'bottom',.5))
    max=max+V(setting(cfg,'right',0),0,setting(cfg,'top',.5));bounds:SetMinMax(min,max)
    local rotation=Q.Euler(setting(cfg,'pitch',78),0,0);local inverse=Q.Inverse(rotation)
    local low,high=V.one*1e20,V.one*-1e20
    local function include(b)
        for x=0,1 do for y=0,1 do for z=0,1 do
            local p=V(x==0 and b.min.x or b.max.x,y==0 and b.min.y or b.max.y,z==0 and b.min.z or b.max.z)
            p=inverse*(p-bounds.center);low=V.Min(low,p);high=V.Max(high,p)
        end end end
    end
    include(bounds);local pad=V.one*setting(cfg,'targetRadius',1);low=low-pad;high=high+pad;include(physical)
    local offset=self.context.level.cameraOffset;local aspect=math.max(.1,camera.aspect)
    local bottom,top=setting(cfg,'viewBottom',.16),setting(cfg,'viewTop',.885)
    local width=math.max(.1,setting(cfg,'framing',.93)-2*math.abs(offset.X))
    local height=math.max(.1,top-bottom-2*math.abs(offset.Y))
    camera.orthographicSize=math.max((high.x-low.x)/(2*aspect*width),(high.y-low.y)/(2*height))
    camera.transform.rotation=rotation
    local focus=bounds.center+rotation*V((low.x+high.x)*.5,(low.y+high.y)*.5,0)
    local screen=camera.transform.right*(offset.X*2*camera.orthographicSize*aspect)
        +camera.transform.up*(((bottom+top-1)*.5+offset.Y)*2*camera.orthographicSize)
    camera.transform.position=focus-camera.transform.forward*40-screen
    local b=self.backdrop_sprite.bounds.size
    local backdrop_height=camera.orthographicSize*2
    self.backdrop.transform.localScale=V(backdrop_height*aspect/b.x,backdrop_height/b.y,1)
    self.board_bounds=bounds;self.screen_width,self.screen_height=U.Screen.width,U.Screen.height
end
function P:begin_entrance(done)
    self.entrance={elapsed=0,done=done,arrival=0,items={}}
    self.track_property:SetFloat('_RouteClip',0)
    for i=0,self.track_renderers.Length-1 do self.track_renderers[i]:SetPropertyBlock(self.track_property) end
    self:entrance_details(0,0)
    local center=self.board_bounds.center
    local height=self.camera.orthographicSize+6;local width=height*self.camera.aspect+5
    for index,t in pairs(self.trucks) do
        local forward,right=t.root.forward,t.root.right;local delta=center-t.origin
        local z,x=math.abs(forward.z),math.abs(forward.x)
        local outside=(z<x and width or height)*(V.Dot(delta,forward)<0 and 1 or -1)
        local lateral=V.Dot(delta,right);local from=t.origin+forward*outside-right*(lateral*(z*lateral<x and 1 or 3))
        local direction=(t.origin-from).normalized;local duration=math.max(.01,(t.origin-from).magnitude*.04)
        local entry={from=from,to=t.origin,right=right,forward=forward,rotation=t.rotation,duration=duration,
            right_dot=V.Dot(direction,right),forward_move=V.Dot(direction,forward)>0}
        entry.left=t.root:Find('Pivot/Model Root/Wheels/Wheel L F');entry.right_wheel=t.root:Find('Pivot/Model Root/Wheels/Wheel R F')
        if entry.left then entry.left_rotation=entry.left.localRotation end
        if entry.right_wheel then entry.right_rotation=entry.right_wheel.localRotation end
        self.entrance.items[index]=entry
        self.entrance.arrival=math.max(self.entrance.arrival,duration);t.root.position=from
    end
    local e=self.entrance
    e.reveal=math.max(.01,self.route.length*.0275)
    e.settled=e.arrival+math.max(e.reveal,.6)
    e.finish=(#self.portal_models>0 and e.settled or e.arrival+e.reveal)+.6
    if self.mechanics.entrance_start then self.mechanics:entrance_start() end
end
function P:entrance_details(time,portal_time)
    local scale=N.clamp(time/.5,0,1)
    if self.arrow_mesh then
        for i=0,self.arrow_vertices.Length-1 do
            local center=self.arrow_centers[math.floor(i/3)]
            self.scaled_arrows[i]=center+(self.arrow_vertices[i]-center)*scale
        end
        self.arrow_mesh.vertices=self.scaled_arrows
    end
    local x=N.clamp(portal_time/.5,0,1)*2;local c=1.70158*1.525;local back
    if x<1 then back=.5*(x*x*((c+1)*x-c)) else x=x-2;back=.5*(x*x*((c+1)*x+c)+2) end
    local travel=N.clamp((portal_time-.4)/.2,0,1)-1;local out=1+2.70158*travel^3+1.70158*travel^2
    for _,model in ipairs(self.portal_models) do model.localScale=V.one*math.max(0,back);model.localPosition=V.back*(1-out) end
end
function P:update_entrance(dt)
    local e=self.entrance;if not e then return end;e.elapsed=e.elapsed+dt
    for index,entry in pairs(e.items) do
        local t=self.trucks[index];local ratio=N.clamp(e.elapsed/entry.duration,0,1)
        if ratio<1 then
            local pos=V.Lerp(entry.from,entry.to,ratio)+entry.right*(1.5*math.pi*entry.right_dot*ratio*math.sin(math.pi*ratio))
            local motion=pos-t.root.position
            if motion.sqrMagnitude>.000001 then
                local direction=motion.normalized
                local turn=V.SignedAngle(entry.forward_move and t.root.forward or -t.root.forward,direction,V.up)
                local steer=N.clamp(turn*(entry.forward_move and -40 or 40),-40,40)
                if entry.left then entry.left.localRotation=Q.AngleAxis(steer,V.up)*entry.left_rotation end
                if entry.right_wheel then entry.right_wheel.localRotation=Q.AngleAxis(steer,V.up)*entry.right_rotation end
                t.root.rotation=Q.LookRotation(entry.forward_move and direction or -direction,V.up)
            end
            t.root.position=pos
        else
            if not entry.arrived then
                entry.arrived=true;entry.arrival_rotation=t.root.rotation
                if entry.left then entry.left.localRotation=entry.left_rotation end
                if entry.right_wheel then entry.right_wheel.localRotation=entry.right_rotation end
                self.mechanics:carrier_arrived(index)
            end
            local elapsed=math.min(.6,e.elapsed-entry.duration)
            local travel=entry.forward*(entry.forward_move and 1 or -1)
            local phase=N.clamp((elapsed<.2 and elapsed or elapsed-.2)/.2,0,1);local ease=1-(1-phase)^2
            local brake=.3*(elapsed<.2 and ease or 1-ease)
            t.root.position=entry.to+travel*brake
            local axis=V.Cross(travel,V.up);local positive=Q.AngleAxis(2,axis)*entry.rotation;local negative=Q.AngleAxis(-2,axis)*entry.rotation
            if elapsed<.2 then t.root.rotation=Q.Lerp(entry.arrival_rotation,positive,elapsed/.2)
            elseif elapsed<.4 then t.root.rotation=Q.Lerp(positive,negative,(elapsed-.2)/.2)
            else t.root.rotation=Q.Lerp(negative,entry.rotation,(elapsed-.4)/.2) end
        end
    end
    local reveal=N.clamp((e.elapsed-e.arrival)/e.reveal,0,1)
    -- Shared track materials remain borrowed; clipping is per renderer.
    self.track_property:SetFloat('_RouteClip',reveal)
    for i=0,self.track_renderers.Length-1 do self.track_renderers[i]:SetPropertyBlock(self.track_property) end
    if e.elapsed>=e.arrival then
        if not e.trail and not e.trail_stopped then e.trail=self.mechanics:particle('trail',self.route:position(reveal)+V.down*.5,Q.identity) end
        if e.trail then e.trail.go.transform.position=self.route:position(reveal)+V.down*.5 end
        if reveal>=1 then
            if e.trail then self.mechanics:stop_particle(e.trail);e.trail=nil;e.trail_stopped=true end
            self:entrance_details(e.elapsed-e.arrival-e.reveal,math.max(0,e.elapsed-e.settled))
        end
    end
    if e.elapsed>=e.finish then
        for _,t in pairs(self.trucks) do t.root:SetPositionAndRotation(t.origin,t.rotation) end
        self.entrance=nil
        if self.mechanics.entrance_done then self.mechanics:entrance_done() end
        e.done();self:refresh_cargo(true)
    end
end
function P:tap(screen_position)
    if self.paused or self.entering or self.home_visible then return false end
    local ray=self.camera:ScreenPointToRay(V(screen_position.x,screen_position.y,0))
    -- Query each native truck collider: no InputLegacyModule, callback
    -- MonoBehaviour or ambiguous Physics.Raycast in the host's default scene.
    local chosen,nearest=nil,math.huge
    for index,t in pairs(self.trucks) do
        local hit,info=t.collider:Raycast(ray,200)
        if hit and info.distance<nearest then chosen,nearest=index,info.distance end
    end
    if chosen then return self:release(chosen) end
    return false
end
function P:update(context,dt)
    if self.closed then return end
    if context.ui_state then context.ui_state.undo_depth=#self.history end
    self.ui:update(context,dt)
    if self.screen_width~=U.Screen.width or self.screen_height~=U.Screen.height then self:frame() end
    if self.services.invalidated or self.paused or self.home_visible then return end
    if self.boosters:busy() then self.boosters:update(dt);return end
    self.time=self.time+dt;self.hidden:SetFloat('_FlowTime',self.time)
    self.mechanics:update(dt)
    if self.entrance then self:update_entrance(dt);return end
    if self.ad_eligible and context.rules.d.status==0 and not self.ui.modal and not self.services.ad_pending and U.Application.isFocused then
        local seconds=math.max(0,dt);self.ad_attempt_seconds=self.ad_attempt_seconds+seconds;self.ad_save_seconds=self.ad_save_seconds+seconds
        Cadence.time(self.progress.data,seconds);self.progress.dirty=true
        if self.ad_save_seconds>=15 then self.ad_save_seconds=0;self.progress:save() end
    end
    self:update_transfers(dt)
    self.accumulator=math.min(self.accumulator+dt,.05)
    while self.accumulator>=.0333 do self:physics_step(.0333);self.accumulator=self.accumulator-.0333 end
    self:refresh_cargo()
    local d=context.rules.d
    context.ui_state.collected=d.beltOccupiedSlots
    context.ui_state.total=math.floor(d.beltCapacity/self.group_size)
    local target=self.ui:tutorial_target_index()
    local projection=self.tutorial_projection
    if target and target>=0 and self.trucks[target] and
        (not projection or projection.target~=target or projection.frame~=self.frame_revision) then
        local bounds=self.trucks[target].collider.bounds;local min,max=V.one*1e10,V.one*-1e10
        for x=0,1 do for y=0,1 do for z=0,1 do
            local p=self.camera:WorldToViewportPoint(V(x==0 and bounds.min.x or bounds.max.x,y==0 and bounds.min.y or bounds.max.y,z==0 and bounds.min.z or bounds.max.z))
            min=V.Min(min,p);max=V.Max(max,p)
        end end end
        self.ui:set_tutorial_target(target,{xmin=min.x,ymin=min.y,xmax=max.x,ymax=max.y})
        self.tutorial_projection={target=target,frame=self.frame_revision}
    end
    if d.status~=0 and not self.result_done then
        if not self.result_at then self.result_at=self.time end
        if (d.status==1 and self.time-self.result_at>=.1 and not self.mechanics:completion_busy()) or (d.status~=1 and self.time-self.result_at>=.6 and #self.flights==0 and not self.mechanics:busy()) then
            self.result_done=true
            context.ui_state.reward=self.progress:complete(context.level,context.kind,d.status==1,self:next_id(),tostring(context.generation)..':'..tostring(d.status),self.daily_day)
            local generation,account=context.generation,self.progress.data
            local function present(shown)
                if self.closed or self.services.invalidated or self.context~=context or context.generation~=generation or self.progress.data~=account then return end
                if shown then Cadence.interstitial_closed(account,Progress.now());self.progress:changed() end
                context.ui_state.result_ready=true;self.ui:refresh(context)
            end
            if self.ad_eligible then
                Cadence.round(account,context.level.id,d.status==1,self.ad_attempt_seconds);self.progress:changed()
            end
            if self.ad_eligible and U.Application.isFocused and Cadence.eligible(account,context.level.id,d.status==1,self.ad_attempt_seconds,Progress.now()) then
                self.services:interstitial('level_complete',present)
            else present(false) end
        end
    end
end
function P:draw_pieces()
    local d=self.context.rules.d
    local blend=N.clamp(self.accumulator/.0333,0,1)
    for _,batch in pairs(self.draw_batches) do batch.count=0 end
    -- Persistent buffers per mesh/chunk: stopped assembled pieces and unchanged
    -- colors stay in native arrays instead of being uploaded again every frame.
    for id,p in pairs(self.visible) do if p.normal_render then
        local batch=self.draw_batches[p.mesh]
        if not batch then batch={mesh=p.mesh,count=0,chunks={}};self.draw_batches[p.mesh]=batch end
        local chunk_index=math.floor(batch.count/1023)+1;local slot=batch.count%1023
        local chunk=batch.chunks[chunk_index]
        if not chunk then
            chunk={matrices=N.array(U.Matrix4x4,1023),colors=N.array(U.Vector4,1023),properties=U.MaterialPropertyBlock(),slots={}}
            batch.chunks[chunk_index]=chunk
        end
        local previous=chunk.slots[slot]
        if not previous then previous={};chunk.slots[slot]=previous end
        if self.belt[id]==p then
            local position=V.Lerp(p.render_from_position,p.render_to_position,blend)
            local rotation=Q.Slerp(p.render_from_rotation,p.render_to_rotation,blend)
            chunk.matrices:SetValue(U.Matrix4x4.TRS(position,rotation,p.render_scale),slot)
            -- A later transition to a static slot must upload its exact pose.
            previous.id,previous.pose=id,nil
        elseif previous.id~=id or previous.pose~=p.pose_revision then
            if not p.matrix then p.matrix=p.tr.localToWorldMatrix end
            chunk.matrices:SetValue(p.matrix,slot);previous.id,previous.pose=id,p.pose_revision
        end
        local color_id=d.pieces[id].color
        if previous.color~=color_id then
            local color=self.batch_colors[color_id]
            if not color then
                local c=N.color(color_id);if self.linear_colors then c=c.linear end
                color=U.Vector4(c.r,c.g,c.b,c.a);self.batch_colors[color_id]=color
            end
            chunk.colors:SetValue(color,slot);previous.color=color_id;chunk.color_dirty=true
        end
        batch.count=batch.count+1
    end end
    for _,batch in pairs(self.draw_batches) do
        local remaining=batch.count
        for _,chunk in ipairs(batch.chunks) do
            if remaining<=0 then break end
            local count=math.min(remaining,1023)
            if chunk.color_dirty then chunk.properties:SetVectorArray('_Color',chunk.colors);chunk.color_dirty=false end
            U.Graphics.DrawMeshInstanced(batch.mesh,0,self.material,chunk.matrices,count,chunk.properties,
                U.Rendering.ShadowCastingMode.Off,false,N.layer,self.camera,U.Rendering.LightProbeUsage.Off)
            remaining=remaining-count
        end
    end
end
function P:late_update(context,dt)
    if self.closed or self.home_visible or not self.board then return end
    -- A single reveal clock follows the original cargo group through its
    -- solid body, peel remainders, flight and assembled cells.
    if not self.boosters:busy() then
        local revealing=false
        for group,cover in pairs(self.covers) do
            if cover.at then
                revealing=true
                if self.time-cover.at>=1 then cover.at=nil end
            end
        end
        if revealing then for index in pairs(self.trucks) do self.dirty[index]=true end end
        self:refresh_cargo()
    end
    for id,p in pairs(self.visible) do if p.visible then
        p.normal_render=self:appearance(p.renderer,context.rules.d.pieces[id])
        self:renderer_enabled(p,not (self.batch and not self.paused and p.normal_render))
    end end
    if self.batch and not self.paused then self:draw_pieces() end
end
function P:dispose()
    if self.closed then return end
    self.paused=true
    if self.camera then self.camera.enabled=false end
    if self.ui then self.ui:dispose();self.ui=nil end
    if self.services then self.services:dispose();self.services=nil end
    if self.progress then self.progress:dispose();self.progress=nil end
    self:clear_level()
    if self.root then destroy(self.root);self.root=nil end
    if self.scene and self.scene:IsValid() and self.scene.isLoaded then
        U.SceneManagement.SceneManager.UnloadSceneAsync(self.scene)
    end
    self.scene=nil;self.physics=nil
    if self.assets then self.assets:dispose();self.assets=nil end
    self.closed=true
end
return P
