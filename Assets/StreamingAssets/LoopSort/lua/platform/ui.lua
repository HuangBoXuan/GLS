-- Platform UGUI presentation. Native Unity/SDK components only; no game C# types.
local UE=CS.UnityEngine
local U=CS.UnityEngine.UI
local V2,Color=UE.Vector2,UE.Color
local Progress=require('platform.progress')
local Services=require('platform.services')
local Peripheral=require('game.peripheral_config')
local Activity=require('game.activity_config')
local FirstLevels=require('game.guide_levels')
local Unlocks=require('game.unlocks')
local Native=require('platform.native')
local Menu=require('ui.menu_theme')
local Signin=require('game.signin')
local SigninView=require('ui.signin')
local UI={};UI.__index=UI
local FULL={0,0,1,1}
local WHITE=Color.white
local BLACK=Color.black
local CLEAR=Color(1,1,1,0)
local BOOSTERS={'undo','shuffle','belt','truck'}
local BOOSTER_NAMES={undo='撤回',shuffle='打乱',belt='扩容',truck='加车'}
local BOOSTER_HELP={undo='撤销最近一次放出方块的操作，恢复到操作前的状态。',shuffle='选择一个包含多种颜色的托盘，重新排列托盘中的方块。',belt='增加传送带的容量，传送带已满时也可以使用。',truck='在本关预留的位置增加一个空托盘。'}
local TABS={'商店','地区','主页','排行','队伍'}
local TAB_ICONS={'shopNav','areasNav','homeNav','rankingNav','teamsNav'}
local MISSION_NAMES={login='累计登录',useboosters='使用道具',reveal='揭示隐藏方块',win='完成关卡',multiplier='领取广告加倍奖励',winstreak='连续通关',winhard='完成困难关卡',usecapacity='使用增加托盘道具',useshuffle='使用打乱道具',spendgolds='消耗金币',winsuperhard='完成超难关卡',useundo='使用撤回道具',earngolds='获得金币'}
local alive=Native.alive
local text_values=setmetatable({},{__mode='k'})
local rect_values=setmetatable({},{__mode='k'})
local zero,center=V2.zero,V2(.5,.5)
local function rect(rt,r)
    local old=rect_values[rt]
    if old and old[1]==r[1] and old[2]==r[2] and old[3]==r[3] and old[4]==r[4] then return end
    rt.anchorMin=V2(r[1],r[2]);rt.anchorMax=V2(r[3],r[4])
    if not old then rt.offsetMin=zero;rt.offsetMax=zero;rt.pivot=center;old={};rect_values[rt]=old end
    old[1],old[2],old[3],old[4]=r[1],r[2],r[3],r[4]
end
local function node(parent,name,r)
    local go=UE.GameObject(name,typeof(UE.RectTransform));local rt=go:GetComponent(typeof(UE.RectTransform))
    rt:SetParent(parent,false);rect(rt,r or FULL);return rt
end
local function set_text(label,value)
    if label==nil then return end
    value=tostring(value)
    -- These labels are Lua-owned. Unchanged values need no native validity,
    -- text getter or setter calls; changed values still use the safe check.
    if text_values[label]~=value and alive(label) then label.text=value;text_values[label]=value end
end
local function nonempty(value) return type(value)=='string' and value~='' end
function UI.new(host,assets,actions)
    actions=actions or {}
    local self=setmetatable({host=host,assets=assets,actions=actions,closed=false,tab=3,scopes={},image_records={},page=0,guide_queue={},home_visible=false,
        elapsed=0,last_tick=0,tutorial_target=-1,tutorial_rect=nil,guide_waiting=false,result_generation=nil,home_revision=0},UI)
    self.progress=actions.progress or Progress.new(host);self.owns_progress=actions.progress==nil
    self.services=actions.services or Services.new(host,self.progress,actions.service_config);self.owns_services=actions.services==nil
    local hasNative,nativeFeatures=pcall(function()return host.Services.Native end)
    self.native_features=hasNative and nativeFeatures or nil
    local root=node(host.Root,'LoopSort Platform UI',FULL)
    self.root=root;host:TrackInstance(root.gameObject)
    local canvas=root.gameObject:AddComponent(typeof(UE.Canvas));canvas.renderMode=UE.RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100
    local scaler=root.gameObject:AddComponent(typeof(U.CanvasScaler));scaler.uiScaleMode=U.CanvasScaler.ScaleMode.ScaleWithScreenSize
    scaler.referenceResolution=V2(1080,1920);scaler.screenMatchMode=U.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5
    root.gameObject:AddComponent(typeof(U.GraphicRaycaster))
    self.font=assert(assets:font(),'Platform UI font is missing')
    self.safe=node(root,'Safe area',FULL)
    self:_safe_area()
    local ok,data=pcall(function() return assets:data('mechanic-guides') end)
    self.guides=ok and data and data.guides or {}
    table.sort(self.guides,function(a,b)
        local left,right=FirstLevels[a.id] or math.huge,FirstLevels[b.id] or math.huge
        if left~=right then return left<right end
        return a.id<b.id
    end)
    self:_gameplay()
    self.toast_scope=self:_scope(root,'Toast layer',FULL)
    self.toast_panel=self:_image(self.toast_scope.root,'Message','toastPanel',{.10,.79,.90,.86})
    self.toast_text=self:_label(self.toast_panel,'Message text','',34,{.08,.15,.92,.85})
    self.toast_panel.gameObject:SetActive(false)
    self.tutorial_scope=self:_scope(self.safe,'Tutorial',FULL)
    self.tutorial_blockers={}
    for i=1,4 do self.tutorial_blockers[i]=self:_image(self.tutorial_scope.root,'Click limit '..i,nil,FULL,CLEAR,true) end
    self.tutorial_hand=self:_image(self.tutorial_scope.root,'Tutorial hand','tutorialHand',{0,0,.12,.08})
    self.tutorial_hand.gameObject:SetActive(false)
    self.tutorial_scope.root.gameObject:SetActive(false)
    local events=CS.UnityEngine.EventSystems.EventSystem.current
    if events==nil then
        -- The platform usually owns an EventSystem. Create one only when absent.
        local go=UE.GameObject('LoopSort UI EventSystem');go.transform:SetParent(host.Root,false)
        go:AddComponent(typeof(CS.UnityEngine.EventSystems.EventSystem));go:AddComponent(typeof(CS.UnityEngine.EventSystems.StandaloneInputModule))
        self.event_system=go;host:TrackInstance(go)
    end
    return self
end
function UI:_safe_area()
    local width,height=UE.Screen.width,UE.Screen.height
    if width<=0 or height<=0 then return end
    local area=UE.Screen.safeArea
    local bottom=math.max(area.yMin,self.banner_top or 0)
    rect(self.safe,{area.xMin/width,bottom/height,area.xMax/width,area.yMax/height})
    self:_fit_dialog_dim()
    self.screen_width,self.screen_height=width,height
    self.geometry_dirty_frames=4
end
function UI:_fit_dialog_dim()
    if not self.modal then return end
    local low,high=self.safe.anchorMin,self.safe.anchorMax
    local width,height=high.x-low.x,high.y-low.y
    if width<=0 or height<=0 then return end
    rect(self.modal.dim,{-low.x/width,-low.y/height,(1-low.x)/width,(1-low.y)/height})
end
function UI:_native_banner()
    local native=self.native_features;if not native then return end
    local wanted=self.home_visible and not self.modal and not self.closed and not self.services.invalidated or false
    if self.banner_wanted~=wanted then native:SetHomeBanner(wanted);self.banner_wanted=wanted end
    local top=wanted and native.BannerTopPixels or 0
    if top~=(self.banner_top or 0) then self.banner_top=top;self:_safe_area() end
end
function UI:_scope(parent,name,r)
    local scope={root=node(parent,name,r),callbacks={},closed=false};self.scopes[scope]=true;return scope
end
function UI:_destroy_scope(scope)
    if not scope or scope.disposed then return end
    scope.closed=true
    local children={}
    for child in pairs(self.scopes) do
        if child~=scope and alive(child.root) and alive(scope.root) and child.root:IsChildOf(scope.root) then children[#children+1]=child end
    end
    for _,child in ipairs(children) do self:_destroy_scope(child) end
    if scope.cleanup then for _,cleanup in ipairs(scope.cleanup) do cleanup() end;scope.cleanup=nil end
    for _,item in ipairs(scope.callbacks) do if alive(item.button) then item.button.onClick:RemoveListener(item.callback) end end
    scope.callbacks={}
    -- Drop layout records while the hierarchy still exists. Destroy is deferred
    -- by Unity, so waiting for the next geometry pass keeps dead userdata alive.
    local root_alive=alive(scope.root)
    for rt in pairs(self.image_records) do
        if not alive(rt) or (root_alive and rt:IsChildOf(scope.root)) then self.image_records[rt]=nil end
    end
    if alive(scope.root) then scope.root.gameObject:SetActive(false);UE.Object.Destroy(scope.root.gameObject) end
    scope.root=nil
    scope.disposed=true;self.scopes[scope]=nil
    self.geometry_dirty_frames=4
end
function UI:_image(parent,name,sprite,r,color,raycast)
    local rt=node(parent,name,r);local image=rt.gameObject:AddComponent(typeof(U.Image))
    if sprite then
        image.sprite=self.assets:sprite(sprite)
        if image.sprite and image.sprite.border.sqrMagnitude>0 then image.type=U.Image.Type.Sliced end
    end
    image.color=color or WHITE;image.raycastTarget=raycast==true
    if sprite then self.image_records[rt]={image=image,art=sprite,panel=self.assets:is_panel(sprite),face=self.assets:content_bounds(sprite),responsive=self.assets:responsive_panel(sprite)};self.geometry_dirty_frames=4 end
    return rt,image
end
local function map_face(point,source,low,high,target,ppu,sliced)
    if not sliced then return point*target end
    local pixel=point*source
    if pixel<=low then return pixel/ppu end
    if pixel>=source-high then return target-(source-pixel)/ppu end
    return low/ppu+(pixel-low)/math.max(.0001,source-low-high)*(target-(low+high)/ppu)
end
function UI:_geometry(rt,record)
    local image=record.image;local sprite=image.sprite;local r=rt.rect
    if not sprite or r.width<=0 or r.height<=0 then return end
    if record.width==r.width and record.height==r.height and record.aspect==image.preserveAspect and record.last_sprite==sprite then return end
    record.width,record.height,record.aspect,record.last_sprite=r.width,r.height,image.preserveAspect,sprite
    local sliced=record.panel and not image.preserveAspect
    image.type=sliced and U.Image.Type.Sliced or U.Image.Type.Simple
    local border=sprite.border;local ppu=math.max(.01,image.pixelsPerUnit);local multiplier=1
    if sliced then
        local scales_with_height=record.art:find('Button') or record.art=='button' or record.art=='levelTile' or record.art=='candyHomeChip'
        local minimum=1
        if record.responsive then
            local scale=record.art=='frame' and .20 or (record.art=='button' or record.art=='levelTile' or record.art=='yellowButton') and .88 or 1
            minimum=sprite.rect.height/(math.max(1,math.min(r.width,r.height))*scale*ppu)
        end
        if self.assets:standalone_panel(record.art) then
            if scales_with_height then minimum=sprite.rect.height/(r.height*ppu)
            elseif record.art=='frame' then minimum=.35
            elseif record.art=='settingsInner' or record.art=='paper' or record.art=='menuRow' then minimum=.6
            else minimum=.4 end
        end
        local edge_limit=scales_with_height and .98 or .70
        multiplier=math.max(minimum,(border.x+border.z)/(ppu*r.width*edge_limit),(border.y+border.w)/(ppu*r.height*edge_limit))
    end
    image.pixelsPerUnitMultiplier=multiplier
    if not record.content then return end
    local width,height,dx,dy=r.width,r.height,0,0;local size=sprite.rect.size
    if not sliced and image.preserveAspect then
        local scale=math.min(width/size.x,height/size.y);width,height=size.x*scale,size.y*scale;dx,dy=(r.width-width)*.5,(r.height-height)*.5
    end
    local face=record.face;ppu=math.max(.01,ppu*multiplier)
    local x0=map_face(face[1],size.x,border.x,border.z,width,ppu,sliced)
    local x1=map_face(face[1]+face[3],size.x,border.x,border.z,width,ppu,sliced)
    local y0=map_face(face[2],size.y,border.y,border.w,height,ppu,sliced)
    local y1=map_face(face[2]+face[4],size.y,border.y,border.w,height,ppu,sliced)
    rect(record.content,{(dx+x0)/r.width,(dy+y0)/r.height,(dx+x1)/r.width,(dy+y1)/r.height})
end
function UI:_content(rt)
    local record=self.image_records[rt]
    if not record then return rt end
    if not record.content then record.content=node(rt,'Artwork content',FULL);record.width=nil;self.geometry_dirty_frames=4 end
    self:_geometry(rt,record);return record.content
end
function UI:_refresh_geometry()
    if (self.geometry_dirty_frames or 0)<=0 then return end
    self.geometry_dirty_frames=self.geometry_dirty_frames-1
    for rt,record in pairs(self.image_records) do
        if not alive(rt) then self.image_records[rt]=nil
        elseif rt.gameObject.activeInHierarchy then self:_geometry(rt,record) end
    end
end
function UI:_icon(parent,name,sprite,r)
    local rt,image=self:_image(self:_content(parent),name,sprite,r);image.preserveAspect=true;return rt,image
end
function UI:_label(parent,name,text,size,r)
    local rt=node(self:_content(parent),name,r);local label=rt.gameObject:AddComponent(typeof(U.Text))
    label.font=self.font;label.fontStyle=UE.FontStyle.Normal;label.text=tostring(text or '');label.color=BLACK;label.alignment=UE.TextAnchor.MiddleCenter
    text_values[label]=tostring(text or '')
    label.fontSize=size or 38;label.resizeTextForBestFit=true;label.resizeTextMinSize=8;label.resizeTextMaxSize=size or 38
    label.horizontalOverflow=UE.HorizontalWrapMode.Wrap;label.verticalOverflow=UE.VerticalWrapMode.Truncate
    label.alignByGeometry=true;label.supportRichText=false;label.raycastTarget=false;return label
end
function UI:_button(scope,parent,name,text,r,fn,sprite,size)
    local rt,image=self:_image(parent,name,sprite or 'button',r,nil,true)
    if sprite=='toggleOn' or sprite=='toggleOff' or sprite=='boosterBg' or sprite=='boosterGrey' then image.preserveAspect=true end
    local button=rt.gameObject:AddComponent(typeof(U.Button));button.targetGraphic=image
    local colors=button.colors;colors.normalColor=WHITE;colors.highlightedColor=Color(1,.99,.94,1);colors.pressedColor=Color(.85,.83,.75,1);colors.disabledColor=Color(.84,.80,.76,.8);button.colors=colors
    local callback=function()
        if self.closed or scope.closed or not alive(rt) or not rt.gameObject.activeInHierarchy then return end
        if self.services.invalidated then self:_account_invalidated();return end
        local ok,error=pcall(fn)
        if not ok then self:toast('操作未完成，请稍后重试');UE.Debug.LogError('LoopSort UI action failed: '..tostring(error)) end
    end
    button.onClick:AddListener(callback);scope.callbacks[#scope.callbacks+1]={button=button,callback=callback}
    local label
    if text~='' then
        label=self:_label(rt,'Label',text,size or Menu.font.button,{.05,.04,.95,.96})
        label.rectTransform.offsetMin=V2(8,3);label.rectTransform.offsetMax=V2(-8,-3)
    end
    return rt,button,label
end
function UI:_input(scope,parent,name,value,hint,r,number)
    local rt,image=self:_image(parent,name,'paper',r,nil,true)
    local input=rt.gameObject:AddComponent(typeof(U.InputField));input.targetGraphic=image
    local label=self:_label(rt,'Value',value,46,{.08,.16,.92,.84})
    local placeholder=self:_label(rt,'Placeholder',hint,34,{.08,.16,.92,.84});placeholder.color=Color(.25,.25,.25,1)
    input.textComponent=label;input.placeholder=placeholder;input.text=value or '';input.characterLimit=number and 5 or 16
    input.lineType=U.InputField.LineType.SingleLine
    input.contentType=number and U.InputField.ContentType.IntegerNumber or U.InputField.ContentType.Standard
    input.caretColor=BLACK;input.customCaretColor=true;input.selectionColor=Color(.38,.78,.71,.6)
    return input
end
function UI:_pause(value)
    self.menu_paused=value
    if self.actions.pause then self.actions.pause(value) elseif self.context then self.context:set_menu_paused(value) end
end
function UI:_invoke(action,...)
    if self.services.invalidated then self:_account_invalidated();return false,'账号已切换，请重新进入游戏' end
    local fn=self.actions[action]
    if not fn then self:toast('此操作暂不可用');return false end
    local ok,message=fn(...)
    if ok==false and nonempty(message) then self:toast(message) end
    return ok,message
end
function UI:toast(message)
    if self.closed then return end
    if message==nil or message=='' then
        self.toast_until=nil;self.toast_text.text='';self.toast_panel.gameObject:SetActive(false);return
    end
    self.toast_text.text=tostring(message);self.toast_panel.gameObject:SetActive(true);self.toast_until=self.elapsed+3.5
    self.geometry_dirty_frames=4
    self.toast_scope.root:SetAsLastSibling()
end
function UI:_gameplay()
    local s=self:_scope(self.safe,'Game HUD',FULL);self.game_scope=s
    local tap_surface=self:_image(s.root,'Board input',nil,{0,.14,1,.845},CLEAR,true)
    local relay=tap_surface.gameObject:AddComponent(typeof(CS.MiniGamePlatform.Components.ModulePointerRelay))
    local on_pointer=function(event)
        if self.closed or self.services.invalidated or self.home_visible or self.modal or self.menu_paused or self.host_paused or not self.context or self.context.loading then return end
        if self.actions.tap then self.actions.tap(event.position) end
    end
    relay.PointerDown:AddListener(on_pointer)
    s.cleanup={function() if alive(relay) then relay.PointerDown:RemoveListener(on_pointer) end end}
    local top=node(s.root,'Top',{.035,.90,.965,.985})
    local level=self:_image(top,'Level','levelPanel',{.66,0,1,1});self.level_label=self:_label(level,'Level text','第 1 关',58,{.08,.16,.92,.88})
    local capacity=self:_image(top,'Capacity','capacityPanel',{.22,0,.645,1});self.capacity_panel=capacity
    self:_icon(capacity,'Conveyor capacity','beltCapacity',{.07,.20,.28,.86});self.capacity_label=self:_label(capacity,'Capacity text','0/0',58,{.30,.16,.93,.88})
    local settings=self:_button(s,top,'Settings','',{0,0,.18,1},function() self:settings() end,'boosterBg')
    self:_icon(settings,'Settings icon','settings',{.20,.19,.81,.82})
    self.instruction_bg=self:_image(s.root,'Instruction surface','paper',{.075,.849,.925,.893},Color(1,1,1,.94))
    self.instruction_label=self:_label(self.instruction_bg,'Instruction','',30,{.035,.10,.965,.90})
    local controls=node(s.root,'Boosters',{.035,.022,.965,.137});self.booster_counts={};self.booster_icons={};self.booster_buttons={}
    for i,id in ipairs(BOOSTERS) do
        local x=(i-1)*.25
        local button,control=self:_button(s,controls,'Booster '..id,'',{x+.015,0,x+.235,1},function() self:_booster(id) end,'boosterBg')
        self.booster_buttons[id]=control
        local icon_bounds=id=='truck' and {.255,.365,.745,.795} or {.13,.25,.87,.91}
        self.booster_icons[id]=self:_icon(button,'Icon',id,icon_bounds)
        self:_label(button,'Name',BOOSTER_NAMES[id],29,{.08,.10,.80,.30})
        local badge=self:_icon(button,'Count background','boosterCount',{.65,-.02,1.06,.39})
        self.booster_counts[id]=self:_label(badge,'Count','2',36,{.10,.12,.90,.88})
    end
    if UE.Application.isEditor or UE.Debug.isDebugBuild then
        self.gm_scope=self:_scope(self.safe,'GM launcher',FULL)
        self:_button(self.gm_scope,self.gm_scope.root,'GM','GM',{.865,.79,.985,.835},function()self:gm()end,'button',32)
    end
end
function UI:gm_jump(value)
    if not(UE.Application.isEditor or UE.Debug.isDebugBuild) then return end
    local id=tonumber(value)
    if not id or id~=math.floor(id) or id<1 then self:toast('请输入有效的整数关卡号');return end
    for _,candidate in ipairs(self.context.repository:list('main')) do
        if candidate==id then self:_invoke('level',id,'main');return end
    end
    self:toast('没有第 '..tostring(id)..' 关，请输入已存在的主关卡号')
end
function UI:gm()
    if not(UE.Application.isEditor or UE.Debug.isDebugBuild) then return end
    if self.services.invalidated then self:_account_invalidated();return end
    if not self.context or self.context.loading then self:toast('请等待关卡加载完成');return end
    if self.context.booster_busy then self:toast('请等待道具动画完成');return end
    local body=self:_dialog('GM 工具',.85)
    self:_button(self.modal,body,'Grant boosters','道具设为 99',{.05,.82,.485,.92},function()
        local ok=self:_invoke('gm_boosters');self:refresh(self.context)
        if ok then self:toast('撤回、打乱、扩容、加车已设为 99') end
    end,'button',36)
    self:_button(self.modal,body,'Clear boosters','清空道具',{.515,.82,.95,.92},function()
        local ok=self:_invoke('gm_clear_boosters');self:refresh(self.context)
        if ok then self:toast('撤回、打乱、扩容、加车已清空') end
    end,'button',36)
    self:_button(self.modal,body,'Force failure','当前关卡失败',{.05,.71,.485,.80},function()self:_invoke('gm_finish',false)end,'button',35)
    self:_button(self.modal,body,'Force win','当前关卡通关',{.515,.71,.95,.80},function()self:_invoke('gm_finish',true)end,'button',35)
    self:_button(self.modal,body,'Unlock all levels','解锁所有关卡',{.05,.595,.95,.675},function()local ok=self:_invoke('gm_unlock_all');if ok then self:toast('已解锁所有关卡') end end,'button',35)
    local input=self:_input(self.modal,body,'GM level',tostring(self.context.level.id),'输入主关卡号',{.05,.50,.63,.58},true)
    self:_button(self.modal,body,'Jump','跳转',{.67,.50,.95,.58},function()self:gm_jump(input.text)end,'button',40)
    self:_label(body,'Mechanic levels','机制首次出现关卡',35,{.05,.445,.95,.49})
    for i,guide in ipairs(self.guides) do
        local level=FirstLevels[guide.id]
        if level then
            local x=.035+((i-1)%2)*.48;local y=.375-math.floor((i-1)/2)*.068
            self:_button(self.modal,body,'GM mechanic '..guide.id,guide.title..' · '..level,{x,y,x+.455,y+.058},function()self:gm_jump(level)end,'button',32)
        end
    end
end
function UI:_booster(id,acknowledged)
    if not self.context or self.home_visible or self.context.loading then return end
    if self.context.booster_busy then self:toast('请等待道具动画完成');return end
    local state=self.context.rules.d
    if id=='belt' and state.status==2 and state.failReason==2 then
        self:toast('时间已用尽，请撤回或重新挑战');return
    end
    if not acknowledged and not self.progress:booster_seen(id) then
        local body=self:_dialog(BOOSTER_NAMES[id]..'道具',.49)
        self:_icon(body,'Booster icon',id,{.32,.50,.68,.84})
        self:_label(body,'Description',BOOSTER_HELP[id],38,{.08,.25,.92,.50})
        self:_button(self.modal,body,'Use','知道了',{.21,.04,.79,.20},function()
            self.progress:learn_booster(id);self:close_dialog();self:_booster(id,true)
        end,'greenButton',42)
        return
    end
    if not self.progress:can_spend(id) then self:toast('道具不足，可在每日礼包中领取');return end
    local success=self:_invoke(id)
    if success==true and id~='shuffle' then self.progress:spend(id);self:refresh(self.context) end
end
-- A temporary copy flies independently of the real button and never changes
-- its hit area. The presenter owns its clock and cancels it on level changes.
function UI:begin_booster_effect(id,target_screen)
    self:end_booster_effect()
    local scope=self:_scope(self.root,'Booster effect',FULL)
    local _,shade=self:_image(scope.root,'Booster dim',nil,FULL,Color(.10,.055,.09,0),true)
    local icon=self:_icon(scope.root,'Flying '..id,id,{.5,.5,.5,.5})
    icon.sizeDelta=V2(170,170)
    local source=self.booster_icons[id]
    local from=alive(source) and scope.root:InverseTransformPoint(source.position) or UE.Vector3.zero
    local to=UE.Vector3.zero
    if id=='belt' then to=scope.root:InverseTransformPoint(self.capacity_panel.position)
    elseif target_screen and target_screen.z>0 then
        -- This canvas is ScreenSpaceOverlay: UI world coordinates are screen pixels.
        to=scope.root:InverseTransformPoint(UE.Vector3(target_screen.x,target_screen.y,0))
    end
    self.booster_effect={scope=scope,icon=icon,image=icon:GetComponent(typeof(U.Image)),shade=shade,from=from,to=to,id=id}
    self:pose_booster_effect(0)
end
function UI:pose_booster_effect(t)
    local effect=self.booster_effect;if not effect then return end
    local fade=Native.clamp(t/.12,0,1)*Native.clamp((1.12-t)/.15,0,1)
    effect.shade.color=Color(.10,.055,.09,.62*fade)
    local scale,position
    if t<.5 then
        local travel=Native.sine(t/.5)
        position=UE.Vector3.Lerp(effect.from,UE.Vector3.zero,travel)
        scale=1+.65*travel+.12*math.sin(math.pi*travel)
    elseif t<.62 then position=UE.Vector3.zero;scale=1.65
    else
        local travel=Native.sine(Native.clamp((t-.62)/.35,0,1))
        position=UE.Vector3.Lerp(UE.Vector3.zero,effect.to,travel)+UE.Vector3.up*(math.sin(math.pi*travel)*55)
        scale=1.65-1.4*travel
    end
    effect.icon.localPosition=position;effect.icon.localScale=UE.Vector3.one*scale
    effect.image.color=Color(1,1,1,Native.clamp((1.02-t)/.05,0,1))
    if effect.id=='belt' then self.capacity_panel.localScale=UE.Vector3.one*(1+.13*math.sin(math.pi*Native.clamp((t-.97)/.25,0,1))) end
end
function UI:end_booster_effect()
    if self.booster_effect then self:_destroy_scope(self.booster_effect.scope);self.booster_effect=nil end
    if alive(self.capacity_panel) then self.capacity_panel.localScale=UE.Vector3.one end
end
function UI:_dialog(title,height,on_close,fixed_title_size)
    self:_clear_fireworks();self:_destroy_scope(self.modal)
    self.modal=self:_scope(self.safe,'Dialog overlay',FULL);self.modal_title=title;self.modal_on_close=on_close
    self.modal.dim=self:_image(self.modal.root,'Dim',nil,FULL,Color(.17,.08,.15,.70),true)
    self:_fit_dialog_dim()
    local low=(1-(height or .68))*.5
    local shift=title=='设置' and -.025 or title=='选择关卡' and -.012 or 0
    local header_left=title=='设置' and .21 or title=='选择关卡' and .20 or .18
    local edge=title=='七日签到' and .02 or title=='选择关卡' and .03 or .055
    local panel=self:_image(self.modal.root,'Dialog','frame',{edge,low+shift,1-edge,1-low+shift},nil,true)
    local chrome=node(panel,'Dialog chrome',FULL)
    if title~='恭喜通过' then
        local offset=title=='机制图鉴' and .012 or 0
        local label=self:_label(chrome,'Title',title,fixed_title_size or Menu.font.title,{.13,.89+offset,.87,.98+offset})
        if fixed_title_size then label.resizeTextForBestFit=false end
    end
    local close=self:_button(self.modal,chrome,'Close','',{.89,.91,1.015,1.015},function()
        if self.modal_on_close then self.modal_on_close() else self:close_dialog() end
    end,'close')
    self.dialog_close=close
    self.modal_body=node(self:_content(panel),'Content',{.015,.015,.985,.88})
    self:_pause(true);self.tutorial_scope.root.gameObject:SetActive(false)
    self:_native_banner()
    return self.modal_body
end
function UI:close_dialog(keep_paused)
    self.signin_view=nil
    self:_clear_fireworks();self.dialog_close=nil;self:_destroy_scope(self.modal);self.modal=nil;self.modal_body=nil;self.modal_title=nil;self.modal_on_close=nil
    if not keep_paused then self:_pause(self.home_visible) end
    self:_native_banner()
end
function UI:settings()
    local body=self:_dialog('设置',.727);rect(body,FULL)
    for i,item in ipairs({{'music','music','音乐'},{'sound','sound','音效'},{'haptics','haptic','振动'}}) do
        local id,icon,title=item[1],item[2],item[3];local y=.81-(i-1)*.13
        local row=self:_image(body,id..' row','menuRow',{.055,y,.945,y+.11})
        self:_icon(row,'Icon',icon,{.055,.22,.16,.78});self:_label(row,'Name',title,48,{.24,.10,.55,.90}).alignment=UE.TextAnchor.MiddleLeft
        self:_button(self.modal,row,'Toggle','',{.69,.22,.95,.78},function()
            self.progress:toggle(id);if self.actions.settings_changed then self.actions.settings_changed(self.progress.data) end;self:settings()
        end,self.progress.data[id] and 'toggleOn' or 'toggleOff',32)
    end
    local row=self:_image(body,'Language','menuRow',{.055,.42,.945,.53})
    self:_icon(row,'Icon','language',{.055,.22,.16,.78});self:_label(row,'Name','语言',48,{.24,.10,.55,.90}).alignment=UE.TextAnchor.MiddleLeft
    self:_button(self.modal,row,'Chinese','简体中文 ▾',{.50,.15,.95,.85},function()self:toast('当前版本支持简体中文')end,'languageButton',52)
    for i,item in ipairs({{'privacyPolicyUrl','隐私政策'},{'termsUrl','用户协议'},{'adPrivacyUrl','广告隐私'},{'accountDeletionUrl','账号与数据删除'}}) do
        local key,title=item[1],item[2];local x=i%2==1 and .055 or .52;local y=i<=2 and .295 or .175
        self:_button(self.modal,body,key,title,{x,y,x+.425,y+.095},function()if key=='accountDeletionUrl' then self:delete_data() else self:information(key) end end,'button',49)
    end
    if not self.home_visible then self:_button(self.modal,body,'Help','玩法图鉴',{.25,.11,.75,.17},function()self:mechanics()end,'button',32) end
    self:_button(self.modal,body,'Home','回到主页',{.08,.025,.92,.10},function()self:_invoke('home')end,'greenButton',64)
end
function UI:levels(page,filter)
    if not self.context then return end
    self.level_filter=filter or self.level_filter or 'all';local ids={};local all=self.context.repository:list('main')
    for _,id in ipairs(all) do
        if id<=Unlocks.level(self.progress.data) and (self.level_filter=='all' or (self.level_filter=='completed' and Progress.contains(self.progress.data.completedLevels,id)) or (self.level_filter=='skipped' and Progress.contains(self.progress.data.skippedLevels,id))) then ids[#ids+1]=id end
    end
    local pages=math.max(1,math.ceil(#ids/20));self.level_page=math.max(0,math.min(pages-1,page or 0))
    local body=self:_dialog('选择关卡',.729);rect(body,FULL)
    for i,item in ipairs({{'all','全部'},{'completed','已通关'},{'skipped','已跳过'}}) do
        local key,title=item[1],item[2];local x=.04+(i-1)*.315
        self:_button(self.modal,body,key,title,{x,.81,x+.29,.90},function()self:levels(0,key)end,self.level_filter==key and 'yellowButton' or 'button',Menu.font.tab)
    end
    for cell=0,19 do local id=ids[self.level_page*20+cell+1]
        if id then local x=.04+(cell%5)*.19;local y=.635-math.floor(cell/5)*.15
            local tile=self:_button(self.modal,body,'Level '..id,tostring(id),{x,y,x+.17,y+.12},function()self:_invoke('level',id)end,'button',Menu.font.level)
            if not Progress.contains(self.progress.data.completedLevels,id) and Progress.contains(self.progress.data.skippedLevels,id) then
                self:_icon(tile,'Status','skipBadge',{.68,-.03,1.05,.34})
            end
        end
    end
    if #ids==0 then self:_label(body,'Empty','暂无记录',40,{.12,.40,.88,.57}) end
    local _,prev=self:_button(self.modal,body,'Previous','上一页',{.05,.035,.32,.13},function()self:levels(self.level_page-1)end,'button',Menu.font.tab);prev.interactable=self.level_page>0
    self:_label(body,'Page',(self.level_page+1)..' / '..pages,40,{.35,.035,.65,.13})
    local _,next=self:_button(self.modal,body,'Next','下一页',{.68,.035,.95,.13},function()self:levels(self.level_page+1)end,'button',Menu.font.tab);next.interactable=self.level_page<pages-1
end
function UI:mechanics(page)
    local guides={};for _,guide in ipairs(self.guides) do if Unlocks.mechanic(self.progress.data,guide.id) then guides[#guides+1]=guide end end
    local pages=math.max(1,math.ceil(#guides/6));page=math.max(0,math.min(pages-1,page or 0))
    local body=self:_dialog('机制图鉴',.84,nil,Menu.font.guideTitle)
    rect(body,{.015,.015,.985,.96})
    for cell=0,5 do
        local guide=guides[page*6+cell+1]
        if guide then
            local x=.025+(cell%2)*.49;local y=.67-math.floor(cell/2)*.259
            local b=self:_button(self.modal,body,'Guide '..guide.id,'',{x,y,x+.46,y+.254},function() self:_guide(guide,function() self:mechanics(page) end,false) end,'mechanicTile')
            self:_icon(b,'Mechanic art',guide.sprite,{.21,.34,.79,.943})
            local title=self:_label(b,'Name',guide.title,Menu.font.guideName,{.035,.04,.965,.34})
            title.resizeTextForBestFit=false
        end
    end
    self:_label(body,'Discovery count','已发现 '..#guides..' 种玩法',29,{.08,.101,.92,.15})
    local _,p=self:_button(self.modal,body,'Previous','上一页',{.05,.005,.32,.08},function() self:mechanics(page-1) end,'button',29);p.interactable=page>0
    self:_label(body,'Page',(page+1)..' / '..pages,31,{.35,.005,.65,.08})
    local _,n=self:_button(self.modal,body,'Next','下一页',{.68,.005,.95,.08},function() self:mechanics(page+1) end,'button',29);n.interactable=page<pages-1
end
function UI:_guide(guide,next,discovery)
    local body=self:_dialog(guide.title,.66,next,Menu.font.guideTitle)
    self:_icon(body,'Mechanic illustration',guide.sprite,{.12,.41,.88,.98})
    local explanation=self:_label(body,'Explanation',guide.description,Menu.font.guideDescription,{.055,.145,.945,.40})
    explanation.resizeTextForBestFit=false
    self:_button(self.modal,body,'Acknowledge',discovery and '知道了' or '返回图鉴',{.19,.005,.81,.13},next,'greenButton',40)
end
function UI:_guide_ids()
    local level=self.context.level;local ids={}
    if not level.closed then ids[20]=true end
    for i=0,level.carriers.Length-1 do
        local c=level.carriers[i]
        if c.curtainColor>0 then ids[21]=true end;if c.barrier then ids[2]=true end
        if c.iceRequires.Length>0 then ids[4]=true end;if c.lockColors.Length>0 then ids[23]=true end
        if c.ropeLocks.Length>0 then ids[25]=true end;if c.carpetLocks.Length>0 then ids[26]=true end
        if c.tagColor>0 then ids[24]=true end;if c.generator then ids[27]=true end
        for j=0,c.groups.Length-1 do local g=c.groups[j];if g.hidden then ids[22]=true end;if g.sticky then ids[28]=true end;if g.keyColor>0 then ids[23]=true end end
    end
    return ids
end
function UI:_next_guide()
    local guide=table.remove(self.guide_queue,1)
    if not guide then self.guide_waiting=false;self:close_dialog();return end
    self.guide_waiting=true
    self:_guide(guide,function() self.progress:learn(guide.id);self:_next_guide() end,true)
end
function UI:load_level(context)
    if self.closed then return end
    self.context=context;self.result_generation=nil;self.last_status=nil;self.tutorial_target=-1;self.tutorial_rect=nil;self.guide_queue={}
    self:close_dialog(true);self:_destroy_scope(self.home_scope);self.home_scope=nil;self.home_visible=false
    self.game_scope.root.gameObject:SetActive(true);self.tutorial_scope.root.gameObject:SetActive(false)
    self.geometry_dirty_frames=4
    if context.kind~='daily' then self.progress.data.currentLevel=context.level.id;self.progress:changed() end
    local present=self:_guide_ids()
    for _,guide in ipairs(self.guides) do if present[guide.id] and not self.progress:seen(guide.id) then self.guide_queue[#self.guide_queue+1]=guide end end
    self.guides_pending=#self.guide_queue>0;self.guide_waiting=false
    self:_pause(false);self:refresh(context)
end
function UI:_play()
    if not self.progress:can_start() then self:lives();return end
    self:close_dialog(true);self.home_visible=false;self:_destroy_scope(self.home_scope);self.home_scope=nil
    self:_native_banner()
    self.game_scope.root.gameObject:SetActive(true);self:_pause(false);self:_invoke('play')
    self.geometry_dirty_frames=4
end
function UI:home()
    if self.closed then return end
    self:close_dialog(true);self.home_visible=true;self:_pause(true)
    self.game_scope.root.gameObject:SetActive(false);self.tutorial_scope.root.gameObject:SetActive(false)
    self:_destroy_scope(self.home_scope);self.home_revision=self.home_revision+1
    local scope=self:_scope(self.safe,'Home',FULL);self.home_scope=scope
    self.home_coins=nil;self.home_lives=nil;self.home_content=nil
    self:_image(scope.root,'Background','homeBackground',FULL,nil,true)
    local gear=self:_button(scope,scope.root,'Settings','',{.025,.925,.15,.975},function()self:settings()end,'settings')
    for _,item in ipairs({{'每日礼包','gift',.025,function()self:daily_reward()end},{'暂停横幅','advertPaused',.805,function()self:advertising()end}}) do
        local b=self:_button(scope,scope.root,item[1],'',{item[3],.60,item[3]+.17,.705},item[4],'button')
        self:_icon(b,'Icon',item[2],{.24,.40,.76,.92})
        local caption=self:_label(b,'Name',item[1],30,{.035,.075,.965,.40})
        caption.resizeTextMinSize=8;caption.verticalOverflow=UE.VerticalWrapMode.Truncate
    end
    self:_button(scope,scope.root,'Play','第 '..self.progress.data.currentLevel..' 关',{.52,.04,.97,.12},function()self:_play()end,'greenButtonHome',Menu.font.level)
    self:_button(scope,scope.root,'Levels','选择关卡',{.03,.04,.48,.12},function()self:levels(0,'all')end,'yellowButtonHome',Menu.font.level)
    if self.progress.status~='' and not self.storage_notice_shown then self.storage_notice_shown=true;self:toast(self.progress.status) end
end
function UI:_home_page(s,root)
    for i,item in ipairs({{'calendar','每日挑战',function() self:calendar() end},{'gift','每日奖励',function() self:daily_reward() end},{'book','机制图鉴',function() self:mechanics() end}}) do
        local x=.02+(i-1)*.33
        local b=self:_button(s,root,item[2],'',{x,.73,x+.30,.97},item[3],'boosterBg')
        self:_icon(b,'Icon',item[1],{.17,.30,.83,.95});self:_label(b,'Name',item[2],32,{.03,.06,.97,.30})
    end
    local banner=self:_image(root,'Game name','ribbon',{.10,.47,.90,.64});self:_label(banner,'Title','缤纷方块',72,{.08,.14,.92,.88})
    self:_button(s,root,'Events','活动与奖励',{.18,.32,.82,.43},function() self:activities() end,'button',42)
    self:_button(s,root,'Play','第 '..self.progress.data.currentLevel..' 关',{.12,.075,.88,.25},function() self:_play() end,'greenButton',67)
    self:_label(root,'Progress','已完成 '..#self.progress.data.completedLevels..' 关',33,{.12,.015,.88,.07})
end
function UI:_shop_page(s,root)
    for i,id in ipairs(BOOSTERS) do
        local y=.76-(i-1)*.185
        local row=self:_image(root,'Shop '..id,'paper',{.015,y,.985,y+.17})
        self:_icon(row,'Booster',id,{.025,.14,.20,.90});self:_label(row,'Name',BOOSTER_NAMES[id]..' × 1',36,{.21,.49,.60,.91})
        self:_label(row,'Owned','拥有 '..self.progress.data[id],28,{.21,.11,.60,.48})
        self:_button(s,row,'Buy',Progress.prices[id]..' 金币',{.63,.19,.97,.83},function()
            local ok,message=self.progress:buy(id);if ok then self:home(1);self:toast('购买成功') else self:toast(message) end
        end,'greenButton',32)
    end
    self:_button(s,root,'Premium','金币礼包 / 高级通行证',{.07,.025,.93,.145},function()
        self.services:purchase('store',function(_,message) self:toast(message) end)
    end,'button',35)
end
function UI:_areas_page(s,root,page)
    self:_destroy_scope(self.areas_scope);local scope=self:_scope(root,'Area list',FULL);self.areas_scope=scope
    for i=1,4 do
        local index=page*4+i;local area=Peripheral.areas[index]
        if area then
            local y=.77-(i-1)*.19;local b=self:_button(scope,scope.root,'Area '..index,'',{.025,y,.975,y+.17},function()
                local ok,message=self.progress:select_area(index);if ok then self:home(2) else self:toast(message) end
            end,self.progress.data.areaId==index and 'greenButton' or 'paper')
            self:_label(b,'Name',area.name,43,{.07,.41,.65,.87})
            self:_label(b,'Requirement',self.progress.data.highestUnlocked>=area.unlock and (self.progress.data.areaId==index and '当前地区' or '点击切换') or '第 '..area.unlock..' 关解锁',29,{.07,.10,.90,.43})
            self:_label(b,'Number',tostring(index),44,{.73,.43,.92,.86})
        end
    end
    local pages=math.ceil(#Peripheral.areas/4)
    local _,prev=self:_button(scope,scope.root,'Previous','上一页',{.05,.02,.32,.12},function() self:_areas_page(s,root,page-1) end,'button',30);prev.interactable=page>0
    self:_label(scope.root,'Page',(page+1)..' / '..pages,35,{.35,.02,.65,.12})
    local _,next=self:_button(scope,scope.root,'Next','下一页',{.68,.02,.95,.12},function() self:_areas_page(s,root,page+1) end,'button',30);next.interactable=page<pages-1
end
function UI:_online_page(s,root,kind)
    local surface=self:_image(root,'Online surface','paper',{.02,.03,.98,.97})
    local label=self:_label(surface,'Status','正在读取…',36,{.09,.38,.91,.65})
    local revision=self.home_revision
    self.services[kind](self.services,function(ok,data)
        if self.closed or s.closed or revision~=self.home_revision then return end
        if not ok then label.text=tostring(data);return end
        local list=type(data)=='table' and (data.entries or data.teams or data) or {}
        if #list==0 then label.text=kind=='leaderboard' and '暂无排行记录' or '暂无可加入的队伍';return end
        label.text=''
        for i=1,math.min(#list,7) do
            local row=list[i];local y=.86-(i-1)*.115
            self:_label(surface,'Row '..i,tostring(row.rank or i)..'  '..tostring(row.name or row.playerName or '玩家')..'  '..tostring(row.score or row.memberCount or ''),34,{.07,y,.93,y+.10})
        end
    end)
end
function UI:profile()
    local body=self:_dialog('个人资料',.51)
    self:_icon(body,'Avatar','avatar',{.33,.61,.67,.98})
    local input=self:_input(self.modal,body,'Player name',self.progress.data.playerName,'请输入昵称',{.09,.38,.91,.57},false)
    self:_label(body,'Progress','已完成 '..#self.progress.data.completedLevels..' 关',35,{.09,.23,.91,.35})
    self:_button(self.modal,body,'Save','保存',{.20,.02,.80,.19},function()
        local name=input.text:gsub('^%s+',''):gsub('%s+$','');if name=='' then self:toast('昵称不能为空');return end
        self.progress.data.playerName=name;self.progress:changed();self:home(self.tab)
    end,'greenButton',42)
end
function UI:lives()
    local body=self:_dialog('生命',.48)
    self:_icon(body,'Heart','heart',{.34,.59,.66,.97})
    self:_label(body,'Lives','当前生命 '..self.progress:life_label()..' / 5',43,{.08,.44,.92,.60})
    self:_label(body,'Timer',self.progress:life_detail(),35,{.08,.30,.92,.44})
    self:_button(self.modal,body,'Refill','900 金币补满',{.13,.07,.87,.25},function()
        local ok,message=self.progress:refill();if ok then self:lives();self:refresh(self.context) else self:toast(message) end
    end,'greenButton',40)
end
function UI:daily_reward()
    if self.modal and self.modal_title=='七日签到' and self.signin_view and self.signin_view.root==self.modal.root then
        self.signin_view.view.update(Signin.state(self.progress.data,Progress.day()),false);return
    end
    local body=self:_dialog('七日签到',.86)
    -- Use the same card canvas as the scene UI; the dialog owns responsive scaling.
    rect(body,{.015,.015,.985,.985})
    local function tint(hex)return Color(math.floor(hex/65536)%256/255,math.floor(hex/256)%256/255,hex%256/255,1)end
    local view=SigninView.draw({
        art=function(parent,name,id,r,aspect)local rt,image=self:_image(self:_content(parent),name,id,r);image.preserveAspect=aspect;return rt end,
        card=function(parent,name,r)
            local container=self:_content(parent)
            if self.assets.index.materials['signin-card'] then
                local rt,image=self:_image(container,name,nil,r)
                image.material=self.assets:material('signin-card')
                return rt,image
            end
            local rt=self:_image(container,name,'paper',r)
            local _,header=self:_image(self:_content(rt),'Header',nil,{.025,.69,.975,.97})
            return rt,header
        end,
        paint=function(view,hex)view.color=tint(hex)end,
        label=function(parent,name,text,size,r,hex)local view=self:_label(parent,name,text,size,r);view.color=tint(hex);return view end,
        button=function(parent,name,text,r,click,enabled)local _,control,label=self:_button(self.modal,parent,name,text,r,click,'greenButton',43);control.interactable=enabled;return control,label end,
    },body,Signin.state(self.progress.data,Progress.day()),function()
        local ok,message=self.progress:claim_daily();self:daily_reward();self:refresh(self.context);self:toast(ok and '已领取：'..message or message)
    end,function()self:close_dialog()end,false)
    self.signin_view={root=self.modal.root,view=view}
end
function UI:calendar()
    local body=self:_dialog('每日挑战',.74)
    self:_label(body,'Month',Progress.month()..'  已完成 '..self.progress:daily_count()..' 天',38,{.05,.90,.95,.98})
    local today=CS.System.DateTime.UtcNow;local days=CS.System.DateTime.DaysInMonth(today.Year,today.Month)
    for day=1,days do
        local x=.025+((day-1)%7)*.138;local y=.78-math.floor((day-1)/7)*.105
        local key=Progress.month()..'-'..string.format('%02d',day)
        local completed=Progress.contains(self.progress.data.dailyCompleted,key)
        self:_button(self.modal,body,'Day '..day,completed and tostring(day)..' ✓' or tostring(day),{x,y,x+.125,y+.09},function()
            if day~=today.Day then self:toast('请选择今天的每日挑战');return end
            self:_start_daily()
        end,completed and 'greenButton' or 'button',31)
    end
    for i,row in ipairs(Peripheral.daily_milestones) do
        local x=.025+(i-1)*.245
        self:_button(self.modal,body,'Milestone '..i,row.days..' 天奖励',{x,.20,x+.23,.31},function()
            local ok,message=self.progress:claim_daily_milestone(i);self:toast(ok and '已领取：'..message or message)
        end,'button',27)
    end
    self:_button(self.modal,body,'Today','挑战今天的关卡',{.12,.035,.88,.16},function() self:_start_daily() end,'greenButton',41)
end
function UI:_start_daily()
    if not self.context or not self.progress:can_start() then self:lives();return end
    local ids=self.context.repository:list('daily');if #ids==0 then self:toast('暂无每日挑战关卡');return end
    local id=ids[(CS.System.DateTime.UtcNow.Day-1)%#ids+1]
    if self.actions.daily then self.actions.daily(id,Progress.day()) else self.context:open_level(id,'daily') end
end
function UI:activities(page,group)
    page=page or 'missions';group=group or 1;self.progress:tick()
    local body=self:_dialog('活动与奖励',.80)
    self:_button(self.modal,body,'Missions','每日任务',{.03,.89,.48,.99},function() self:activities('missions',group) end,page=='missions' and 'greenButton' or 'button',32)
    self:_button(self.modal,body,'Pass','通行证',{.52,.89,.97,.99},function() self:activities('pass',1) end,page=='pass' and 'greenButton' or 'button',32)
    local a=self.progress.data.platformActivities
    if page=='missions' then
        for i=1,4 do local x=.035+(i-1)*.24;self:_button(self.modal,body,'Mission day '..i,'第 '..i..' 天',{x,.805,x+.22,.875},function() self:activities('missions',i) end,group==i and 'greenButton' or 'button',28) end
        for i,row in ipairs(Activity.missions[group]) do
            local y=.675-(i-1)*.115;local count=math.min(row.goal,a.counts[row.kind] or 0)
            local panel=self:_image(body,'Mission '..i,'paper',{.025,y,.975,y+.103})
            self:_label(panel,'Goal',(MISSION_NAMES[row.kind] or row.kind)..'  '..count..'/'..row.goal,30,{.03,.42,.69,.94})
            self:_label(panel,'Reward',Progress.reward_label(row.reward),24,{.03,.05,.69,.44})
            self:_button(self.modal,panel,'Claim',Progress.contains(a.claimed,group..':'..i) and '已领取' or '领取',{.72,.16,.97,.85},function()
                local ok,message=self.progress:claim_mission(group,i);self:activities('missions',group);self:toast(ok and '已领取：'..message or message)
            end,'greenButton',28)
        end
        self:_label(body,'Points','任务积分：'..tostring(a.blocks or 0),30,{.05,.145,.95,.20})
        for i,row in ipairs(Activity.milestones) do
            local x=.025+(i-1)*.245
            self:_button(self.modal,body,'Points reward '..i,row.goal..' 积分',{x,.035,x+.23,.14},function()
                local ok,message=self.progress:claim_mission_milestone(i);self:activities('missions',group);self:toast(ok and '已领取：'..message or message)
            end,'button',25)
        end
    else
        self:_label(body,'Keys','通行证钥匙：'..tostring(a.passKeys or 0),34,{.05,.81,.95,.88})
        local pages=math.ceil(#Activity.pass/5);group=math.max(1,math.min(pages,group))
        for cell=1,5 do
            local index=(group-1)*5+cell;local row=Activity.pass[index]
            if row then
                local required=0;for i=1,index do required=required+Activity.pass[i].keys end
                local y=.675-(cell-1)*.117;local panel=self:_image(body,'Pass reward '..index,'paper',{.025,y,.975,y+.107})
                self:_label(panel,'Requirement','第 '..row.id..' 阶 · '..required..' 钥匙',29,{.03,.43,.71,.95})
                self:_label(panel,'Reward',Progress.reward_label(row.free),24,{.03,.045,.71,.46})
                self:_button(self.modal,panel,'Claim',Progress.contains(a.passClaimed,index) and '已领取' or '领取',{.74,.15,.975,.86},function()
                    local ok,message=self.progress:claim_pass(index);self:activities('pass',group);self:toast(ok and '已领取：'..message or message)
                end,'greenButton',27)
            end
        end
        self:_button(self.modal,body,'Previous','上一页',{.04,.105,.30,.18},function() self:activities('pass',math.max(1,group-1)) end,'button',28)
        self:_label(body,'Page',group..' / '..pages,30,{.34,.105,.66,.18})
        self:_button(self.modal,body,'Next','下一页',{.70,.105,.96,.18},function() self:activities('pass',math.min(pages,group+1)) end,'button',28)
        self:_button(self.modal,body,'Premium pass','高级通行证',{.18,.012,.82,.09},function() self.services:purchase('premium_pass',function(_,message) self:toast(message) end) end,'button',30)
    end
end
function UI:cloud()
    local body=self:_dialog('云存档',.48)
    self:_label(body,'Description',self.progress.persistent and '本地进度已按平台账号独立保存。云同步需要游戏服务器提供接口。' or self.progress.status,36,{.07,.48,.93,.94})
    local message=self:_label(body,'Cloud status','',30,{.07,.29,.93,.49})
    local scope=self.modal
    self:_button(scope,body,'Upload','同步到云端',{.12,.07,.88,.25},function()
        message.text='正在同步…';self.services:upload(function(ok,data)
            if self.closed or scope.closed then return end
            message.text=ok and '云端已确认保存' or tostring(data)
        end)
    end,'greenButton',40)
end
function UI:_result(won)
    local body=self:_dialog(won and '恭喜通过' or '再试一次',won and .43 or .62)
    if won then
        self.dialog_close.gameObject:SetActive(false)
        self:_label(body,'Congratulations','恭喜通过！',55,{.10,.44,.90,.74})
        self:_fireworks(body)
        self:_button(self.modal,body,'Next','下一关',{.06,.08,.48,.32},function()
            self:_invoke(self.context.kind=='daily' and 'home' or 'next')
        end,'greenButton',46)
        self:_button(self.modal,body,'Home','返回主页',{.52,.08,.94,.32},function()self:_invoke('home')end,'button',44)
    else
        self:_label(body,'Message','重新规划方块顺序，再试一次',40,{.08,.63,.92,.87})
        self:_button(self.modal,body,'Retry','重新开始',{.15,.42,.85,.59},function()self:_invoke('retry')end,'greenButton',44)
        self:_button(self.modal,body,'Skip','跳过本关',{.15,.22,.85,.39},function()self:_invoke('skip')end,'button',42)
        self:_button(self.modal,body,'Home','返回主页',{.15,.02,.85,.19},function()self:_invoke('home')end,'button',42)
    end
end
function UI:information(key)
    local allowed={privacyPolicyUrl=true,termsUrl=true,adPrivacyUrl=true,accountDeletionUrl=true}
    if not allowed[key] then return end
    local ok,config=pcall(function()return self.assets:data('peripheral-services')end)
    local url=ok and config and config[key]
    if type(url)=='string' and url:match('^https://[^/]+') then UE.Application.OpenURL(url) else self:toast('此服务页面尚未配置') end
end
function UI:delete_data()
    local body=self:_dialog('账号与数据删除',.61)
    self:_label(body,'Warning','清除本机进度会删除关卡和道具记录，无法撤销。云端账号的数据删除请通过服务页面办理。',38,{.08,.43,.92,.91})
    self:_button(self.modal,body,'Local','确认清除本机进度',{.13,.23,.87,.39},function()self:_invoke('reset_local')end,'redButton',39)
    self:_button(self.modal,body,'Account','账号数据删除页面',{.13,.035,.87,.195},function()self:information('accountDeletionUrl')end,'button',36)
end
function UI:advertising()
    local body=self:_dialog('广告',.52)
    self:_label(body,'Status','广告服务入口。需要接入广告平台和服务端，奖励由服务验证发放。',36,{.09,.43,.91,.89})
    self:_button(self.modal,body,'Watch','观看广告',{.13,.22,.87,.40},function()
        self.services:rewarded('daily_gift',function(ok,message)if not self.closed then self:toast(ok and '广告已完成，奖励由服务验证发放' or tostring(message)) end end)
    end,'greenButton',40)
    self:_button(self.modal,body,'Privacy','广告隐私',{.13,.025,.87,.205},function()self:information('adPrivacyUrl')end,'button',38)
end
function UI:_clear_fireworks()
    if self.fireworks and alive(self.fireworks.layer) then UE.Object.Destroy(self.fireworks.layer.gameObject) end
    self.fireworks=nil
end
function UI:_fireworks(body)
    self:_clear_fireworks()
    local colors={Native.hex(0xFFD166),Native.hex(0xF49E4C),Native.hex(0xEF7364),Native.hex(0x69C6B5),Native.hex(0xB69BD6),Native.hex(0xFFF0CA)}
    local layer=node(body,'Celebration confetti',FULL);layer:SetAsFirstSibling()
    self.fireworks={layer=layer,at=self.elapsed,pieces={}}
    for i=0,55 do
        local u,v=((i*37+11)%101)/100,((i*53+7)%97)/96;local side=i%2==0 and 1 or -1
        local rt,image=self:_image(layer,'Confetti '..i,nil,{.5,.5,.5,.5},CLEAR)
        rt.sizeDelta=V2(7+5*u,i%3==0 and 22 or 11+7*v)
        self.fireworks.pieces[#self.fireworks.pieces+1]={rt=rt,image=image,x=side>0 and .08 or .92,y=.47,
            vx=side*(.22+.26*u),vy=1+.24*v,delay=math.floor(i/14)*.10+u*.08,phase=i*2.399963,
            life=1.8+.5*v,spin=side*(180+280*u),color=colors[i%#colors+1]}
    end
end
function UI:_animate_fireworks()
    local effect=self.fireworks;if not effect then return end
    if not alive(effect.layer) then self.fireworks=nil;return end
    local elapsed=self.elapsed-effect.at
    if elapsed>=3 then self:_clear_fireworks();return end
    local bounds=effect.layer.rect;local width,height=bounds.width,bounds.height
    for _,spark in ipairs(effect.pieces) do
        local age=elapsed-spark.delay;local c=spark.color
        if age<0 or age>spark.life then spark.image.color=CLEAR
        else
            local x=spark.x+spark.vx*age+math.sin(spark.phase+age*4)*.035*age
            local y=spark.y+spark.vy*age-.8*age*age
            spark.rt.anchoredPosition=V2((x-.5)*width,(y-.5)*height)
            spark.rt.localRotation=UE.Quaternion.Euler(0,0,spark.phase*180/math.pi+spark.spin*age)
            spark.rt.localScale=UE.Vector3(.15+.85*math.abs(math.cos(spark.phase+age*8)),1,1)
            local alpha=Native.clamp(age/.08,0,1)*Native.clamp((spark.life-age)/.45,0,1)*Native.clamp((y-.28)/.25,0,1)
            spark.image.color=Color(c.r,c.g,c.b,alpha)
        end
    end
end
function UI:_tutorial_active()
    return self.context and self.context.kind~='daily' and self.context.level.id<=2 and self.context.rules.d.status==0 and not self.home_visible and not self.modal and not self.context.loading
end
function UI:_find_tutorial_target()
    local rules=self.context.rules;local state=rules.d
    if state.beltCount>0 or state.pendingTransfers>0 then return -1 end
    local selected,best=-1,-math.huge
    for i=0,state.carriers.Count-1 do
        local c=state.carriers[i]
        if c.CanEmit and not c.packed and c.releasing==0 and rules:releasable_count(i)>0 then
            local colors,count={},0
            for slot=0,c.cargo.Count-1,rules.group_size do
                local color=state.pieces[c.cargo[slot]].color
                if not colors[color] then colors[color]=true;count=count+1 end
            end
            local score=(count>1 and 1000 or 0)-c.cargo.Count
            if score>best then selected,best=i,score end
        end
    end
    return selected
end
function UI:allows_release(index)
    if self.closed or self.services.invalidated or not self.context or self.context.loading or self.home_visible or self.modal or self.menu_paused or self.host_paused then return false end
    if self:_tutorial_active() then return index==self:_find_tutorial_target() end
    return true
end
function UI:tutorial_target_index() return self:_tutorial_active() and self:_find_tutorial_target() or -1 end
function UI:set_tutorial_target(index,r)
    self.tutorial_target=index;self.tutorial_rect=r and {r[1] or r.xmin,r[2] or r.ymin,r[3] or r.xmax,r[4] or r.ymax} or nil
    self:_tutorial_visual()
end
function UI:_tutorial_visual()
    local active=self:_tutorial_active() and self.tutorial_target>=0 and self.tutorial_rect~=nil and self.tutorial_target==self:_find_tutorial_target()
    local go=self.tutorial_scope.root.gameObject
    if go.activeSelf~=active then go:SetActive(active) end
    if not active then return end
    -- Presenter rect is screen-normalized. Convert to safe-area coordinates.
    local r=self.tutorial_rect;local safe=UE.Screen.safeArea;local w,h=UE.Screen.width,UE.Screen.height
    local xmin=math.max(0,(r[1]*w-safe.xMin)/safe.width-.012);local xmax=math.min(1,(r[3]*w-safe.xMin)/safe.width+.012)
    local ymin=math.max(.14,(r[2]*h-safe.yMin)/safe.height-.010);local ymax=math.min(.84,(r[4]*h-safe.yMin)/safe.height+.010)
    if xmax<=xmin or ymax<=ymin then self.tutorial_scope.root.gameObject:SetActive(false);return end
    -- Four transparent blockers leave only the highlighted carrier tappable.
    -- Top/settings and bottom controls remain usable; gameplay also checks allows_release.
    rect(self.tutorial_blockers[1],{0,.14,xmin,.84});rect(self.tutorial_blockers[2],{xmax,.14,1,.84})
    rect(self.tutorial_blockers[3],{xmin,.14,xmax,ymin});rect(self.tutorial_blockers[4],{xmin,ymax,xmax,.84})
    local x=math.min(.84,(xmin+xmax)*.5);local y=math.max(.16,ymin-.045)+math.sin(self.elapsed*5)*.007
    rect(self.tutorial_hand,{x,y,x+.13,y+.09});self.tutorial_hand.gameObject:SetActive(true)
end
function UI:_instruction()
    if not self.context then return '' end
    local context,state=self.context,self.context.rules.d
    if context.ui_state and nonempty(context.ui_state.instruction) then return context.ui_state.instruction end
    if context.kind~='daily' and context.level.id<=2 and state.status==0 then
        if state.beltCount>0 or state.pendingTransfers>0 then return '方块沿传送带移动，靠近同色托盘后自动收集' end
        if context.level.id==1 then return '点击手指指向的托盘，将红色方块收集到另一个托盘' end
        return state.packedCount==0 and '先点击混色托盘，收集最外侧的黄色方块' or '继续收集红色方块，装满剩余托盘'
    end
    return ''
end
function UI:refresh(context)
    if self.closed then return end
    if context then self.context=context end
    if self.context then
        local c,d=self.context,self.context.rules.d;local state=c.ui_state or {}
        set_text(self.level_label,(c.kind=='daily' and '每日挑战 ' or '第 ')..c.level.id..(c.kind=='daily' and '' or ' 关'))
        set_text(self.capacity_label,tostring(state.collected or d.beltOccupiedSlots or d.beltCount or 0)..'/'..tostring(state.total or d.beltCapacity or 0))
        local instruction=self:_instruction();set_text(self.instruction_label,instruction)
        local show_instruction=instruction~='' and not self.home_visible
        if self.instruction_visible~=show_instruction then self.instruction_bg.gameObject:SetActive(show_instruction);self.instruction_visible=show_instruction end
        for _,id in ipairs(BOOSTERS) do set_text(self.booster_counts[id],self.progress.data[id]) end
        local undo_enabled=(state.undo_depth or 0)>0 and not c.booster_busy and not c.loading
        if self.undo_enabled~=undo_enabled then
            self.undo_enabled=undo_enabled;self.booster_buttons.undo.interactable=undo_enabled
        end
        if not self.home_visible then
            local previous=self.last_status;self.last_status=d.status
            if (d.status==1 or d.status==2) and state.result_ready==true and self.result_generation~=c.generation then
                self.result_generation=c.generation;self:_result(d.status==1)
            elseif d.status==0 and previous==2 then
                self:close_dialog();self.result_generation=nil
            end
        end
    end
end
function UI:update(context,dt)
    if self.closed then return end
    if self.services.invalidated then self:_account_invalidated();return end
    self.context=context or self.context;self.elapsed=self.elapsed+math.max(0,dt or 0)
    self:_native_banner()
    if self.screen_width~=UE.Screen.width or self.screen_height~=UE.Screen.height then self:_safe_area() end
    if self.elapsed-self.last_tick>=1 then self.last_tick=self.elapsed;self.progress:tick() end
    if self.toast_until and self.elapsed>=self.toast_until then self.toast_until=nil;self.toast_panel.gameObject:SetActive(false) end
    if self.guides_pending and self.context and not self.context.loading and not self.home_visible and not self.modal then self.guides_pending=false;self:_next_guide() end
    self:refresh(self.context);self:_tutorial_visual();self:_animate_fireworks()
    self:_refresh_geometry()
end
function UI:_account_invalidated()
    if self.account_invalidated or self.closed then return end
    self.account_invalidated=true
    self.game_scope.root.gameObject:SetActive(false)
    if self.home_scope and alive(self.home_scope.root) then self.home_scope.root.gameObject:SetActive(false) end
    self.tutorial_scope.root.gameObject:SetActive(false)
    local body=self:_dialog('账号已切换',.43,function() end)
    self:_label(body,'Identity notice','当前游戏已暂停。请退出本游戏后重新进入，以加载新账号的进度。原账号存档不会被覆盖。',40,{.08,.15,.92,.90})
end
function UI:set_paused(value) self.host_paused=value==true end
function UI:dispose()
    if self.disposed then return end
    self:_clear_fireworks()
    self.closed=true
    self:_native_banner()
    local scopes={};for scope in pairs(self.scopes) do scopes[#scopes+1]=scope end
    for _,scope in ipairs(scopes) do self:_destroy_scope(scope) end
    if alive(self.root) then self.root.gameObject:SetActive(false);UE.Object.Destroy(self.root.gameObject) end
    if alive(self.event_system) then UE.Object.Destroy(self.event_system) end
    if self.owns_services then self.services:dispose() end
    if self.owns_progress then self.progress:dispose() end
    self.root=nil;self.context=nil;self.host=nil;self.assets=nil;self.actions=nil;self.image_records={}
    self.disposed=true
end
return UI
