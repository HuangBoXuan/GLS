local C=require('game.ad_cadence')
local checks=0
local function check(ok,message) assert(ok,message);checks=checks+1 end
local function fresh()
    local p={};C.normalize(p,0)
    p.adTotalWins=5;p.adGameplaySeconds=180;p.adGameplaySecondsSinceInterstitial=120;p.adRoundsSinceInterstitial=1
    return p
end
local function eligible(p,level,won,seconds,now) return C.eligible(p,level or 6,won~=false,seconds or 45,now or 2000) end
check(eligible(fresh()),'First eligible ad: five wins and three minutes')
for _,id in ipairs({1,2,3,4,5}) do check(not eligible(fresh(),id),'First five levels protected') end
for key,value in pairs({adTotalWins=4,adGameplaySeconds=179.99,adGameplaySecondsSinceInterstitial=119.99,adRoundsSinceInterstitial=0}) do
    local p=fresh();p[key]=value;check(not eligible(p),'First ad gate: '..key)
end
local p=fresh();p.adLastInterstitialUtc=1880;p.adRoundsSinceInterstitial=1
check(not eligible(p,10),'Before level eleven requires two settled rounds')
check(eligible(p,11),'Level eleven permits one round, with time gates')
p.adRoundsSinceInterstitial=2
check(eligible(p,10),'Two rounds permit earlier levels')
check(not eligible(p,10,true,45,1999),'Real-time interval is independently required')
p.adGameplaySecondsSinceInterstitial=119.99
check(not eligible(p,11),'Effective gameplay interval is independently required')
p=fresh();C.rewarded_closed(p,1881)
check(not eligible(p),'Rewarded-close protection includes early ad cancellation')
check(eligible(p,6,true,45,2001),'Rewarded protection expires at 120 seconds')
local old=p.adGameplaySeconds;C.time(p,-1);C.time(p,0/0);C.time(p,math.huge)
check(p.adGameplaySeconds==old,'Invalid frame times cannot bypass gates')
C.time(p,2.5);check(p.adGameplaySeconds==old+2.5,'Valid gameplay time accumulates')
local legacy={};C.normalize(legacy,12)
check(legacy.adTotalWins==12 and legacy.adGameplaySeconds==0,'Old saves retain victories without inventing playtime')
C.normalize(legacy,50);check(legacy.adTotalWins==12,'Migration runs once')
p=fresh();C.round(p,8,false,45)
check(not eligible(p,8,false,45),'First ad cannot be a failure ad')
p=fresh();p.adLastInterstitialUtc=1880;p.adRoundsSinceInterstitial=1
C.round(p,8,false,45);check(not eligible(p,8,false,45),'First failure is protected')
C.round(p,8,false,44.99);check(not eligible(p,8,false,44.99),'Short failure is protected')
C.round(p,8,false,45);check(eligible(p,8,false,45),'Repeated eligible failure can show at settlement')
check(not eligible(p,7,false,45),'Failures before level eight are protected')
local rounds,time=p.adRoundsSinceInterstitial,p.adGameplaySecondsSinceInterstitial
-- No fill deliberately makes no close call; the next settled round may try again.
check(p.adRoundsSinceInterstitial==rounds and p.adGameplaySecondsSinceInterstitial==time,'No fill consumes no cadence budget')
C.interstitial_closed(p,2000)
check(p.adLastInterstitialUtc==2000 and p.adRoundsSinceInterstitial==0 and p.adGameplaySecondsSinceInterstitial==0,'Closed interstitial persists and resets cadence')
check(not eligible(p,11,true,45,2500),'Waiting without gameplay cannot generate another ad')
return 'WoolRush cadence boundaries, migration, failures and rewarded protection passed ('..checks..' checks)'
