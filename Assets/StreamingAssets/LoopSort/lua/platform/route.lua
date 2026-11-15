-- The same 256-sample steering and spatial search as the standalone board.
-- Unity's isolated PhysicsScene supplies collision response; this is not a
-- kinematic conveyor replacement and never changes global Physics settings.
local N=require('platform.native')
local V,U=N.V,N.U
local Route={};Route.__index=Route
local function key(x,y,z) return ((x&0x1fffff)<<42)|((y&0x1fffff)<<21)|(z&0x1fffff) end
function Route.new(spline)
    local self=setmetatable({spline=spline,samples={},cells={},length=spline.length,closed=spline.closed},Route)
    local positions,forwards={},{}
    local count=spline.sample_count
    for i=1,count do positions[i]=N.vec(spline.positions[i]) end
    for i=1,count do
        local before,after
        if spline.closed then before=positions[(i-2)%(count-1)+1];after=positions[i%(count-1)+1]
        elseif i==1 then before=positions[1];after=positions[2]
        elseif i==count then before=positions[count-1];after=positions[count]
        else before=positions[i-1];after=positions[i+1] end
        local forward=after-before
        forwards[i]=forward.sqrMagnitude>0.00001 and forward.normalized or V.forward
    end
    -- Position() clamps beyond the open ends. Match NativeSpline's Slerp
    -- branch, including its zero vector at the clamped side.
    if not spline.closed and count>2 then
        forwards[1]=V.Slerp(positions[2]-positions[1],V.zero,0).normalized
        forwards[count]=V.Slerp(V.zero,positions[count]-positions[count-1],math.huge).normalized
    end
    self.positions,self.forwards=positions,forwards
    -- Cache scalar geometry once. Arithmetic on Unity Vector3 userdata crosses
    -- the reflection bridge for every operator/property in the search loop.
    self.segments={}
    for i=1,#positions-1 do
        local a,b=positions[i],positions[i+1]
        local ax,ay,az=a.x,a.y,a.z;local dx,dy,dz=b.x-ax,b.y-ay,b.z-az
        local length2=dx*dx+dy*dy+dz*dz
        self.segments[i]={x=ax,y=ay,z=az,dx=dx,dy=dy,dz=dz,length2=length2,length=math.sqrt(length2),distance=spline.distances[i]}
    end
    for i=0,255 do
        local pct=i/255;local pos=self:position(pct);local forward=self:forward(pct)
        local distance=spline:distance_at_percent(pct)
        local s={position=pos,forward=forward,side=V.Cross(forward,V.up),distance=distance,target=self:at(distance+1)}
        s.x,s.y,s.z=pos.x,pos.y,pos.z
        s.fx,s.fy,s.fz=forward.x,forward.y,forward.z
        s.sx,s.sy,s.sz=s.side.x,s.side.y,s.side.z
        s.tx,s.ty,s.tz=s.target.x,s.target.y,s.target.z
        self.samples[i+1]=s
        local k=key(math.floor(pos.x/.33),math.floor(pos.y/.33),math.floor(pos.z/.33))
        if not self.cells[k] then self.cells[k]={} end
        table.insert(self.cells[k],s)
    end
    return self
end
function Route:position(p) return N.vec(self.spline:position(p)) end
function Route:at(d) return N.vec(self.spline:at_distance(d)) end
function Route:forward(p)
    local x=N.clamp(p,0,1)*(#self.forwards-1);local i=math.min(math.floor(x)+1,#self.forwards-1)
    return V.Slerp(self.forwards[i],self.forwards[i+1],x-i+1).normalized
end
function Route:nearest(position)
    local x,y,z=position.x,position.y,position.z
    local best,err,along=nil,math.huge,0
    for _,s in ipairs(self.segments) do
        local t=N.clamp(((x-s.x)*s.dx+(y-s.y)*s.dy+(z-s.z)*s.dz)/math.max(.00000001,s.length2),0,1)
        local dx,dy,dz=s.x+s.dx*t-x,s.y+s.dy*t-y,s.z+s.dz*t-z
        local e=dx*dx+dy*dy+dz*dz
        if e<err then best,err,along=s,e,t end
    end
    if not best then return self.positions[1],V.forward,0 end
    local s=best;local tangent=s.length>1e-5 and V(s.dx/s.length,s.dy/s.length,s.dz/s.length) or V.zero
    return V(s.x+s.dx*along,s.y+s.dy*along,s.z+s.dz*along),tangent,s.distance+s.length*along
end
function Route:sample(position)
    return self:sample_xyz(position.x,position.y,position.z)
end
function Route:sample_xyz(px,py,pz)
    local x,y,z=math.floor(px/.33),math.floor(py/.33),math.floor(pz/.33)
    local best,err=nil,math.huge
    for radius=1,2 do
        for dx=-radius,radius do for dy=-radius,radius do for dz=-radius,radius do
            if radius==1 or math.abs(dx)==radius or math.abs(dy)==radius or math.abs(dz)==radius then
                local bucket=self.cells[key(x+dx,y+dy,z+dz)]
                if bucket then for _,s in ipairs(bucket) do
                    local ex,ey,ez=s.x-px,s.y-py,s.z-pz
                    local e=ex*ex+ey*ey+ez*ez
                    if e<err then err,best=e,s end
                end end
            end
        end end end
        if best then return best end
    end
    for _,s in ipairs(self.samples) do
        local ex,ey,ez=s.x-px,s.y-py,s.z-pz
        local e=ex*ex+ey*ey+ez*ez;if e<err then err,best=e,s end
    end
    return best
end
local gravity=V(0,-9.81,0)
function Route:step(body,s,speed,p,px,py,pz)
    local ox,oy,oz=px,py,pz
    local dx,dy,dz=px-s.x,py-s.y,pz-s.z
    body:AddForce(gravity,U.ForceMode.Acceleration)
    local side,height=dx*s.sx+dy*s.sy+dz*s.sz,dy
    local sc,hc=N.clamp(side,-.4,.4),N.clamp(height,0,.55)
    local reset=height>1.1 or math.abs(side)>.8
    if side~=sc or height~=hc or reset then
        local forward=dx*s.fx+dy*s.fy+dz*s.fz
        px,py,pz=s.x+s.fx*forward+s.sx*sc,s.y+s.fy*forward+s.sy*sc+hc,s.z+s.fz*forward+s.sz*sc
        p=V(px,py,pz)
        body.position=p
        if reset then body.velocity=V.zero;body.angularVelocity=V.zero;return p,px,py,pz end
        local v=body.velocity
        local vx,vy,vz=v.x,v.y,v.z
        if side~=sc then local dot=vx*s.sx+vy*s.sy+vz*s.sz;vx,vy,vz=vx-s.sx*dot,vy-s.sy*dot,vz-s.sz*dot end
        if height~=hc then vy=0 end
        body.velocity=V(vx,vy,vz)
    end
    -- Apply module-local gravity rather than changing the host's Physics.gravity.
    local fx,fy,fz=s.tx-ox,s.ty-oy,s.tz-oz;local length2=fx*fx+fy*fy+fz*fz
    if length2>.00000001 then local scale=12*speed/math.sqrt(length2);body:AddForce(V(fx*scale,fy*scale,fz*scale),U.ForceMode.Force) end
    return p,px,py,pz
end
return Route
