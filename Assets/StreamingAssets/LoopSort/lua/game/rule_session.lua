-- Portable gameplay state. Input is a decoded JSON level, never a C# DTO.
-- The same sampled conveyor positions determine both density and transport.
local Model = require('game.rule_model')
local Rules = require('game.rule_engine')(Model)
local Spline = require('game.conveyor_spline')
local Json = require('game.json')
local Session = {}

local function optional(source, key)
    local value = source[key]
    if Json.is_null(value) then return nil end
    return value
end

local function number(source, key, default, integer)
    local value = optional(source, key)
    if value == nil then value = default end
    assert(type(value) == 'number' and value == value and math.abs(value) < math.huge,
        'Invalid numeric level field: '..key)
    assert(not integer or value % 1 == 0, 'Expected integer level field: '..key)
    return value
end

local function boolean(source, key)
    local value = optional(source, key)
    assert(value == nil or type(value) == 'boolean', 'Invalid boolean level field: '..key)
    return value == true
end

local function sequence(source, key, convert)
    local input = optional(source, key)
    if input == nil then return Model.Array(0) end
    assert(type(input) == 'table', 'Expected decoded JSON array: '..key)
    -- JSON is one-based. Gameplay collections remain zero-based so cargo IDs
    -- and group boundaries retain their original meaning.
    local count = #input
    for index in pairs(input) do
        assert(type(index) == 'number' and index % 1 == 0 and index >= 1 and index <= count,
            'Expected dense JSON array: '..key)
    end
    local output = Model.Array(count)
    for i = 1, count do
        assert(input[i] ~= nil, 'Missing array element: '..key)
        output[i-1] = convert(input[i])
    end
    return output
end

local function text(value)
    assert(type(value) == 'string', 'Expected string in level data')
    return value
end

local function color(value)
    assert(type(value) == 'number' and value % 1 == 0 and value > 0,
        'Expected positive integer cargo/lock color')
    return value
end

local function group(source)
    assert(type(source) == 'table' and not Json.is_null(source), 'Invalid cargo group')
    return {
        color = color(source.color), keyColor = number(source, 'keyColor', 0, true),
        hidden = boolean(source, 'hidden'), sticky = boolean(source, 'sticky'), raw = optional(source, 'raw'),
    }
end

local function carrier(source)
    assert(type(source) == 'table' and not Json.is_null(source), 'Invalid carrier')
    local id = text(source.id)
    assert(id ~= '', 'Carrier ID must not be empty')
    return {
        id = id, x = number(source, 'x', 0), y = number(source, 'y', -1),
        z = number(source, 'z', 0), yaw = number(source, 'yaw', 0),
        groups = sequence(source, 'groups', group),
        curtainColor = number(source, 'curtainColor', 0, true),
        barrier = boolean(source, 'barrier'), generator = boolean(source, 'generator'),
        tagColor = number(source, 'tagColor', 0, true),
        capacityGroups = number(source, 'capacityGroups', 4, true),
        targetColor = number(source, 'targetColor', 0, true),
        iceRequires = sequence(source, 'iceRequires', text),
        lockColors = sequence(source, 'lockColors', color),
        ropeLocks = sequence(source, 'ropeLocks', text),
        carpetLocks = sequence(source, 'carpetLocks', text),
        carpetOrder = number(source, 'carpetOrder', 0, true),
    }
end

local function point(source)
    assert(type(source) == 'table' and not Json.is_null(source), 'Invalid conveyor control point')
    return { order = number(source, 'order', 0, true), x = number(source, 'x', 0),
        y = number(source, 'y', 0), z = number(source, 'z', 0) }
end

function Session.level(source, sampled_length)
    assert(type(source) == 'table' and not Json.is_null(source), 'Expected decoded JSON level')
    local offset = optional(source, 'cameraOffset')
    if offset == nil then offset = {} end
    assert(type(offset) == 'table', 'Invalid camera offset')
    local level = {
        id = number(source, 'id', 0, true), slots = number(source, 'slots', 0, true),
        colorMix = number(source, 'colorMix', 0, true),
        sourceCarrier = number(source, 'sourceCarrier', 0, true),
        sourceSpline = number(source, 'sourceSpline', 0, true),
        subdivide = number(source, 'subdivide', 0, true), theme = optional(source, 'theme'),
        cameraRotation = number(source, 'cameraRotation', 0),
        cameraOffset = { X = number(offset, 'X', 0), Y = number(offset, 'Y', 0) },
        closed = boolean(source, 'closed'),
        timeLimitSeconds = number(source, 'timeLimitSeconds', 0),
        points = sequence(source, 'points', point),
        carriers = sequence(source, 'carriers', carrier),
        spares = sequence(source, 'spares', carrier),
        unsupported = sequence(source, 'unsupported', text),
    }
    assert(level.id > 0 and level.slots > 0 and level.carriers.Length > 0, 'Invalid level identity/capacity')
    assert(level.subdivide >= 0 and level.timeLimitSeconds >= 0, 'Invalid level subdivision/time limit')
    local spline
    if sampled_length == nil and level.points.Length >= 2 then
        local points = {}
        for i = 0, level.points.Length-1 do points[i+1] = level.points[i] end
        spline = Spline.new(points, level.closed, level.subdivide)
        sampled_length = spline.length
    end
    -- Never substitute the serialized estimate or a control-point polyline.
    -- An existing presentation may instead pass its own sampled path length.
    assert(sampled_length == nil or (type(sampled_length) == 'number' and
        sampled_length >= 0 and sampled_length < math.huge), 'Invalid sampled conveyor length')
    assert(level.points.Length < 3 or (sampled_length and sampled_length > 0),
        'The sampled conveyor must have positive length')
    level.splineLength = sampled_length or 0
    level.IsSupported = level.unsupported.Length == 0
    local ids = {}
    for _, collection in ipairs({level.carriers, level.spares}) do
        for i = 0, collection.Length-1 do
            local id = collection[i].id
            assert(not ids[id], 'Duplicate carrier ID: '..id)
            ids[id] = true
        end
    end
    return level, spline
end

function Session.new(source, sampled_length, allow_fallback)
    local level, spline = Session.level(source, sampled_length)
    local rules = Rules.new(Model.RuleStateData())
    rules:initialize(level, allow_fallback == true)
    -- State, callbacks and snapshots belong to this instance. A platform
    -- presentation owns ticking/animations and drops the instance on exit.
    return rules, level, spline
end

Session.Model = Model
return Session
