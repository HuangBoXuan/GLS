-- Presentation spacing only. Rule slots and level data keep their identities.
-- Both Unity presentation paths pass the actual cart renderer footprints here.
local Json=require('game.json')
local M={}
function M.solve(cells)
    local parents,owners={},{}
    local function find(i)
        while parents[i]~=i do parents[i]=parents[parents[i]];i=parents[i] end
        return i
    end
    for i in ipairs(cells) do parents[i]=i end
    for i,c in ipairs(cells) do for _,rope in ipairs(c.ropes or {}) do
        if owners[rope] then parents[find(i)]=find(owners[rope]) else owners[rope]=i end
    end end
    local units,by_root={},{}
    for i,c in ipairs(cells) do
        local root=find(i);local u=by_root[root]
        if not u then
            u={box={c.box[1],c.box[2],c.box[3],c.box[4]},shift={0,0},axis=c.axis,away=c.away or 1,members={},rope=false}
            by_root[root]=u;units[#units+1]=u
        end
        u.members[#u.members+1]=i;u.rope=u.rope or #(c.ropes or {})>0
        for a=1,2 do u.box[a]=math.min(u.box[a],c.box[a]);u.box[a+2]=math.max(u.box[a+2],c.box[a+2]) end
    end
    local function margin(u) return u.rope and .72 or 0 end
    local function gap(a,b,axis)
        if a.rope or b.rope then return .14+margin(a)+margin(b) end
        return b.box[axis]-a.box[axis+2]
    end
    -- Reserve the same depth that the loop adds beyond the cart's mouth.
    -- Move the complete row away from its outlet so the front chain does not
    -- occupy the conveyor edge while the lateral gaps are opened.
    local reserved={}
    for _,anchor in ipairs(units) do if anchor.rope then
        local other=2-anchor.axis;local center=(anchor.box[other]+anchor.box[other+2])*.5
        for _,u in ipairs(units) do
            local rowCenter=(u.box[other]+u.box[other+2])*.5
            if not reserved[u] and u.axis==anchor.axis and u.away==anchor.away and math.abs(rowCenter-center)<.2 then
                u.shift[other]=u.shift[other]+.72*anchor.away;reserved[u]=true
            end
        end
    end end
    for pass=1,3 do for _,anchor in ipairs(units) do if anchor.rope then
        local axis=anchor.axis+1;local other=3-axis;local row={}
        for _,u in ipairs(units) do
            if u.axis==anchor.axis and u.box[other+2]+u.shift[other]+margin(u)>anchor.box[other]+anchor.shift[other]-margin(anchor)
                and u.box[other]+u.shift[other]-margin(u)<anchor.box[other+2]+anchor.shift[other]+margin(anchor) then row[#row+1]=u end
        end
        table.sort(row,function(a,b) return a.box[axis]+a.box[axis+2]<b.box[axis]+b.box[axis+2] end)
        local pivot=1;for i,u in ipairs(row) do if u==anchor then pivot=i end end
        for i=pivot+1,#row do
            local a,b=row[i-1],row[i]
            local delta=a.box[axis+2]+a.shift[axis]+gap(a,b,axis)-b.box[axis]-b.shift[axis]
            if delta>0 then b.shift[axis]=b.shift[axis]+delta end
        end
        for i=pivot-1,1,-1 do
            local a,b=row[i],row[i+1]
            local delta=b.box[axis]+b.shift[axis]-gap(a,b,axis)-a.box[axis+2]-a.shift[axis]
            if delta<0 then a.shift[axis]=a.shift[axis]+delta end
        end
    end end end
    local result={};for _,u in ipairs(units) do for _,i in ipairs(u.members) do result[i]={u.shift[1],u.shift[2]} end end
    return result
end
function M.offsets(encoded)
    local rows={}
    for i,offset in ipairs(M.solve(Json.decode(encoded))) do rows[i]=string.format('[%.9g,%.9g]',offset[1],offset[2]) end
    return '['..table.concat(rows,',')..']'
end
return M
