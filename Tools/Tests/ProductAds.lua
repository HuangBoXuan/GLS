local Services=require('platform.services')
local UI=require('platform.ui')
local calls,shows=0,0
local ads={IsAvailable=true,IsReady=function()return false end,Show=function()shows=shows+1 end}
local service=setmetatable({host={Services={Ads=ads}}},{__index=Services})
service:interstitial('level_complete',function(ok)assert(not ok);calls=calls+1 end)
assert(calls==1 and shows==0,'Unavailable interstitial must continue without an ad')
ads.IsReady=function()return true end
ads.Show=function(self,placement,format,callback)
    assert(placement=='level_complete' and format=='interstitial');shows=shows+1
    callback('{"status":"completed"}');callback('{"status":"completed"}')
end
service:interstitial('level_complete',function(ok)assert(ok);calls=calls+1 end)
assert(calls==2 and shows==1 and not service.ad_pending,'Interstitial callback must complete once')
local late
ads.Show=function(self,p,f,callback)late=callback end
service:interstitial('level_complete',function()calls=calls+1 end)
service.invalidated=true;late('{"status":"completed"}')
assert(calls==2,'Old account callback must not advance a level')
local wanted,resizes={},0
local native={BannerTopPixels=150,SetHomeBanner=function(self,value)wanted[#wanted+1]=value end}
local page=setmetatable({native_features=native,home_visible=true,services={},_safe_area=function()resizes=resizes+1 end},{__index=UI})
page:_native_banner();page:_native_banner()
assert(#wanted==1 and wanted[1] and page.banner_top==150 and resizes==1,'Home banner must reserve native pixel geometry')
page.modal={};page:_native_banner()
assert(not wanted[2] and page.banner_top==0,'Dialogs must hide the banner and release its inset')
page.modal=nil;page.closed=true;page:_native_banner();assert(page.banner_top==0)
local nextClick,navigated
local view=setmetatable({context={kind='main'},modal={},dialog_close={gameObject={SetActive=function()end}},
    services={interstitial=function()error('Next must not request an ad')end},
    _dialog=function()return {} end,_label=function()end,_fireworks=function()end,
    _button=function(self,scope,root,name,text,rect,click)if name=='Next' then nextClick=click end end,
    _invoke=function(self,action)navigated=action end},{__index=UI})
view:_result(true);nextClick();assert(navigated=='next','Platform next-level navigation must be direct')
local p={};local cadence=require('game.ad_cadence');cadence.normalize(p,0)
local saved=0
local rewarded=setmetatable({host={Services={Ads={IsAvailable=true,Show=function(self,placement,format,reply)reply('{"status":"canceled"}') end}}},
    config={ad_reward='reward'},progress={data=p,changed=function()saved=saved+1 end}},{__index=Services})
rewarded:rewarded('revive',function(ok)assert(not ok,'Canceled rewarded ad must not award')end)
assert(saved==1 and p.adLastRewardedClosedUtc>0,'Platform canceled rewarded close must protect interstitial cadence')
return 'Interstitial fallback, duplicate callback, account invalidation, native banner inset, direct Next and rewarded protection passed'
