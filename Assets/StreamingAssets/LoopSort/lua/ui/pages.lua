local V=CS.UnityEngine.Vector2
local UI={}
local Menu=require('ui.menu_theme')
local config=require('game.config')
local skin=require('ui.skin')
local first_levels=require('game.guide_levels')
local flow=require('game.flow')
local unlocks=require('game.unlocks')
local peripheral=require('game.peripheral')
local activities=require('game.activities')
local Signin=require('game.signin')
local SigninView=require('ui.signin')
local signin_views=setmetatable({},{__mode='k'})
local function vector(r,offset) return V(r[offset],r[offset+1]) end
local function art(h,parent,name,id,r,aspect) return h:Art(parent,name,id,vector(r,1),vector(r,3),aspect or false) end
local function label(h,parent,name,text,size,r) return h:Label(parent,name,tostring(text),math.floor(size+.5),vector(r,1),vector(r,3)) end
local function paragraph(h,parent,name,text,size,r) return h:Paragraph(parent,name,text,math.floor(size+.5),vector(r,1),vector(r,3)) end
local function button(h,parent,name,text,r,click,sprite,size)
    if not size then return h:Button(parent,name,text,vector(r,1),vector(r,3),click,sprite or 'button') end
    local b=h:Button(parent,name,'',vector(r,1),vector(r,3),click,sprite or 'button')
    label(h,b.transform,'Text',text,size/1.25,{.05,.04,.95,.96})
    return b
end
local function on(h,event,...) local callback=h[event];if callback then callback(...) end end
local function sized_dialog(h,title,height)
    h:Begin(title)
    h.DialogRoot.sizeDelta=V(960,height)
end
local function lower_title(root,offset)
    local title=root:Find('Dialog chrome/Title')
    offset=offset or 0
    title.anchorMin=V(.13,.865+offset)
    title.anchorMax=V(.87,.955+offset)
end
local function guide_title(root,offset)
    lower_title(root,offset)
    local title=root:Find('Dialog chrome/Title'):GetComponent(typeof(CS.UnityEngine.UI.Text))
    title.fontSize=Menu.font.guideTitle
    title.resizeTextForBestFit=false
end
local function color(name) return skin.color(name,CS.UnityEngine.Color.white) end
local function ordered_guides()
    local source=CS.LoopSort0.MechanicGuide.All;local guides={}
    for i=0,source.Length-1 do guides[#guides+1]=source[i] end
    table.sort(guides,function(a,b)
        local left,right=first_levels[a.id] or math.huge,first_levels[b.id] or math.huge
        if left~=right then return left<right end
        return a.id<b.id
    end)
    return guides
end
local ids={'undo','shuffle','belt','truck'}
local names={'撤回一步','打乱方块','传送带扩容','增加托盘'}
local cities={}
for i,area in ipairs(peripheral.config.areas) do cities[i]=area.name end
local area_pages=setmetatable({},{__mode='k'})
local ranking_pages=setmetatable({},{__mode='k'})
local tabs={'商店','地区','主页','排行','队伍'}
local home_views={}
local home_contexts=setmetatable({},{__mode='k'})

function UI.select_home_page(h,page)
    local context=home_contexts[h]
    if not context or not h.IsHome then return end
    h:Close();context.page=page
    for i=1,5 do
        context.pages[i].gameObject:SetActive(i==page)
        context.selections[i].gameObject:SetActive(i==page)
        context.labels[i].color=color('text')
        context.buttons[i].interactable=i~=page
    end
    context.title.text=page==3 and cities[context.area] or tabs[page]
    h:MarkSkinDirty()
end
local function rebuild_home(h,page)
    local context=home_contexts[h];local game=h.CurrentGame
    UI.home(h,game.Progress,context and context.play_level or game.Level.id,page)
end

function UI.home(h,progress,play_level)
    h:OpenHome();home_contexts[h]=nil;local root=h.HomeRoot
    h:HomeIllustration(root)
    button(h,root,'Settings','',{.034,.928,.153,.982},function()on(h,'OnPause')end,'settings')
    local items={
        {'Gift','每日礼包','candyGift',.085,function()h:PeripheralPage('daily_reward')end},
        {'Encyclopedia','图鉴','candyBook',.365,function()UI.mechanics(h)end},
        {'Ads','暂停横幅','candyVideo',.645,function()UI.advertising(h)end},
    }
    for _,item in ipairs(items) do
        local x=item[4]
        local b=button(h,root,item[1],'',{x,.235,x+.27,.365},item[5],'candyHomeChip')
        b.targetGraphic.color=CS.UnityEngine.Color.clear
        art(h,b.transform,'Caption tab','candyHomeChip',{.025,.015,.975,.33})
        art(h,b.transform,'Icon',item[3],{.015,.29,.985,1},true)
        label(h,b.transform,'Text',item[2],32,{.085,.04,.915,.29})
    end
    button(h,root,'Play','开始第 '..play_level..' 关',{.205,.116,.795,.193},function()on(h,'OnPlay')end,'greenButtonHome',66)
    button(h,root,'Levels','关卡回顾',{.29,.050,.71,.097},function()on(h,'OnLevels')end,'yellowButtonHome',37)
end
function UI.tutorial(h,guide,next,discovery)
    h:Toast('')
    h:Begin(guide.title);local root=h.DialogRoot
    guide_title(root)
    local paper=art(h,root,'Illustration paper','mechanicTile',{.12,.39,.88,.80}).rectTransform
    local inset=.12+(.22-.12)*(1-math.max(0,math.min(1,guide.scale)))
    art(h,paper,'Feature '..guide.id,guide.sprite,{inset,.19,1-inset,.96},true)
    if discovery then label(h,paper,'Discovery','新机制解锁',25,{.08,.01,.92,.16}) end
    local description=paragraph(h,root,'Description',guide.description,Menu.font.guideDescription/1.25,{.07,.17,.93,.385})
    description.resizeTextForBestFit=false
    button(h,root,'Got it','知道了',{.22,.045,.78,.165},next,'greenButton');h:SetDialogClose(next)
end
function UI.mechanics(h)
    h:Toast('')
    h:Begin('机制图鉴');local root=h.DialogRoot;local guides={}
    guide_title(root,.02)
    for _,guide in ipairs(ordered_guides()) do if unlocks.mechanic(h.CurrentGame.Progress,guide.id) then guides[#guides+1]=guide end end
    root.sizeDelta=V(960,1400)
    for index,guide in ipairs(guides) do
        local i=index-1;local x=.075+i%3*.29;local y=.70-math.floor(i/3)*.192
        local b=button(h,root,'Guide '..guide.id,'',{x,y,x+.27,y+.177},function()h:FeatureTutorial(guide,function()h:Mechanics()end,false)end,'mechanicTile')
        art(h,b.transform,'Illustration',guide.sprite,{.15,.45,.85,.925},true)
        local name=label(h,b.transform,'Name',guide.title,Menu.font.guideName/1.25,{.02,.025,.98,.45})
        name.resizeTextForBestFit=false
    end
    label(h,root,'Footer','已发现 '..#guides..' 种玩法',29,{.1,.045,.9,.10})
end
local function shop_content(h,root)
    local p=h.CurrentGame.Progress
    h:IconValue(root,'Wallet','goldPile','金币  '..p.coins,31,V(.15,.74),V(.85,.82))
    for i=1,4 do
        local index=i;local y=.565-(i-1)*.155
        local row=art(h,root,'Offer '..(i-1),'settingsInner',{.085,y,.915,y+.14}).rectTransform
        art(h,row,'Booster',ids[i],{.025,.07,.20,.93},true)
        label(h,row,'Name',names[i],28,{.24,.52,.61,.90}).color=color('text')
        label(h,row,'Stock','拥有 '..p[ids[i]]..' 个',23,{.24,.12,.61,.45}).color=color('text')
        button(h,row,'Buy',config.booster_prices[ids[i]]..' 金币',{.64,.15,.97,.85},function()h:BuyBooster(index-1)end,'greenButton')
    end
    label(h,root,'Store footer','使用金币补充道具',25,{.1,.035,.9,.08})
end
home_views[1]=shop_content
function UI.shop(h)
    if h.IsHome then rebuild_home(h,1);return end
    h:Begin('道具商店','shopBanner');shop_content(h,h.DialogRoot)
end
function UI.buy(h,index)
    local i=index+1;h:Begin(names[i]);local root=h.DialogRoot
    local stand=art(h,root,'Stand','stand',{.1,.40,.9,.80}).rectTransform
    art(h,stand,'Booster',ids[i],{.22,.10,.78,.94},true)
    local descriptions={'撤回上一次操作，恢复方块和托盘状态。','重新排列一个托盘中的方块。','让传送带多容纳一个完整方块。','在本关增加一个可用的空托盘。'}
    paragraph(h,root,'Description',descriptions[i],31,{.12,.245,.88,.375})
    button(h,root,'Confirm',config.booster_prices[ids[i]]..' 金币  ·  购买 1 个',{.13,.115,.87,.225},function()
        if h.OnBuyBooster and h.OnBuyBooster(ids[i]) then h:Shop();h:Toast('已购买'..names[i]) else h:Toast('金币不足') end
    end,'greenButton')
    button(h,root,'Back','返回商店',{.26,.025,.74,.095},function()h:Shop()end,'blueButton')
end
local function areas_content(h,root)
    local page=area_pages[h] or 0;local areas=peripheral.config.areas
    for offset=0,3 do
        local index=page*4+offset+1;local area=areas[index]
        if area then
            local y=.61-offset*.155;local unlocked=h.CurrentGame.Progress.highestUnlocked>=area.unlock
            local row=art(h,root,'Area '..index,'paper',{.07,y,.93,y+.145}).rectTransform
            label(h,row,'Title',area.name,29,{.04,.50,.57,.93})
            label(h,row,'Unlock','第 '..area.unlock..' 关解锁',21,{.04,.08,.57,.45})
            local b=button(h,row,'Choose',h.CurrentGame.Progress.areaId==index and '当前地区' or unlocked and '前往' or '未解锁',{.62,.15,.96,.85},function()
                if peripheral.select_area(h.CurrentGame,index) then rebuild_home(h,2) end
            end,unlocked and 'greenButton' or 'boosterGrey');b.interactable=unlocked
        end
    end
    button(h,root,'Previous','<',{.12,.06,.29,.13},function()area_pages[h]=math.max(0,page-1);rebuild_home(h,2)end)
    label(h,root,'Page',(page+1)..' / '..math.ceil(#areas/4),25,{.34,.06,.66,.13})
    button(h,root,'Next','>',{.71,.06,.88,.13},function()area_pages[h]=math.min(math.ceil(#areas/4)-1,page+1);rebuild_home(h,2)end)
end
home_views[2]=areas_content
function UI.areas(h)
    if h.IsHome then UI.select_home_page(h,2);return end
    h:Begin('选择地区');areas_content(h,h.DialogRoot)
end
local function ranking_content(h,root)
    local game=h.CurrentGame;local p=game.Progress
    if game.ServicesConfigured then
        for i,scope in ipairs({'world','country','weekly'}) do
            local x=.07+(i-1)*.29
            local b=button(h,root,scope,({'世界','国家','周榜'})[i],{x,.70,x+.27,.79},function()ranking_pages[h]=0;game:FetchLeaderboard(scope);rebuild_home(h,4)end);b.interactable=not game.ServiceBusy
        end
        if game.LeaderboardCount==0 then paragraph(h,root,'Status',game.ServiceStatus~='' and game.ServiceStatus or '选择榜单，从服务器加载真实排名',26,{.09,.35,.91,.65}) end
        local page=math.max(0,math.min(ranking_pages[h] or 0,math.max(0,math.ceil(game.LeaderboardCount/5)-1)))
        for i=0,math.min(4,game.LeaderboardCount-page*5-1) do
            label(h,root,'Player '..i,game:LeaderboardRow(page*5+i),24,{.08,.58-i*.07,.92,.65-i*.07})
        end
        if game.LeaderboardCount>5 then
            button(h,root,'Prev','<',{.12,.22,.28,.28},function()ranking_pages[h]=math.max(0,page-1);rebuild_home(h,4)end)
            label(h,root,'Page',(page+1)..' / '..math.ceil(game.LeaderboardCount/5),22,{.30,.22,.70,.28})
            button(h,root,'Next','>',{.72,.22,.88,.28},function()ranking_pages[h]=math.min(math.ceil(game.LeaderboardCount/5)-1,page+1);rebuild_home(h,4)end)
        end
        button(h,root,'Submit',game.PeripheralAccountConnected and '提交成绩' or '提交成绩需登录',{.24,.115,.76,.205},function()game:SubmitLeaderboard()end,'greenButton').interactable=not game.ServiceBusy and game.PeripheralAccountConnected
    else
        art(h,root,'Medal','rankingNav',{.35,.50,.65,.75},true)
        paragraph(h,root,'Service unavailable','尚未配置排行榜服务器\n连接自己的服务后，可查看世界、国家和周榜',29,{.08,.28,.92,.48})
    end
    label(h,root,'Local record','本机记录：第 '..p.highestUnlocked..' 关 · 通关 '..p.completedLevels.Count..' 关',24,{.08,.04,.92,.11})
end
home_views[4]=ranking_content
function UI.ranking(h)
    if h.IsHome then UI.select_home_page(h,4);return end
    h:Begin('闯关记录');ranking_content(h,h.DialogRoot)
end
local function teams_content(h,root)
    art(h,root,'Teams','teamsNav',{.26,.40,.74,.78},true)
    label(h,root,'Description','队伍功能尚未开放',35,{.12,.23,.88,.38})
end
home_views[5]=teams_content
function UI.teams(h)
    if h.IsHome then UI.select_home_page(h,5);return end
    h:Begin('队伍');teams_content(h,h.DialogRoot)
end
function UI.daily(h)
    UI.peripheral_calendar(h)
end
function UI.profile(h)
    h:Begin('个人资料');local root=h.DialogRoot
    local selected=h.CurrentGame.Progress.avatarIndex;local portrait_buttons={}
    for i=0,h.CurrentGame.AvatarCount-1 do
        local index=i;local x=.19+(i%3)*.22;local y=.65-math.floor(i/3)*.14
        local b=button(h,root,'Avatar '..i,'',{x,y,x+.18,y+.125},function()
            selected=index;for key,view in pairs(portrait_buttons) do view.targetGraphic.color=key==selected and color('mint') or CS.UnityEngine.Color.white end
        end,'paper');portrait_buttons[i]=b
        b.targetGraphic.color=selected==i and color('mint') or CS.UnityEngine.Color.white
        art(h,b.transform,'Portrait','avatar',{.08,.08,.92,.92},true).sprite=h.CurrentGame:ProfileAvatar(i)
    end
    local input=h:Input(root,'昵称',h.CurrentGame.Progress.playerName,V(.14,.245),V(.86,.345));input.characterLimit=12
    button(h,root,'Save','保存资料',{.21,.12,.79,.23},function()
        local name=h:Trim(input.text)
        if name=='' then h:Toast('请输入昵称');return end
        h.CurrentGame.Progress.avatarIndex=selected
        on(h,'OnName',name);on(h,'OnHome')
    end,'greenButton')
    button(h,root,'Cloud','云存档',{.27,.03,.73,.10},function()h:PeripheralPage('cloud')end,'blueButton')
end
local function settings_row(h,root,name,icon,text,index,total)
    local y=1-index/total
    local artwork=art(h,root,name..' row','menuRow',{0,y,1,y+1/total})
    artwork.color=CS.UnityEngine.Color.clear
    local row=artwork.rectTransform
    if index<total then
        local line=h:Image(row,'Separator',nil,V(.043,0),V(.957,.013))
        line.color=CS.UnityEngine.Color(.80,.77,.88,1)
    end
    art(h,row,'Icon',icon,{.042,.16,.15,.84},true)
    label(h,row,'Label',text,39,{.175,.09,.67,.91}).alignment=CS.UnityEngine.TextAnchor.MiddleLeft
    return row
end
function UI.pause(h,p)
    local home=h.IsHome
    sized_dialog(h,home and '设置' or '暂停',home and 1270 or 1240);local root=h.DialogRoot
    local inset=art(h,root,'Settings inset','settingsInner',{.07,home and .356 or .46,.93,home and .82 or .81}).rectTransform
    local audio={{'music','music','音乐'},{'sound','sound','音效'},{'haptics','haptic','振动'}}
    for i,item in ipairs(audio) do
        local key=item[1]
        local row=settings_row(h,inset,key,item[2],item[3],i,home and 4 or 3)
        button(h,row,'Toggle','',{.76,.20,.959,.80},function()on(h,'OnToggle',key)end,p[key] and 'toggleOn' or 'toggleOff')
    end
    if not home then
        button(h,root,'Continue','继续游戏',{.07,.292,.93,.433},function()on(h,'OnCancel')end,'greenButton',52)
        button(h,root,'Retry','重新开始',{.07,.135,.484,.276},function()on(h,'OnRetry')end,'candyPurpleButton',35)
        button(h,root,'Home','返回主页',{.516,.135,.93,.276},function()on(h,'OnHome')end,'candyPurpleButton',35)
        h:TextButton(root,'Help','玩法图鉴',V(.25,.045),V(.68,.116),function()on(h,'OnHelp')end)
        button(h,root,'Help icon','',{.68,.050,.76,.107},function()on(h,'OnHelp')end,'helpButton')
        return
    end
    local row=settings_row(h,inset,'Language','language','语言',4,4)
    button(h,row,'Language',h.CurrentGame.SelectedLanguageName,{.60,.17,.96,.84},function()UI.languages(h)end,'languageButton',35)
    for i,item in ipairs({{'privacyPolicyUrl','隐私政策'},{'termsUrl','用户协议'},{'adPrivacyUrl','广告隐私设置'},{'accountDeletionUrl','账号与数据删除'}}) do
        local key,text=item[1],item[2];local x=i%2==1 and .07 or .52;local y=i<=2 and .208 or .097
        button(h,root,key,text,{x,y,x+.41,y+.101},function()
            if key=='accountDeletionUrl' then UI.delete_data(h)
            elseif key=='adPrivacyUrl' then h.CurrentGame:AdvertisingPrivacy()
            else h:OpenInformation(key) end
        end,'button',35)
    end
    label(h,root,'Version','版本 '..h.CurrentGame.ClientVersion,27,{.16,.028,.84,.083})
end
function UI.languages(h)
    sized_dialog(h,'选择语言',1080);local root=h.DialogRoot;local game=h.CurrentGame
    for i,item in ipairs({{'en','English'},{'zh-CN','简体中文'},{'hi','हिन्दी'},{'pt-BR','Português'}}) do
        local code,name=item[1],item[2];local y=.68-(i-1)*.175
        local selected=game.Progress.language==code
        local control=button(h,root,'Language '..code,name,{.075,y,.925,y+.135},function()game:SelectLanguage(code)end,'button',44)
        if selected then control.targetGraphic.color=CS.UnityEngine.Color(.92,.86,1,1) end
    end
    h:SetDialogClose(function()UI.pause(h,game.Progress)end)
end
function UI.result(h,won)
    h:Begin(won and '恭喜通过' or '再试一次吧！',won and 'candyWinRibbon' or 'candyFailRibbon')
    local root=h.DialogRoot
    if won then art(h,root,'Candy light','candyGlow',{-.13,.31,1.13,.84}) end
    art(h,root,'Candy gift',won and 'candyWinGift' or 'candyFailGift',won and {.105,.305,.895,.710} or {.17,.312,.83,.675},true)
    button(h,root,'Home','返回',{.067,.217,.478,.287},function()on(h,'OnHome')end,'candyPurpleButton',48)
    if won then
        button(h,root,'Next','前往下一关',{.522,.217,.933,.287},function()on(h,'OnContinue')end,'greenButton',48)
        h:Celebrate()
        return
    end
    button(h,root,'Retry','重新挑战',{.522,.217,.933,.287},function()on(h,'OnRetry')end,'greenButton',48)
    local skip=button(h,root,'Skip','',{.23,.137,.77,.203},function()h.CurrentGame:SkipLevelWithAd()end)
    art(h,skip.transform,'Video','advert',{.075,.22,.24,.80},true)
    label(h,skip.transform,'Caption','看广告跳关',39,{.25,.06,.95,.94})
    skip.interactable=h.CurrentGame.CanSkipLevel and not h.CurrentGame.CommerceBusy
end
function UI.levels(h,catalog,current,page,filter)
    filter=filter or 'all';local progress=h.CurrentGame.Progress;local levels={}
    for i=0,catalog.levels.Length-1 do
        local item=catalog.levels[i]
        if item.id<=unlocks.level(progress) and (filter=='all' or (filter=='completed' and progress.completedLevels:Contains(item.id)) or (filter=='skipped' and progress.skippedLevels:Contains(item.id))) then levels[#levels+1]=item.id end
    end
    local pages=math.max(1,math.ceil(#levels/20));page=math.max(0,math.min(pages-1,page or 0))
    h:Begin('选择关卡');local root=h.DialogRoot;root.sizeDelta=V(960,1400)
    for i,item in ipairs({{'all','全部'},{'completed','已通关'},{'skipped','已跳过'}}) do
        local key=item[1];local x=.04+(i-1)*.315
        button(h,root,key,item[2],{x,.81,x+.29,.90},function()UI.levels(h,catalog,current,0,key)end,filter==key and 'yellowButton' or 'button',Menu.font.tab)
    end
    for cell=0,19 do
        local id=levels[page*20+cell+1]
        if id then local x=.04+(cell%5)*.19;local y=.635-math.floor(cell/5)*.15
            local tile=button(h,root,'Level '..id,tostring(id),{x,y,x+.17,y+.12},function()on(h,'OnLevel',id)end,'button',Menu.font.level)
            tile.targetGraphic.color=CS.UnityEngine.Color.white
            local colors=tile.colors;colors.normalColor=CS.UnityEngine.Color.white;colors.disabledColor=CS.UnityEngine.Color.white;tile.colors=colors
            if not progress.completedLevels:Contains(id) and progress.skippedLevels:Contains(id) then
                art(h,tile.transform,'Status','skipBadge',{.68,-.03,1.05,.34},true)
            end
        end
    end
    if #levels==0 then label(h,root,'Empty','暂无记录',34,{.12,.36,.88,.53}) end
    label(h,root,'Page',(page+1)..' / '..pages,32,{.35,.05,.65,.12})
    button(h,root,'Prev','上一页',{.05,.035,.32,.13},function()UI.levels(h,catalog,current,page-1,filter)end,'button',Menu.font.tab).interactable=page>0
    button(h,root,'Next','下一页',{.68,.035,.95,.13},function()UI.levels(h,catalog,current,page+1,filter)end,'button',Menu.font.tab).interactable=page<pages-1
end
function UI.gm(h)
    if not(CS.UnityEngine.Application.isEditor or CS.UnityEngine.Debug.isDebugBuild) then return end
    local game=h.CurrentGame
    h:Begin('GM 工具');local root=h.DialogRoot;root.sizeDelta=V(960,1450)
    button(h,root,'Grant boosters','道具设为 99',{.05,.82,.485,.92},function()
        local progress=game.Progress
        for _,id in ipairs({'undo','shuffle','belt','truck'}) do progress[id]=99 end
        game:SaveProgress();h:Toast('撤回、打乱、扩容、加车已设为 99')
    end,'button',36)
    button(h,root,'Clear boosters','清空道具',{.515,.82,.95,.92},function()game:GmClearBoosters()end,'button',36)
    button(h,root,'Force failure','当前关卡失败',{.05,.71,.485,.80},function()game:GmFinishLevel(false)end,'button',35)
    button(h,root,'Force win','当前关卡通关',{.515,.71,.95,.80},function()game:GmFinishLevel(true)end,'button',35)
    button(h,root,'Unlock all levels','解锁所有关卡',{.05,.595,.95,.675},function()game:GmUnlockAllLevels()end,'button',35)
    local input=h:Input(root,'输入主关卡号',tostring(game.Level.id),V(.05,.50),V(.63,.58))
    input.contentType=CS.UnityEngine.UI.InputField.ContentType.IntegerNumber;input.characterLimit=5
    local function jump(value)
        local id=tonumber(value)
        if not id or id~=math.floor(id) or id<1 then h:Toast('请输入有效的整数关卡号');return end
        for i=0,game.Catalog.levels.Length-1 do
            if game.Catalog.levels[i].id==id then on(h,'OnLevel',id);return end
        end
        h:Toast('没有第 '..tostring(id)..' 关')
    end
    button(h,root,'Jump','跳转',{.67,.50,.95,.58},function()jump(input.text)end,'button',42)
    label(h,root,'Mechanic levels','机制首次出现关卡',32,{.05,.445,.95,.49})
    local guides=ordered_guides()
    for index,guide in ipairs(guides) do
        local i=index-1;local level=first_levels[guide.id]
        if level then
            local x=.035+(i%2)*.48;local y=.375-math.floor(i/2)*.068
            button(h,root,'GM mechanic '..guide.id,guide.title..' · '..level,{x,y,x+.455,y+.058},function()jump(level)end,'button',30)
        end
    end
end
function UI.notice(h,message)
    h:Begin('特殊关卡');local root=h.DialogRoot
    paragraph(h,root,'Details',message,27,{.12,.30,.88,.76})
    button(h,root,'Back','返回',{.18,.08,.82,.25},function()on(h,'OnCancel')end)
end
local life_views=setmetatable({},{__mode='k'})
function UI.peripheral_activities(h)
    local game=h.CurrentGame;if activities.tick(game) then game:SaveProgress() end
    h:Begin('活动与奖励');local root=h.DialogRoot
    art(h,root,'Activities gift','gift',{.34,.59,.66,.80},true)
    button(h,root,'Weekly missions','任务挑战 · '..activities.remaining(game,false),{.13,.40,.87,.55},function()h:PeripheralPage('missions')end,'greenButton')
    button(h,root,'Battle pass','通行证 · '..activities.remaining(game,true),{.13,.22,.87,.37},function()h:PeripheralPage('pass')end,'blueButton')
    label(h,root,'Local cycle','当前为本地活动周期，奖励和任务来自原包配置。',24,{.1,.09,.9,.17})
end
function UI.peripheral_missions(h,argument)
    local game=h.CurrentGame;if activities.tick(game) then game:SaveProgress() end
    local unlocked=activities.day(game);local day=math.max(1,math.min(4,tonumber(argument) or unlocked))
    h:Begin('任务挑战');local root=h.DialogRoot;root.sizeDelta=V(960,1400)
    for i=1,4 do
        local index=i;local x=.08+(i-1)*.215
        button(h,root,'Day tab '..i,'第 '..i..' 天',{x,.74,x+.195,.81},function()h:PeripheralPage('missions',tostring(index))end,i==day and 'greenButton' or 'paper')
    end
    for i,row in ipairs(activities.config.missions[day]) do
        local index=i;local y=.61-(i-1)*.105;local claimed=activities.claimed_mission(game,day,index)
        local value=activities.quantity(game,row.kind);local ready=day<=unlocked and value>=row.goal and not claimed
        local panel=art(h,root,'Mission '..i,'paper',{.07,y,.93,y+.095}).rectTransform
        label(h,panel,'Mission',activities.mission_name(row)..' '..math.min(value,row.goal)..' / '..row.goal,25,{.025,.49,.74,.94})
        label(h,panel,'Reward',peripheral.reward_label(row.reward),21,{.025,.06,.74,.47})
        local caption=claimed and '已领取' or day>unlocked and '未开放' or ready and '领取' or row.kind=='multiplier' and not game.RewardedAdsConfigured and '广告未配置' or '进行中'
        button(h,panel,'Claim',caption,{.76,.16,.98,.84},function()activities.claim_mission(game,day,index)end,ready and 'greenButton' or 'paper').interactable=ready
    end
    button(h,root,'Milestones','任务积分 '..activities.quantity(game,'blocks')..' · 阶段奖励',{.16,.065,.84,.155},function()h:PeripheralPage('mission_rewards')end,'yellowButton')
    label(h,root,'Local duration','本地活动剩余 '..activities.remaining(game,false),22,{.1,.015,.9,.05})
end
function UI.peripheral_mission_rewards(h)
    local game=h.CurrentGame;h:Begin('任务阶段奖励');local root=h.DialogRoot
    label(h,root,'Blocks','任务积分：'..activities.quantity(game,'blocks'),35,{.1,.73,.9,.81})
    for i,row in ipairs(activities.config.milestones) do
        local index=i;local y=.56-(i-1)*.135;local claimed=activities.claimed_milestone(game,i)
        local panel=art(h,root,'Milestone '..i,'paper',{.09,y,.91,y+.12}).rectTransform
        paragraph(h,panel,'Reward',row.goal..' 积分\n'..peripheral.reward_label(row.reward),25,{.03,.05,.72,.95})
        local ready=not claimed and activities.quantity(game,'blocks')>=row.goal
        button(h,panel,'Claim',claimed and '已领取' or '领取',{.75,.15,.97,.85},function()activities.claim_milestone(game,index)end,ready and 'greenButton' or 'paper').interactable=ready
    end
    button(h,root,'Back','返回任务',{.25,.035,.75,.12},function()h:PeripheralPage('missions')end,'blueButton')
end
function UI.peripheral_pass(h,argument)
    local game=h.CurrentGame;if activities.tick(game) then game:SaveProgress() end
    local rows=activities.config.pass;local page=math.max(0,math.min(math.ceil(#rows/5)-1,tonumber(argument) or 0))
    h:Begin('通行证');local root=h.DialogRoot;root.sizeDelta=V(960,1400)
    label(h,root,'Progress','已解锁 '..activities.pass_level(game)..' 档 · 当前钥匙 '..activities.pass_keys(game),30,{.08,.76,.92,.82})
    if activities.premium(game) then label(h,root,'Premium','高级轨道已解锁',25,{.08,.70,.92,.75})
    elseif game.PremiumPaymentsConfigured then
        button(h,root,'Premium','解锁高级轨道 '..game.PremiumPassPrice,{.18,.70,.82,.755},function()game:PurchasePremiumPass()end,'yellowButton').interactable=not game.CommerceBusy
    else label(h,root,'Premium','免费轨道可领取 · 高级轨道尚未接入支付服务',23,{.08,.70,.92,.75}) end
    for offset=0,4 do
        local index=page*5+offset+1;local row=rows[index]
        if row then
            local y=.58-offset*.108;local claimed=activities.claimed_pass(game,index)
            local ready=index<=activities.pass_level(game) and not claimed
            local panel=art(h,root,'Tier '..index,'paper',{.07,y,.93,y+.10}).rectTransform
            label(h,panel,'Free','第 '..index..' 档：'..peripheral.reward_label(row.free),23,{.025,.50,.74,.95})
            label(h,panel,'Paid','高级：'..peripheral.reward_label(row.paid),19,{.025,.04,.74,.48})
            button(h,panel,'Claim',claimed and '已领取' or ready and '领取' or row.keys..' 钥匙',{.76,.53,.98,.95},function()activities.claim_pass(game,index,page)end,ready and 'greenButton' or 'paper').interactable=ready
            local paid_claimed=activities.claimed_paid(game,index);local paid_ready=activities.premium(game) and index<=activities.pass_level(game) and not paid_claimed
            button(h,panel,'Paid claim',paid_claimed and '已领取' or activities.premium(game) and '高级奖励' or '未解锁',{.76,.05,.98,.47},function()activities.claim_paid(game,index,page)end,paid_ready and 'yellowButton' or 'paper').interactable=paid_ready
        end
    end
    button(h,root,'Prev','<',{.12,.05,.30,.12},function()h:PeripheralPage('pass',tostring(math.max(0,page-1)))end)
    label(h,root,'Page',(page+1)..' / '..math.ceil(#rows/5),24,{.32,.05,.68,.12})
    button(h,root,'Next','>',{.70,.05,.88,.12},function()h:PeripheralPage('pass',tostring(math.min(math.ceil(#rows/5)-1,page+1)))end)
    label(h,root,'Expiry','本地活动剩余 '..activities.remaining(game,true),21,{.1,.005,.9,.04})
end
function UI.peripheral_lives(h)
    local game=h.CurrentGame;game:SetPaused(true);peripheral.tick(game)
    h:Begin('生命补充');local root=h.DialogRoot
    art(h,root,'Heart','heart',{.32,.50,.68,.78},true)
    local amount=label(h,root,'Lives',peripheral.life_label(game)..' / '..peripheral.config.max_lives,44,{.16,.39,.84,.49})
    local timer=label(h,root,'Recovery',peripheral.life_detail(game),29,{.10,.29,.90,.38})
    paragraph(h,root,'Rule','每 30 分钟恢复一颗生命；无限生命期间失败不扣除生命。',27,{.12,.18,.88,.28})
    local refill=button(h,root,'Refill',peripheral.config.refill_cost..' 金币 · 补满',{.18,.055,.82,.16},function()peripheral.refill(game)end,'greenButton')
    refill.interactable=game.Progress.lives<peripheral.config.max_lives
    life_views[h]={amount=amount,timer=timer,refill=refill}
end
function UI.peripheral_calendar(h)
    local game=h.CurrentGame;game:SetPaused(true);h:Begin('每日挑战');local root=h.DialogRoot;root.sizeDelta=V(960,1400)
    local month=game.CurrentMonth;local count=peripheral.daily_count(game,month)
    label(h,root,'Month',month..'  ·  已完成 '..count..' 天',33,{.10,.76,.90,.83})
    for i,day in ipairs({'一','二','三','四','五','六','日'}) do
        local x=.075+(i-1)*.122;label(h,root,'Weekday '..i,day,25,{x,.70,x+.11,.75})
    end
    local offset=game:CalendarOffset(month)
    for day=1,game:CalendarDays(month) do
        local date=month..'-'..string.format('%02d',day);local cell=day-1+offset
        local x=.075+(cell%7)*.122;local y=.635-math.floor(cell/7)*.060
        local completed=game.Progress.dailyCompleted:Contains(date);local failed=game.Progress.dailyFailed:Contains(date)
        local b=button(h,root,'Day '..day,tostring(day),{x,y,x+.11,y+.055},function()game:StartDaily(date)end,completed and 'greenButton' or failed and 'redButton' or 'paper')
        b.interactable=date<=game.TodayKey
    end
    label(h,root,'Daily source','每日挑战使用独立关卡；已错过的本月日期可补玩。',23,{.08,.270,.92,.320})
    for i,row in ipairs(peripheral.config.daily_milestones) do
        local index=i;local claimed=game.Progress.dailyClaimed:Contains(month..':'..row.days)
        local x=.09+((i-1)%2)*.43;local y=.155-math.floor((i-1)/2)*.105
        local b=button(h,root,'Milestone '..i,row.days..' 天 · '..(claimed and '已领取' or peripheral.reward_label(row.reward)),{x,y,x+.39,y+.09},function()peripheral.claim_daily_milestone(game,index)end,claimed and 'boosterGrey' or 'greenButton')
        b.interactable=not claimed and count>=row.days
    end
end
function UI.peripheral_daily_reward(h)
    local game=h.CurrentGame;local cached=signin_views[h]
    if h.HasDialog and h.DialogTitle=='七日签到' and cached and cached.root==h.DialogRoot then
        cached.view.update(Signin.state(game.Progress,game.TodayKey),game.CommerceBusy);return false
    end
    h:Begin('七日签到');local root=h.DialogRoot
    local function tint(hex) return CS.LoopSort0.ArtTheme.Hex(hex) end
    local view=SigninView.draw({
        art=function(parent,name,id,r,aspect)return art(h,parent,name,id,r,aspect).rectTransform end,
        card=function(parent,name,r)
            local surface=art(h,parent,name,'signinCard',r)
            return surface.rectTransform,surface
        end,
        paint=function(view,hex)view.color=tint(hex)end,
        label=function(parent,name,text,size,r,hex)local textView=label(h,parent,name,text,size,r);textView.color=tint(hex);return textView end,
        button=function(parent,name,text,r,click,enabled)
            local control=button(h,parent,name,'',r,click,'greenButton');control.interactable=enabled
            return control,label(h,control.transform,'Text',text,43,{.05,.04,.95,.96})
        end,
    },root,Signin.state(game.Progress,game.TodayKey),function()peripheral.claim_daily_reward(game)end,function()signin_views[h]=nil;h:Close()end,game.CommerceBusy)
    signin_views[h]={root=root,view=view}
end
function UI.peripheral_booster_lesson(h,id)
    local index;for i,key in ipairs(ids) do if key==id then index=i;break end end
    if not index then return end
    h:Begin(names[index]);local root=h.DialogRoot
    art(h,root,'Booster',id,{.32,.48,.68,.77},true)
    local descriptions={'撤回上一次操作，把方块恢复到操作之前的位置。','选择一个混色托盘，重新排列托盘内方块。','给传送带增加一整块容量，拥堵失败后也能继续。','在关卡预留位置添加一个空托盘，增加收集空间。'}
    paragraph(h,root,'Lesson',descriptions[index],31,{.12,.25,.88,.44})
    button(h,root,'Try','开始使用',{.20,.08,.80,.21},function()peripheral.accept_booster(h.CurrentGame,id)end,'greenButton')
end
function UI.peripheral_cloud(h)
    local game=h.CurrentGame;h:Begin('云存档');local root=h.DialogRoot
    art(h,root,'Cloud icon','avatar',{.36,.62,.64,.79},true)
    local configured=game.ServicesConfigured
    paragraph(h,root,'Status',not configured and '尚未连接自己的云存档服务\n本机进度会继续正常保存' or not game.PeripheralAccountConnected and '云存档服务已配置\n登录服务尚未接入，没有模拟登录账户' or (game.ServiceStatus~='' and game.ServiceStatus or '上传本机进度，或下载同一账户的云存档。'),29,{.12,.43,.88,.64})
    local up=button(h,root,'Upload','上传本机存档',{.14,.28,.86,.40},function()game:UploadCloudSave();UI.peripheral_cloud(h)end,'greenButton')
    local down=button(h,root,'Download','下载云存档',{.14,.14,.86,.26},function()game:DownloadCloudSave();UI.peripheral_cloud(h)end,'blueButton')
    up.interactable=configured and game.PeripheralAccountConnected and not game.ServiceBusy;down.interactable=up.interactable
    if game.CloudRestoreAvailable then button(h,root,'Restore','确认恢复（先备份本机）',{.14,.035,.86,.125},function()game:RestoreCloudSave()end,'yellowButton') end
end
function UI.refresh_peripheral(h,game)
    if h.HomeLifeLabel then h.HomeLifeLabel.text=peripheral.life_label(game) end
    local view=life_views[h]
    if view and h.HasDialog and h.DialogTitle=='生命补充' then
        view.amount.text=peripheral.life_label(game)..' / '..peripheral.config.max_lives
        view.timer.text=peripheral.life_detail(game);view.refill.interactable=game.Progress.lives<peripheral.config.max_lives
    end
end
function UI.service_refresh(h,game)
    if h.HasDialog and h.DialogTitle=='云存档' then UI.peripheral_cloud(h)
    elseif h.IsHome and home_contexts[h] and home_contexts[h].page==4 then rebuild_home(h,4)
    else h:Toast(game.ServiceStatus) end
end
function UI.refresh(h,game)
    h.LevelLabel.text=game.IsDailyRun and '每日挑战' or '当前第 '..game.Level.id..' 关'

    local state=game.State
    h.CapacityLabel.text=math.ceil(state.BeltCount/state.GroupSize)..' / '..math.floor(state.BeltCapacity/state.GroupSize)
    h.CapacityLabel.color=CS.UnityEngine.Color.white
    for i=0,3 do
        local stock=game.Progress[ids[i+1]];h.BoosterCounts[i].text=tostring(stock)
        h.StockBadges[i]:SetActive(stock>0)
        h.PricePlates[i]:SetActive(stock<=0)
        h.BoosterButtons[i].interactable=not game.IsEnteringLevel and not game.CommerceBusy and (i~=0 or game.UndoDepth>0 or stock<=0)
    end
    h.UndoBackground:SetArtwork('boosterBg')
    -- This hidden value feeds the first-level guided caption, never a second
    -- visible HUD line. Shuffle selection feedback uses the existing toast.
    h.InstructionLabel.text=not game.IsDailyRun and game.Level.id<=2 and flow.tutorial_instruction(game) or ''
end
local ad_views=setmetatable({},{__mode='k'})
function UI.advertising(h)
    h.CurrentGame:SetPaused(true)
    h:Begin('暂停横幅广告');local root=h.DialogRoot;local game=h.CurrentGame
    lower_title(root)
    local card=art(h,root,'Benefit card','paper',{.08,.40,.92,.84}).rectTransform
    h:AdBreakArtwork(card,V(.07,.54),V(.30,.91))
    local duration=label(h,card,'Duration',game.BannerPauseLabel,59,{.34,.59,.94,.89})
    paragraph(h,card,'Benefit','享受无横幅打扰的游戏时间\n道具仍可通过看广告获取',32,{.07,.12,.93,.48})
    local watch=button(h,root,'Watch','',{.13,.14,.87,.28},function()game:ClaimBannerPause()end,'greenButton')
    art(h,watch.transform,'Video','advert',{.055,.22,.20,.78},true)
    local caption=label(h,watch.transform,'Caption','看广告领取',39,{.23,.08,.96,.92})
    local status=label(h,root,'Availability','',23,{.10,.29,.90,.36})
    ad_views[h]={duration=duration,watch=watch,caption=caption,status=status,kind='banner',root=root}
    UI.refresh_advertising(h)
end
function UI.revive_offer(h)
    local game=h.CurrentGame;game:SetPaused(true)
    sized_dialog(h,'矿车有危险！',1170);local root=h.DialogRoot
    lower_title(root)
    local card=art(h,root,'Revive benefit','paper',{.075,.36,.925,.83}).rectTransform
    art(h,card,'Minecart danger','candyReviveDanger',{.10,.34,.90,.94},true)
    paragraph(h,card,'Benefit','本局增加一个传送带位置，继续当前关卡',30,{.07,.10,.93,.34})
    local watch=button(h,root,'Revive','',{.12,.115,.88,.255},function()game:ReviveWithAd()end,'greenButton')
    art(h,watch.transform,'Video','advert',{.07,.22,.23,.78},true)
    local caption=label(h,watch.transform,'Primary action label','复活',48,{.25,.07,.94,.93})
    local status=label(h,root,'Availability','',23,{.09,.275,.91,.33})
    ad_views[h]={watch=watch,caption=caption,status=status,kind='revive',root=root}
    h:SetDialogClose(function()game:ShowFailurePage()end)
    UI.refresh_advertising(h)
end
function UI.booster_reward(h,id)
    local item
    for i,key in ipairs(ids) do if key==id then item=i end end
    if not item then return end
    local game=h.CurrentGame;game:SetPaused(true)
    h:Begin(names[item]);local root=h.DialogRoot
    art(h,root,'Booster',id,{.31,.52,.69,.85},true)
    local descriptions={'撤回上一次操作，恢复方块和矿车状态。','重新排列一辆矿车中的方块，各种颜色的数量保持不变。','增加传送带容量，让更多方块参与组装。','增加一辆空矿车，腾出空间继续分解和组装。'}
    paragraph(h,root,'Description',descriptions[item],31,{.10,.34,.90,.49})
    local watch=button(h,root,'Watch','',{.18,.12,.82,.28},function()game:ClaimBoosterFromAd(id)end,'greenButton')
    art(h,watch.transform,'Video','advert',{.055,.22,.22,.78},true)
    local caption=label(h,watch.transform,'Caption','免费 ×1',43,{.24,.06,.96,.94})
    local status=label(h,root,'Availability','',23,{.10,.025,.90,.09})
    ad_views[h]={watch=watch,caption=caption,status=status,kind='booster',root=root}
    UI.refresh_advertising(h)
end
function UI.refresh_advertising(h)
    local view=ad_views[h]
    if not view or not h.HasDialog or view.root~=h.DialogRoot then ad_views[h]=nil;return end
    local game=h.CurrentGame;local active=view.kind=='banner' and game.BannerPauseSeconds>0
    view.watch.interactable=not game.CommerceBusy and not active and (view.kind~='revive' or game.CanReviveWithAd)
    view.caption.text=active and '暂停中' or game.CommerceBusy and '正在播放…' or view.kind=='banner' and '看广告领取' or view.kind=='revive' and '复活' or '免费 ×1'
    view.status.text=active and '横幅广告已暂停' or game.CommerceBusy and '完成广告后领取奖励' or game.AdvertisingStatus
    if view.duration then view.duration.text=game.BannerPauseLabel end
end
function UI.delete_data(h)
    h:Begin('账号与数据删除');local root=h.DialogRoot
    paragraph(h,root,'Details','清除本机进度会删除关卡和道具记录，无法撤销。云端账号的数据删除请通过服务页面办理。',32,{.10,.40,.90,.76})
    button(h,root,'Local','确认清除本机进度',{.14,.23,.86,.37},function()h.CurrentGame:ResetLocalProgress()end,'redButton')
    button(h,root,'Account','账号数据删除页面',{.14,.07,.86,.21},function()h:OpenInformation('accountDeletionUrl')end)
end
function UI.release_signin(h)signin_views[h]=nil end
function UI.render(page,h,...)
    local draw=UI[page];if not draw then return false end
    if page~='peripheral_daily_reward' then signin_views[h]=nil end
    -- In-place sign-in updates already dirty the changed Graphic components.
    if draw(h,...)~=false then h:MarkSkinDirty() end
    return true
end
return UI
