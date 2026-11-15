-- Native Unity helpers shared by the downloadable Lua presentation.
-- No game assembly or process-global gameplay state is used here.
local U = CS.UnityEngine
local N = {U=U, V=U.Vector3, Q=U.Quaternion, layer=30}
function N.vec(v) return U.Vector3(v.x or 0,v.y or 0,v.z or 0) end
function N.quat(v) return U.Quaternion(v.x or 0,v.y or 0,v.z or 0,v.w or 1) end
function N.array(kind,n) return CS.System.Array.CreateInstance(typeof(kind),n) end
local reference_equals=CS.System.Object.ReferenceEquals
-- A Lua userdata can survive both Unity destruction and xLua's object-pool
-- sweep. Check the managed target before invoking any instance method; the
-- remaining Equals check handles Unity's destroyed-but-not-yet-swept objects.
function N.alive(v)
    return v~=nil and not reference_equals(v,nil) and not v:Equals(nil)
end
function N.node(name,parent)
    local go=U.GameObject(name);go.layer=N.layer
    if parent then go.transform:SetParent(parent,false) end
    return go
end
function N.destroy(go)
    if N.alive(go) then
        if go:GetType()==typeof(U.GameObject) then go:SetActive(false) end
        U.Object.Destroy(go)
    end
end
function N.layer_tree(go)
    local nodes=go:GetComponentsInChildren(typeof(U.Transform),true)
    for i=0,nodes.Length-1 do nodes[i].gameObject.layer=N.layer end
end
function N.clone(prefab,parent)
    local go=U.Object.Instantiate(prefab,parent,false);N.layer_tree(go);return go
end
function N.model(name,mesh,material,parent)
    local go=N.node(name,parent)
    local f=go:AddComponent(typeof(U.MeshFilter));f.sharedMesh=assert(mesh,'Missing mesh: '..name)
    local r=go:AddComponent(typeof(U.MeshRenderer));r.sharedMaterial=assert(material,'Missing material: '..name)
    r.shadowCastingMode=U.Rendering.ShadowCastingMode.Off;r.receiveShadows=false
    return go,r,f
end
function N.tint(go,color)
    local block=U.MaterialPropertyBlock();block:SetColor('_Color',color)
    local renderers=go:GetComponentsInChildren(typeof(U.Renderer),true)
    for i=0,renderers.Length-1 do renderers[i]:SetPropertyBlock(block) end
end
function N.hex(rgb)
    return U.Color(((rgb>>16)&255)/255,((rgb>>8)&255)/255,(rgb&255)/255,1)
end
local palette={ [1]=0xFF5B59,[2]=0x4276F5,[102]=0x72C8F4,[3]=0x57D36A,[203]=0x329E64,
    [4]=0xFFD04D,[5]=0xA67AE6,[6]=0xF49642,[7]=0xBD8B69,[8]=0xED65BA,[108]=0xF5ACD9,
    [208]=0xB94494,[11]=0x9C99A7,[12]=0xE9DDD0,[13]=0x656074 }
local colors={}
function N.color(id)
    if not colors[id] then
        local c=palette[id] and N.hex(palette[id]) or (palette[id%100] and N.color(id%100) or U.Color.magenta)
        if not palette[id] then c=id>=200 and c*.6 or U.Color.Lerp(c,U.Color.white,.35) end
        colors[id]=c
    end
    return colors[id]
end
function N.clamp(v,a,b) return math.max(a,math.min(b,v)) end
function N.sine(t) return (1-math.cos(math.pi*N.clamp(t,0,1)))*.5 end
function N.jump(a,b,t)
    return U.Vector3.Lerp(a,b,t)+U.Vector3.up*(math.sin(math.pi*t)*(1-(1-t)^5)*math.pi)
end
function N.bounds(go)
    local renderers=go:GetComponentsInChildren(typeof(U.Renderer),true)
    local bounds=U.Bounds(go.transform.position,U.Vector3.zero)
    if renderers.Length>0 then bounds=renderers[0].bounds end
    for i=1,renderers.Length-1 do bounds:Encapsulate(renderers[i].bounds) end
    return bounds
end
return N
