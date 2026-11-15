local Signin=require('game.signin')
local Peripheral=require('game.peripheral')
local Progress=require('platform.progress')
local today='2026-11-01'
local function inventory(count,last)
    return {undo=10,belt=10,shuffle=10,truck=10,dailyRewardCount=count,lastDailyRewardDay=last or ''}
end
local function stock(p,a,b,c,d)
    assert(p.undo==a and p.belt==b and p.shuffle==c and p.truck==d,'Unexpected reward inventory')
end
assert(Signin.state(inventory(0),today).day==1)
assert(Signin.state(inventory(0,today),today).claimed)
assert(Signin.count({lastDailyRewardDay='2026-10-31'})==1,'Legacy save migration')
assert(Signin.count({dailyRewardCount=0/0,lastDailyRewardDay=''})==0)
assert(Signin.state(inventory(7,today),today).completed==7)
assert(Signin.state(inventory(7,'2026-10-31'),today).day==1,'Cycle rollover')
assert(Signin.state(inventory(2,'2026-10-01'),today).day==3,'Missed days retain cumulative progress')
local saves=0
local game={Progress=inventory(1,'2026-10-31'),TodayKey=today,SaveProgress=function()saves=saves+1 end,
    Hud={PeripheralPage=function()end,Toast=function()end}}
Peripheral.claim_daily_reward(game);stock(game.Progress,12,12,10,10)
assert(game.Progress.dailyRewardCount==2 and saves==1)
Peripheral.claim_daily_reward(game);stock(game.Progress,12,12,10,10);assert(saves==1,'Duplicate scene claim')
game.Progress=inventory(3,'2026-10-31');Peripheral.claim_daily_reward(game,true);stock(game.Progress,12,12,12,12)
assert(game.Progress.dailyRewardCount==4,'Multiplier must not skip sign-in days')
local originalDay=Progress.day;Progress.day=function()return today end
local native=setmetatable({data=inventory(4,'2026-10-31'),changed=function()saves=saves+1 end},{__index=Progress})
assert(native:claim_daily());stock(native.data,12,10,12,10)
assert(not native:claim_daily(),'Duplicate native claim');stock(native.data,12,10,12,10)
native.locked=true;native.data=inventory(5,'2026-10-31');assert(not native:claim_daily());stock(native.data,10,10,10,10)
native.locked=false;native.data=inventory(6,'2026-10-31');assert(native:claim_daily());stock(native.data,12,12,12,12)
native.data.lastDailyRewardDay='2026-10-31';assert(native:claim_daily());assert(native.data.dailyRewardCount==8)
stock(native.data,14,14,14,14)
-- Native presentation must update its existing cards after a claim.
local NativeUI=require('platform.ui')
local nodes=0;local callback
local function node()nodes=nodes+1;return {}end
local host=setmetatable({progress=setmetatable({data=inventory(0),changed=function()end},{__index=Progress})},{__index=NativeUI})
function host:_dialog(title)self.modal={root=node()};self.modal_title=title;return node()end
function host:_content(parent)return parent end
function host:_image()return node(),node()end
function host:_label(parent,name,text)local label=node();label.text=text;return label end
function host:_button(scope,parent,name,text,r,fn)callback=fn;return node(),node(),{text=text}end
function host:refresh()end
function host:toast()end
function host:close_dialog()self.modal=nil;self.closedByContinue=true end
host:daily_reward();local initial=nodes;local root=host.modal.root
callback();assert(host.modal.root==root and nodes==initial,'Native sign-in claim rebuilt its nodes')
assert(host.signin_view.view.buttonLabel.text=='继续游戏');stock(host.progress.data,12,12,12,12)
callback();assert(host.closedByContinue,'Native continue callback was not updated')
Progress.day=originalDay
return 'Sign-in migration, schedule, inventory, duplicates, multiplier, rollover and native view reuse passed'
