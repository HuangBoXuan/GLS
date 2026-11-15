-- Portable position/distance sampling from NativeSpline.cs (BSpline, sampleRate=8).
-- JSON input, positions and distances are ordinary one-based Lua arrays.
-- This module does not implement tangent Slerp, track meshes or physics.
local Spline = {}
Spline.__index = Spline

local floor, min, max, sqrt = math.floor, math.min, math.max, math.sqrt
local pack, unpack = string.pack, string.unpack

local function finite(value, name)
    assert(type(value) == 'number' and value == value and math.abs(value) < math.huge,
        'Invalid spline '..name)
    return value
end

-- Only construction uses this conversion. NativeSpline builds Vector3/float
-- samples, so retaining double precision here can change density thresholds.
local function f32(value)
    return (unpack('<f', pack('<f', value)))
end

local function clamp01(value)
    return max(0, min(1, value))
end

local function copy(point)
    return { x = point.x, y = point.y, z = point.z }
end

local function coordinate(point, key)
    local value = point[key]
    return finite(value == nil and 0 or value, 'control '..key)
end

local function midpoint(a, b)
    return { x = f32(f32(a.x+b.x)*.5), y = f32(f32(a.y+b.y)*.5), z = f32(f32(a.z+b.z)*.5) }
end

local function extrapolate(a, b, factor)
    return {
        x = f32(a.x+f32(f32(b.x-a.x)*factor)),
        y = f32(a.y+f32(f32(b.y-a.y)*factor)),
        z = f32(a.z+f32(f32(b.z-a.z)*factor)),
    }
end

local function control(points, closed, index)
    local count = #points
    -- index is zero-based here, matching the C# Control method.
    if closed then return points[index % count + 1] end
    if index < 0 then return extrapolate(points[1], points[2], index) end
    if index >= count then
        return extrapolate(points[count], points[count-1], -(index-count+1))
    end
    return points[index+1]
end

local function sample_component(a, b, c, d, w0, w1, w2, w3)
    return f32(f32(f32(f32(a*w0)+f32(b*w1))+f32(c*w2))+f32(d*w3)) / 6
end

local function sample(points, closed, percent)
    local scaled = clamp01(percent)*(closed and #points or #points-1)
    local segment = floor(scaled)
    local t = f32(scaled-segment)
    local t2 = f32(t*t)
    local t3 = f32(t2*t)
    local w0 = f32(f32(f32(1-f32(3*t))+f32(3*t2))-t3)
    local w1 = f32(f32(4-f32(6*t2))+f32(3*t3))
    local w2 = f32(f32(f32(1+f32(3*t))+f32(3*t2))-f32(3*t3))
    local a, b = control(points, closed, segment-1), control(points, closed, segment)
    local c, d = control(points, closed, segment+1), control(points, closed, segment+2)
    return {
        x = f32(sample_component(a.x, b.x, c.x, d.x, w0, w1, w2, t3)),
        y = f32(sample_component(a.y, b.y, c.y, d.y, w0, w1, w2, t3)),
        z = f32(sample_component(a.z, b.z, c.z, d.z, w0, w1, w2, t3)),
    }
end

local function distance(a, b)
    local x, y, z = f32(a.x-b.x), f32(a.y-b.y), f32(a.z-b.z)
    return f32(sqrt(f32(f32(f32(x*x)+f32(y*y))+f32(z*z))))
end

function Spline.new(points, closed, subdivide, surface)
    assert(type(points) == 'table' and #points >= 2, 'A conveyor requires at least two control points')
    assert(closed == nil or type(closed) == 'boolean', 'Invalid spline closed flag')
    closed = closed == true
    subdivide = finite(subdivide == nil and 0 or subdivide, 'subdivide')
    assert(subdivide >= 0 and subdivide % 1 == 0, 'Spline subdivision must be a nonnegative integer')
    surface = f32(finite(surface == nil and 1.1 or surface, 'surface')) -- TrackGeometry.Surface
    local controls = {}
    for index in pairs(points) do
        assert(type(index) == 'number' and index % 1 == 0 and index >= 1 and index <= #points,
            'Spline controls must be a dense JSON array')
    end
    for i = 1, #points do
        local point = points[i]
        assert(type(point) == 'table', 'Invalid spline control point')
        controls[i] = {
            x = f32(coordinate(point, 'x')),
            y = f32(surface+f32(coordinate(point, 'y'))),
            z = f32(coordinate(point, 'z')),
        }
    end
    for _ = 1, subdivide do
        local expanded = {}
        for i = 1, #controls do
            expanded[#expanded+1] = controls[i]
            if closed or i < #controls then
                expanded[#expanded+1] = midpoint(controls[i], controls[i % #controls + 1])
            end
        end
        controls = expanded
    end

    local self = setmetatable({ closed = closed, positions = {}, distances = {} }, Spline)
    local intervals = 7*(closed and #controls or #controls-1)
    local accumulated = 0
    for i = 0, intervals do
        local point = sample(controls, closed, i/intervals)
        if i > 0 then accumulated = f32(accumulated+distance(self.positions[i], point)) end
        self.positions[i+1], self.distances[i+1] = point, accumulated
    end
    self.length, self.sample_count = finite(accumulated, 'length'), intervals+1
    return self
end

local function percent_span(self, percent)
    local scaled = clamp01(finite(percent, 'percent'))*(self.sample_count-1)
    local index = min(floor(scaled), self.sample_count-2)
    return index+1, scaled-index
end

local function lerp(a, b, t)
    return { x = a.x+(b.x-a.x)*t, y = a.y+(b.y-a.y)*t, z = a.z+(b.z-a.z)*t }
end

-- Matches the sampled position returned by NativeSpline.Evaluate, rather
-- than evaluating a different analytic curve between the cached samples.
function Spline:position(percent)
    local index, t = percent_span(self, percent)
    return lerp(self.positions[index], self.positions[index+1], t)
end

function Spline:distance_at_percent(percent)
    local index, t = percent_span(self, percent)
    local first, last = self.distances[index], self.distances[index+1]
    return first+(last-first)*t
end

function Spline:at_distance(value)
    value = finite(value, 'distance')
    if self.length <= 0 then return copy(self.positions[1]) end
    value = self.closed and value % self.length or max(0, min(self.length, value))
    local first, last = 2, self.sample_count
    while first < last do
        local middle = floor((first+last)/2)
        if self.distances[middle] < value then first = middle+1 else last = middle end
    end
    local begin, finish = self.distances[first-1], self.distances[first]
    local t = finish ~= begin and clamp01((value-begin)/(finish-begin)) or 0
    return lerp(self.positions[first-1], self.positions[first], t)
end

return Spline
