-- Native assets borrowed from the module scope. Only objects created here are destroyed here.
local Json = require('game.json')
local U = CS.UnityEngine
local Assets = {}
Assets.__index = Assets

local theme = {
    ink=0x49263E, cream=0xFFF0D0, plum=0x603B55, mint=0x61C7B4,
    teal=0x286363, amber=0xFFB83E, caramel=0xC88D65, coral=0xFF625E,
    accent=0xB8F3D8, text=0x000000,
}
local cargo = {
    [1]=0xFF5B59,[2]=0x4276F5,[102]=0x72C8F4,[3]=0x57D36A,[203]=0x329E64,[4]=0xFFD04D,
    [5]=0xA67AE6,[6]=0xF49642,[7]=0xBD8B69,[8]=0xED65BA,[108]=0xF5ACD9,[208]=0xB94494,
    [11]=0x9C99A7,[12]=0xE9DDD0,[13]=0x656074,
}
local cells = {}
local function cell(id,atlas,index,panel) cells[id]={atlas=atlas,index=index,panel=panel or false} end
local icons={'undo','shuffle','belt','truck','settings','close','coin','heart','avatar','plus','info','gift',
    'sound','music','haptic','shopNav','areasNav','homeNav','rankingNav','teamsNav','calendar','check','boosterCount','brokenHeart',
    'tutorialCurtain','tutorialHidden','tutorialPortal','tutorialBarrier','tutorialIce','tutorialLock',
    'tutorialRope','tutorialCarpet','tutorialTag','tutorialGenerator','tutorialScrew','blocks'}
for i,id in ipairs(icons) do cell(id,'icons',i-1) end
for index,names in pairs({
    [0]={'button','blueButton','levelPanel','capacityPanel','coinBar','ribbon','shopBanner','priceBadge','redButton','yellowButton'},
    [1]={'boosterBg','boosterGrey'},[10]={'frame','paper','failArt','stand','settingsInner','navBackground'},
    [11]={'greenButton','buttonGreen','navSelected','navSeparator'},
}) do for _,id in ipairs(names) do cell(id,'reference-ui',index,true) end end
for id,index in pairs({undo=2,shuffle=3,belt=4,truck=5,settings=6,gift=7,boosterCount=8,close=9}) do cell(id,'reference-ui',index) end
cell('book','mechanics-book',0)
cells.goldPile=cells.coin; cells.disabled=cells.close

-- Both presentation paths consume the same final skin definition.
for id,source in pairs(require('ui.menu_theme').sprites) do cells[id]=source end

local function hex(value) return U.Color(((value>>16)&255)/255,((value>>8)&255)/255,(value&255)/255,1) end
function Assets.new(host)
    assert(host and host.CurrentGameId,'A module host is required')
    local index=Json.decode(host:ReadDataText('data/'..host.CurrentGameId..'/assets/index.json'))
    assert(index.schema==1 and index.bundle==host.CurrentGameId..'.bundle','Invalid native asset index')
    return setmetatable({host=host,index=index,borrowed={},owned={},sprites={},surfaces={},decoded={},disposed=false},Assets)
end
function Assets:_alive() assert(not self.disposed,'Module assets have been disposed') end
function Assets:_load(path,kind)
    self:_alive()
    assert(type(path)=='string' and path:sub(1,7)=='Assets/','Invalid asset address')
    local found=self.borrowed[path]
    if found then return found end
    local asset=self.host:LoadAsset(self.index.bundle,path,typeof(kind))
    assert(asset~=nil,'Module asset unavailable: '..path)
    self.borrowed[path]=asset
    return asset
end
function Assets:own(value)
    self:_alive()
    assert(value~=nil,'Cannot own a nil Unity object')
    self.owned[#self.owned+1]=value
    return value
end
function Assets:mesh(id,complete)
    local group=complete==false and self.index.primaryMeshes or self.index.meshes
    return self:_load(assert(group[tostring(id)],'Unknown model '..tostring(id)),U.Mesh)
end
function Assets:material(name)
    return self:_load(assert(self.index.materials[name],'Unknown material '..tostring(name)),U.Material)
end
function Assets:new_material(name)
    return self:own(U.Material(self:material(name)))
end
function Assets:feature_material(id)
    local path=self.index.featureMaterials[tostring(id)]
    return path and self:_load(path,U.Material) or self:material('cream')
end
-- Clone and reuse recoloured materials; never mutate materials borrowed from
-- the host's bundle. This also styles prefabs baked before the palette change.
function Assets:style_volcanic(go)
    local palette={[8]=0xF46B2D,[9]=0xD89448,[10]=0xF46B2D,[11]=0xD89448,
        [202]=0xF46B2D,[201]=0x342A2C,[25]=0xD89448}
    self.volcanic_materials=self.volcanic_materials or {}
    local renderers=go:GetComponentsInChildren(typeof(U.Renderer),true)
    for i=0,renderers.Length-1 do
        local renderer=renderers[i];local materials=renderer.sharedMaterials;local changed=false
        for slot=0,materials.Length-1 do
            local original=materials[slot]
            local id=original and tonumber(original.name:match('[Ff]eature[ %-](%d+)'))
            if id and palette[id] then
                local material=self.volcanic_materials[id]
                if not material then
                    material=self:own(U.Material(original));material.color=hex(palette[id])
                    if id==8 or id==9 or id==10 then material:SetColor('_EdgeColor',hex(0xF7B45A));material:SetFloat('_EdgeWidth',.065) end
                    self.volcanic_materials[id]=material
                end
                materials[slot]=material;changed=true
            end
        end
        if changed then renderer.sharedMaterials=materials end
    end
end
function Assets:chain_mesh(width,depth)
    self.chain_meshes=self.chain_meshes or {}
    local key=string.format('%.4f/%.4f',width,depth)
    if self.chain_meshes[key] then return self.chain_meshes[key] end
    local vertices,normals,slots={},{},{ {},{} }
    local function triangle(slot,a,b,c)
        local indices=slots[slot]
        indices[#indices+1]=a;indices[#indices+1]=b;indices[#indices+1]=c
    end
    local function ring(slot,center,rx,rz,tube,rotation)
        local first=#vertices
        for i=0,15 do for j=0,5 do
            local a,u=i*2*math.pi/16,j*2*math.pi/6
            local x,z,radial,y=math.cos(a),math.sin(a),math.cos(u),math.sin(u)
            local p=center+rotation*U.Vector3(x*(rx+radial*tube),y*tube,z*(rz+radial*tube))
            local n=rotation*U.Vector3(x*radial,y,z*radial)
            vertices[#vertices+1]=U.Vector3(p.x/width,p.z/depth,-p.y)
            normals[#normals+1]=U.Vector3(n.x*width,n.z*depth,-n.y).normalized
        end end
        for i=0,15 do for j=0,5 do
            local row,next_row=first+i*6,first+((i+1)%16)*6
            local a,b,c,d=row+j,next_row+j,next_row+(j+1)%6,row+(j+1)%6
            triangle(slot,a,c,b);triangle(slot,a,d,c)
        end end
    end
    local tube=math.min(.071,width*.075)
    for _,side in ipairs({-1,1}) do
        local nx,nz=math.max(2,math.ceil(width/.34)),math.max(2,math.ceil(depth/.34))
        for i=1,nx-1 do ring(1,U.Vector3(width*(-.5+(i-.5)/(nx-1)),.08,side*depth*.5),.234,.092,tube,U.Quaternion.Euler((i%2)*62,0,0)) end
        for i=1,nz-1 do ring(1,U.Vector3(side*width*.5,.08,depth*(-.5+(i-.5)/(nz-1))),.092,.234,tube,U.Quaternion.Euler(0,0,(i%2)*62)) end
        for _,z in ipairs({-depth*.5,depth*.5}) do ring(2,U.Vector3(side*width*.5,.136,z),.158,.158,tube*1.28,U.Quaternion.Euler(0,35,0)) end
    end
    ring(2,U.Vector3(0,.164,depth*.5),.335,.109,tube*1.26,U.Quaternion.identity)
    local nativeVertices=CS.System.Array.CreateInstance(typeof(U.Vector3),#vertices)
    local nativeNormals=CS.System.Array.CreateInstance(typeof(U.Vector3),#vertices)
    for i,p in ipairs(vertices) do nativeVertices[i-1]=p;nativeNormals[i-1]=normals[i] end
    local mesh=self:own(U.Mesh());mesh.name='Volcanic chain links '..key
    if #vertices>65535 then mesh.indexFormat=CS.UnityEngine.Rendering.IndexFormat.UInt32 end
    mesh.vertices=nativeVertices;mesh.normals=nativeNormals;mesh.subMeshCount=2
    for slot,values in ipairs(slots) do
        local indices=CS.System.Array.CreateInstance(typeof(CS.System.Int32),#values)
        for i,value in ipairs(values) do indices[i-1]=value end
        mesh:SetTriangles(indices,slot-1)
    end
    mesh:RecalculateBounds();self.chain_meshes[key]=mesh;return mesh
end
function Assets:carpet_mesh(count)
    count=math.max(1,count)
    self.carpet_meshes=self.carpet_meshes or {}
    if self.carpet_meshes[count] then return self.carpet_meshes[count] end
    -- Closed fabric mesh, including the underside and stitched edge thickness.
    local bounds=self:mesh(397,false).bounds;local low,high=bounds.min,bounds.max
    local columns=math.min(256,count*16);local rows=4
    local face=(columns+1)*(rows+1)
    local vertices=CS.System.Array.CreateInstance(typeof(U.Vector3),face*2)
    local uv=CS.System.Array.CreateInstance(typeof(U.Vector2),vertices.Length)
    local triangles=CS.System.Array.CreateInstance(typeof(CS.System.Int32),columns*rows*12+(columns+rows)*12)
    for z=0,rows do for x=0,columns do
        local u,v=x/columns,z/rows;local i=z*(columns+1)+x
        local fold=.065*(.5-.5*math.cos(u*count*4*math.pi))*(.75+.25*math.sin(v*math.pi))
        vertices[i]=U.Vector3(low.x+(high.x-low.x)*u,low.y+fold,low.z+(high.z-low.z)*v)
        uv[i]=U.Vector2(u,v)
        vertices[i+face]=vertices[i]+U.Vector3(0,-.06,0);uv[i+face]=uv[i]
    end end
    local i=0
    for z=0,rows-1 do for x=0,columns-1 do
        local a=z*(columns+1)+x;local c=a+columns+1
        triangles[i]=a;triangles[i+1]=c;triangles[i+2]=a+1
        triangles[i+3]=a+1;triangles[i+4]=c;triangles[i+5]=c+1;i=i+6
        triangles[i]=a+face;triangles[i+1]=a+1+face;triangles[i+2]=c+face
        triangles[i+3]=a+1+face;triangles[i+4]=c+1+face;triangles[i+5]=c+face;i=i+6
    end end
    local function edge(a,c)
        triangles[i]=a;triangles[i+1]=a+face;triangles[i+2]=c
        triangles[i+3]=c;triangles[i+4]=a+face;triangles[i+5]=c+face;i=i+6
    end
    for x=0,columns-1 do edge(x+1,x);edge(rows*(columns+1)+x,rows*(columns+1)+x+1) end
    for z=0,rows-1 do edge(z*(columns+1),(z+1)*(columns+1));edge((z+1)*(columns+1)+columns,z*(columns+1)+columns) end
    local mesh=self:own(U.Mesh());mesh.name='Volcanic blanket folds and hem '..count
    mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh:RecalculateNormals();mesh:RecalculateBounds()
    self.carpet_meshes[count]=mesh;return mesh
end
function Assets:physics_material()
    return self:_load(self.index.physicsMaterial,U.PhysicMaterial)
end
function Assets:surface(color,pattern)
    self:_alive()
    if type(color)=='string' or type(color)=='number' then color=self:color(color) end
    pattern=pattern or 0
    -- Curtains never mutate their shared surface after assignment. Reuse it
    -- across level reloads and undo instead of retaining a material per curtain.
    local key=table.concat({color.r,color.g,color.b,color.a,pattern},':')
    local material=self.surfaces[key]
    if material then return material end
    material=self:new_material('cream');material.color=color
    material:SetFloat('_Pattern',pattern)
    self.surfaces[key]=material
    return material
end
function Assets:color(key)
    if type(key)=='string' then return hex(assert(theme[key],'Unknown theme color '..key)) end
    if cargo[key] then return hex(cargo[key]) end
    if not self.palette then
        self.palette={}
        for _,entry in ipairs(self:data('palette').colors) do self.palette[entry.id]=U.Color(entry.r,entry.g,entry.b,1) end
    end
    local color=self.palette[key]
    if color then return color end
    local basis=self.palette[key%100] or U.Color.magenta
    if key>=200 then return U.Color(basis.r*.6,basis.g*.6,basis.b*.6,1) end
    return U.Color.Lerp(basis,U.Color.white,.35)
end
function Assets:font() return self:_load(self.index.font,U.Font) end
function Assets:audio(name)
    local path=self.index.audio[name]
    return path and self:_load(path,U.AudioClip) or nil
end
function Assets:texture(name)
    return self:_load(assert(self.index.textures[name],'Unknown texture '..tostring(name)),U.Texture2D)
end
function Assets:_json(section,name)
    self:_alive()
    local key=section..'/'..name
    if not self.decoded[key] then
        local path=assert(self.index[section][name],'Unknown module data '..key)
        self.decoded[key]=Json.decode(self:_load(path,U.TextAsset).text)
    end
    return self.decoded[key]
end
function Assets:data(name) return self:_json('data',name) end
function Assets:animation(name) return self:_json('animations',tostring(name)) end
function Assets:prefab(name)
    return self:_load(assert(self.index.prefabs[name],'Unknown prefab '..tostring(name)),U.GameObject)
end
function Assets:track(kind,id)
    local catalog=assert(self.index.tracks[kind or 'main'],'Unknown track catalog')
    return self:_load(assert(catalog[tostring(id)],'Missing conveyor for level '..tostring(id)),U.GameObject)
end
function Assets:_atlas()
    if not self.atlas then self.atlas=Json.decode(self:_load(self.index.atlas,U.TextAsset).text) end
    return self.atlas
end
function Assets:content_bounds(name)
    local definition=cells[name]
    if not definition then return {0,0,1,1} end
    local entries=self:_atlas()[definition.atlas..'.content']
    return entries and entries[definition.index+1] or {0,0,1,1}
end
function Assets:standalone_panel(name)
    local source=cells[name]
    return source and source.panel and source.atlas~='approved-menu' and source.atlas~='menu-details' and source.atlas~='reference-ui'
end
function Assets:responsive_panel(name) return cells[name] and (cells[name].atlas=='approved-menu' or cells[name].atlas=='menu-details') end
function Assets:is_panel(name) return cells[name] and cells[name].panel or false end
function Assets:sprite(name)
    self:_alive()
    local definition=cells[name]
    if not definition then return nil end
    if self.sprites[name] then return self.sprites[name] end
    local atlas=self:_atlas()
    local shared=self.index.textures['ui-batch'] and atlas['ui-batch.'..definition.atlas]
    local bounds=shared and shared[definition.index+1]
    local texture
    if bounds and not Json.is_null(bounds) then
        texture=self:texture('ui-batch')
    else
        texture=self:texture(definition.atlas)
        bounds=assert(atlas[definition.atlas][definition.index+1],'Missing sprite rectangle '..name)
    end
    local rect=U.Rect(bounds[1]*texture.width,bounds[2]*texture.height,bounds[3]*texture.width,bounds[4]*texture.height)
    local border=U.Vector4.zero
    if definition.panel then
        local list=atlas[definition.atlas..'.borders']
        local edges=list and list[definition.index+1] or {.22,.22,.22,.22}
        border=U.Vector4(rect.width*edges[1],rect.height*edges[2],rect.width*edges[3],rect.height*edges[4])
    end
    local sprite=self:own(U.Sprite.Create(texture,rect,U.Vector2(.5,.5),200,0,U.SpriteMeshType.FullRect,border))
    sprite.name='LoopSort / '..name
    self.sprites[name]=sprite
    return sprite
end
function Assets:dispose()
    if self.disposed then return end
    -- Destroy only locally created resources. The host unloads borrowed bundle assets.
    for i=#self.owned,1,-1 do
        local object=self.owned[i]
        if object~=nil then U.Object.Destroy(object) end
        self.owned[i]=nil
    end
    self.borrowed={};self.sprites={};self.surfaces={};self.decoded={};self.atlas=nil;self.palette=nil
    self.host=nil;self.index=nil;self.disposed=true
end
return Assets
