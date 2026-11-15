local V2, Color = CS.UnityEngine.Vector2, CS.UnityEngine.Color
local Skin = {}
local colors = {
    ink=0x49263E,cream=0xFFF0D0,plum=0x603B55,mint=0x61C7B4,
    teal=0x286363,amber=0xFFB83E,caramel=0xC88D65,coral=0xFF625E,text=0x000000,
}
local layout = {
    ['Playing Monitor/Top']={0,.928,1,.989},
    ['Playing Monitor/Top/Level frame']={.66,.06,.975,.98},
    ['Playing Monitor/Top/Capacity frame']={.352,.10,.648,.91},
    ['Playing Monitor/Top/Settings']={.025,.06,.15,.98},
    ['Playing Monitor/Instruction']={.07,.195,.93,.23},
    ['Home Monitor/Profile']={.035,.886,.18,.977},
    ['Home Monitor/Coins']={.23,.909,.61,.977},
    ['Home Monitor/Life']={.64,.919,.76,.971},
    ['Home Monitor/Unlimited life']={.64,.919,.76,.971},
    ['Home Monitor/Settings']={.034,.928,.153,.982},
    ['Home Monitor/Navigation background']={0,0,1,.13},
    ['Home Monitor/Navigation separator']={0,.128,1,.133},
}
-- Any existing element can be addressed by its path, or styled by its name.
-- Add new controls in pages.lua; no Unity scene/prefab rebuild is necessary.
local dialog_layout={} -- Each menu owns its layout; no stale per-name overrides.
local text_overrides={}
local font_overrides={}
function Skin.color(name, fallback)
    local n=colors[name];if not n then return fallback end
    return Color(((n>>16)&255)/255,((n>>8)&255)/255,(n&255)/255,1)
end
function Skin.text(value) return text_overrides[value] or value end
function Skin.apply(hud)
    local rects=hud:SkinRects()
    for i=0,rects.Length-1 do
        local rt=rects[i];local path=hud:SkinPath(rt);local item=layout[path]
        if not item and dialog_layout[hud.DialogTitle] and path:match('^Modal overlay/Dialog/') then
            item=dialog_layout[hud.DialogTitle][rt.name]
        end
        if item then
            rt.anchorMin,rt.anchorMax=V2(item[1],item[2]),V2(item[3],item[4])
            rt.offsetMin,rt.offsetMax=V2(item[5] or 0,item[6] or 0),V2(item[7] or 0,item[8] or 0)
        end
        local label=hud:SkinText(rt)
        if label then
            local value=Skin.text(label.text)
            if value~=label.text then label.text=value end
            local image=rt:GetComponentInParent(typeof(CS.LoopSort0.ReferenceImage))
            local sprite=image and image.sprite and image.sprite.name or ''
            local level=path:find('Playing Monitor/Top/Level frame')~=nil
            local chrome=path=='Modal overlay/Dialog/Dialog chrome/Title'
            local candyTitle=chrome and (hud.DialogTitle=='设置' or hud.DialogTitle=='暂停' or hud.DialogTitle=='恭喜通过' or hud.DialogTitle=='再试一次吧！')
            local white=(sprite:find('greenButton') or sprite:match('/ yellowButton$') or sprite:find('candyPurpleButton') or sprite:find('redButton') or level or path:find('Playing Monitor/Top/Capacity frame') or path:find('Inventory badge') or candyTitle) and true or false
            local signinInk=hud.DialogTitle=='七日签到' and (rt.name=='Day' or rt.name=='Status') and label.color
            CS.LoopSort0.UiTypography.Apply(label,white,level)
            if signinInk then label.color=signinInk end
            local font=font_overrides[path]
            if font then label.fontSize=font;label.resizeTextMaxSize=font end
        end
    end
end
return Skin
