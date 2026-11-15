-- Shared artwork and typography for both Unity UI entry points.
local Theme={font={button=49,title=60,body=48,caption=40,primary=80,level=63,tab=52,
    guideTitle=51,guideName=35,guideDescription=39},sprites={}}
local function group(atlas,index,panel,ids)
 for _,id in ipairs(ids) do Theme.sprites[id]={atlas=atlas,index=index,panel=panel} end
end
group('approved-menu',0,true,{'button','blueButton','redButton','levelPanel','capacityPanel','coinBar','priceBadge'})
group('approved-menu',1,true,{'levelTile','boosterBg','boosterGrey'})
-- Rectangular dialog base and overlaid header are separate Image nodes.
group('approved-menu',6,true,{'frame'})
group('approved-menu',3,true,{'ribbon','shopBanner','greenButton','buttonGreen'})
group('approved-menu',4,true,{'yellowButton','navSelected'})
group('menu-details',8,true,{'menuRow','paper','settingsInner','failArt','stand','navBackground'})
group('menu-details',9,true,{'languageButton'})
for id,index in pairs({toggleOn=0,toggleOff=1,skipBadge=3,music=4,sound=5,haptic=6,language=7,close=11}) do group('menu-details',index,false,{id}) end
group('advert-clapper',0,false,{'advert'})
group('advert-yellow',0,false,{'advertPaused'})
group('belt-capacity',0,false,{'beltCapacity'})
-- Artist-supplied standalone artwork. Both the Editor player and module host resolve these aliases.
for _,id in ipairs({'belt','close','haptic','language','music','shuffle','sound','toggleOn','truck','tutorialHand','undo','helpButton'}) do
 group(id,0,false,{id})
end
group('redButton',0,true,{'redButton'})
-- Candy factory 2.0 artwork.
group('candy-factory-home',0,false,{'homeBackground'})
group('candy-factory-board',0,false,{'gameBackground'})
group('candy-result-win',0,false,{'candyWinGift'})
group('candy-result-fail',0,false,{'candyFailGift'})
group('candy-revive-danger',0,false,{'candyReviveDanger'})
for i,id in ipairs({'candyGift','candyBook','candyVideo'}) do group('candy-home-icons',i-1,false,{id}) end
local plates='candy-v2-panels'
group(plates,0,true,{'frame','comPanel'})
group(plates,1,true,{'candyHeader'})
group(plates,4,true,{'priceBadge'})
group(plates,5,true,{'paper','menuRow','settingsInner','stand','toastPanel'})
group(plates,8,true,{'capacityPanel'})
group(plates,9,true,{'mechanicTile'})
group(plates,10,false,{'settings'})
group(plates,11,false,{'candyWinRibbon'})
group(plates,12,false,{'candyFailRibbon'})
group(plates,13,false,{'toggleOff'})
group(plates,14,false,{'candyGlow'})
local buttons='candy-buttons'
group(buttons,0,true,{'greenButton','greenButtonHome','buttonGreen'})
group(buttons,1,true,{'yellowButton','navSelected','candyPurpleButton'})
group(buttons,2,true,{'button','blueButton','levelTile'})
group(buttons,3,true,{'yellowButtonHome'})
group(buttons,4,true,{'languageButton'})
group(buttons,5,true,{'candyHomeChip'})
group(buttons,6,true,{'boosterBg','boosterBgHome'})
group(buttons,7,true,{'boosterGrey'})
-- 3 by 4 atlas in the approved factory mechanism order.
for i,id in ipairs({'tutorialPortal','tutorialHidden','tutorialCurtain','tutorialBarrier','tutorialScrew',
    'tutorialIce','tutorialLock','tutorialTag','tutorialRope','tutorialCarpet','tutorialGenerator'}) do
 group('candy-mechanisms',i-1,false,{id})
end
group('candy-mechanisms',11,false,{'truck'})
return Theme
