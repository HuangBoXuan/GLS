local Assets={}
local Skin=require('ui.skin')
local Theme=CS.LoopSort0.ArtTheme
local definitions={}
local icons={'undo','shuffle','belt','truck','settings','close','coin','heart','avatar','plus','info','gift',
    'sound','music','haptic','shopNav','areasNav','homeNav','rankingNav','teamsNav','calendar','check','boosterCount','brokenHeart',
    'tutorialCurtain','tutorialHidden','tutorialPortal','tutorialBarrier','tutorialIce','tutorialLock',
    'tutorialRope','tutorialCarpet','tutorialTag','tutorialGenerator','tutorialScrew','blocks'}
for i,id in ipairs(icons) do definitions[id]={atlas='icons',index=i-1,panel=false} end
definitions.goldPile=definitions.coin;definitions.disabled=definitions.close
definitions.book={atlas='mechanics-book',index=0,panel=false}
-- Reference skin: cream faces on plum rims, shared by gameplay and dialogs.
local reference_panels={
    [0]={'button','blueButton','levelPanel','capacityPanel','coinBar','ribbon','shopBanner','priceBadge','redButton','yellowButton'},
    [1]={'boosterBg','boosterGrey'},
    [10]={'frame','paper','failArt','stand','settingsInner','navBackground'},
    [11]={'greenButton','buttonGreen','navSelected','navSeparator'},
}
for index,group in pairs(reference_panels) do
    for _,id in ipairs(group) do definitions[id]={atlas='reference-ui',index=index,panel=true} end
end
local reference_icons={undo=2,shuffle=3,belt=4,truck=5,settings=6,gift=7,boosterCount=8,close=9}
for id,index in pairs(reference_icons) do definitions[id]={atlas='reference-ui',index=index,panel=false} end
definitions.disabled=definitions.close
-- Cream/plum menu skin. Shared aliases keep gameplay and dialogs consistent.

-- Both presentation paths consume the same final skin definition.
for id,source in pairs(require('ui.menu_theme').sprites) do definitions[id]=source end

function Assets.sprite(id)
    local source=definitions[id];if not source then return nil end
    local cell=CS.LoopSort0.ThemeSpriteDefinition();cell.atlas,cell.index,cell.panel=source.atlas,source.index,source.panel
    return cell
end
local cargo={
    [1]=0xFF5B59,[2]=0x4276F5,[102]=0x72C8F4,[3]=0x57D36A,[203]=0x329E64,[4]=0xFFD04D,
    [5]=0xA67AE6,[6]=0xF49642,[7]=0xBD8B69,[8]=0xED65BA,[108]=0xF5ACD9,[208]=0xB94494,
    [11]=0x9C99A7,[12]=0xE9DDD0,[13]=0x656074,
}
function Assets.cargo_color(id,original) return cargo[id] and Theme.Hex(cargo[id]) or original end
local surfaces={
    [2]={'amber',0},[205]={'amber',0},[8]={0xF46B2D,2},[9]={0xD89448,0},[10]={0xF46B2D,2},
    [11]={0xD89448,0},[199]={'cream',0},[14]={0xEBAE52,1},[23]={0xEBAE52,1},
    [15]={'caramel',5},[206]={'caramel',5},[16]={'ink',0},[30]={'ink',0},[22]={'amber',4},
    [24]={0xAFE9E7,6},[26]={'cream',3},[27]={'caramel',0},[28]={'plum',0},[32]={'plum',0},
    [31]={'cream',0},[33]={'mint',0},[171]={0x57D36A,0},[177]={'coral',0},
    [202]={0xF46B2D,0},[201]={0x342A2C,0},[25]={0xD89448,0},
}
function Assets.feature_material(id)
    local surface=surfaces[id] or {'cream',0}
    local color=type(surface[1])=='number' and Theme.Hex(surface[1]) or Skin.color(surface[1],CS.UnityEngine.Color.white)
    local material=Theme.Surface(color,surface[2])
    if id==8 or id==9 or id==10 then
        material:SetColor('_EdgeColor',Theme.Hex(0xF7B45A));material:SetFloat('_EdgeWidth',.065)
    end
    if id==24 then
        material:SetFloat('_Opacity',.42);material:SetInt('_SrcBlend',5);material:SetInt('_DstBlend',10)
        material:SetInt('_ZWrite',0);material.renderQueue=3000
    end
    return material
end
return Assets
