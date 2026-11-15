-- Pure Lua data model injected into game.rule_engine.
-- Collections deliberately retain the existing zero-based DTO contract.
local Model = {}

Model.PieceLocation = { Cargo = 0, Releasing = 1, Belt = 2, Receiving = 3, Packed = 4 }
Model.RunStatus = { Playing = 0, Won = 1, Failed = 2 }
Model.FailReason = { None = -1, ConveyorFull = 0, CantCompleteCarrier = 1, TimeUp = 2 }

-- Keep values outside the proxy so every numeric assignment is bounds checked,
-- including assignments to existing slots. Count also tracks unfilled arrays.
local collection_states = setmetatable({}, { __mode = "k" })
local collection_methods = {}
local collection_meta = {}

local function nonnegative_integer(value, name)
    assert(type(value) == "number" and value >= 0 and value < math.huge and value == math.floor(value),
        (name or "Value") .. " must be a nonnegative integer")
    return value
end

local function state_of(collection)
    local state = collection_states[collection]
    assert(state, "Expected a rule model collection")
    return state
end

local function checked_index(state, index)
    nonnegative_integer(index, "Index")
    assert(index < state.count, "Collection index is out of range")
    return index + 1
end

local function mutable_list(collection)
    local state = state_of(collection)
    assert(not state.fixed, "Cannot resize a fixed-length array")
    return state
end

local function new_collection(count, fixed, default)
    local collection, items = {}, {}
    for i = 1, count do items[i] = default end
    collection_states[collection] = { items = items, count = count, fixed = fixed }
    return setmetatable(collection, collection_meta)
end

-- Ordinary Lua sequences are one-based. Explicit Count/Length tables and model
-- collections are zero-based; this conversion never shifts a piece identity.
local function sequence_reader(source)
    assert(type(source) == "table", "Expected a Lua sequence or rule model collection")
    local state = collection_states[source]
    if state then
        return state.count, function(index) return state.items[index + 1] end
    end
    local count = rawget(source, "Length")
    if count == nil then count = rawget(source, "Count") end
    if count ~= nil then
        nonnegative_integer(count, "Sequence length")
        return count, function(index) return source[index] end
    end
    return #source, function(index) return source[index + 1] end
end

local function copy_sequence(source, fixed, clone)
    assert(clone == nil or type(clone) == "function", "Clone must be a function")
    local count, read = sequence_reader(source)
    local result = new_collection(count, fixed)
    local items = state_of(result).items
    for index = 0, count - 1 do
        local value = read(index)
        if clone and value ~= nil then value = clone(value) end
        items[index + 1] = value
    end
    return result
end

function Model.List(sequence)
    if sequence == nil then return new_collection(0, false) end
    return copy_sequence(sequence, false)
end

function Model.Array(count, default)
    return new_collection(nonnegative_integer(count, "Array length"), true, default)
end

function Model.ArrayFrom(sequence, clone)
    return copy_sequence(sequence, true, clone)
end

function collection_methods:Add(value)
    local state = mutable_list(self)
    state.count = state.count + 1
    state.items[state.count] = value
end

function collection_methods:Clear()
    local state = mutable_list(self)
    state.items, state.count = {}, 0
end

function collection_methods:Contains(value)
    local state = state_of(self)
    for i = 1, state.count do
        if state.items[i] == value then return true end
    end
    return false
end

function collection_methods:RemoveAt(index)
    local state = mutable_list(self)
    local first = checked_index(state, index)
    for i = first, state.count - 1 do state.items[i] = state.items[i + 1] end
    state.items[state.count] = nil
    state.count = state.count - 1
end

function collection_methods:Remove(value)
    local state = mutable_list(self)
    for i = 1, state.count do
        if state.items[i] == value then
            self:RemoveAt(i - 1)
            return true
        end
    end
    return false
end

function collection_methods:RemoveRange(index, count)
    local state = mutable_list(self)
    nonnegative_integer(index, "Index")
    nonnegative_integer(count, "Range count")
    assert(index <= state.count and count <= state.count - index, "Collection range is out of bounds")
    if count == 0 then return end
    local remaining = state.count - count
    for i = index + 1, remaining do state.items[i] = state.items[i + count] end
    for i = remaining + 1, state.count do state.items[i] = nil end
    state.count = remaining
end

-- Like List<T>.ToArray(), this copies the container, not referenced objects.
-- Snapshot creation explicitly calls PieceState/CarrierState.Copy separately.
function collection_methods:ToArray()
    return Model.ArrayFrom(self)
end

function collection_methods:get_Item(index)
    local state = state_of(self)
    return state.items[checked_index(state, index)]
end

function collection_methods:set_Item(index, value)
    local state = state_of(self)
    state.items[checked_index(state, index)] = value
end

collection_meta.__index = function(collection, key)
    local state = state_of(collection)
    if type(key) == "number" then return state.items[checked_index(state, key)] end
    if key == "Count" or key == "Length" then return state.count end
    return collection_methods[key]
end

collection_meta.__newindex = function(collection, key, value)
    assert(type(key) == "number", "Collection properties are read-only")
    local state = state_of(collection)
    state.items[checked_index(state, key)] = value
end

collection_meta.__len = function(collection) return state_of(collection).count end
collection_meta.__pairs = function(collection)
    local state, index = state_of(collection), -1
    return function()
        index = index + 1
        if index < state.count then return index, state.items[index + 1] end
    end
end
collection_meta.__metatable = "Rule collection"

local piece_methods = {}
local piece_meta = { __index = piece_methods }

function Model.PieceState()
    return setmetatable({
        id = 0, color = 0, carrier = 0, hidden = false,
        keyColor = 0, sticky = false, bondedGroup = -1,
        transferVersion = 0, receiveTarget = -1,
        loopCarrier = -1, loopCount = 0, location = Model.PieceLocation.Cargo,
    }, piece_meta)
end

function piece_methods:Copy()
    local copy = Model.PieceState()
    for key, value in pairs(self) do copy[key] = value end
    return copy
end

local carrier_methods = {}
local carrier_meta = {}

function Model.CarrierState()
    return setmetatable({
        index = 0, id = nil, packed = false,
        cargo = Model.List(), incoming = Model.List(),
        releasing = 0, lastReleasedColor = 0,
        curtainColor = 0, curtainOpen = true,
        barrier = false, generator = false, iceBroken = false, tagColor = 0,
        capacityGroups = 4, targetColor = 0,
        iceRequires = Model.Array(0), lockColors = Model.List(),
        ropeLocks = Model.List(), carpetLocks = Model.List(), carpetOrder = 0,
    }, carrier_meta)
end

carrier_meta.__index = function(carrier, key)
    if key == "Frozen" then return carrier.iceRequires.Length > 0 and not carrier.iceBroken end
    if key == "Accessible" then
        return carrier.curtainOpen and not carrier.Frozen and carrier.lockColors.Count == 0 and carrier.carpetLocks.Count == 0
    end
    if key == "CanEmit" then return carrier.Accessible and not carrier.barrier and carrier.ropeLocks.Count == 0 end
    return carrier_methods[key]
end

carrier_meta.__newindex = function(carrier, key, value)
    assert(key ~= "Frozen" and key ~= "Accessible" and key ~= "CanEmit", "Carrier access properties are read-only")
    rawset(carrier, key, value)
end

function carrier_methods:Copy()
    return setmetatable({
        index = self.index, id = self.id, packed = self.packed,
        cargo = Model.List(self.cargo), incoming = Model.List(self.incoming),
        releasing = self.releasing, lastReleasedColor = self.lastReleasedColor,
        curtainColor = self.curtainColor, curtainOpen = self.curtainOpen,
        barrier = self.barrier, generator = self.generator, iceBroken = self.iceBroken, tagColor = self.tagColor,
        capacityGroups = self.capacityGroups, targetColor = self.targetColor,
        iceRequires = Model.ArrayFrom(self.iceRequires), lockColors = Model.List(self.lockColors),
        ropeLocks = Model.List(self.ropeLocks), carpetLocks = Model.List(self.carpetLocks), carpetOrder = self.carpetOrder,
    }, carrier_meta)
end

function Model.RuleStateData()
    return {
        fragments = nil, pieces = Model.List(), carriers = Model.List(), keyCollections = Model.List(),
        beltCapacity = 0, beltCount = 0, beltOccupiedSlots = 0, pendingTransfers = 0,
        packedCount = 0, epoch = 0, featureRevision = 0,
        status = Model.RunStatus.Playing, failReason = Model.FailReason.None,
        elapsedTime = 0, cannotCompleteTime = 0, conveyorSpeedScale = 1,
    }
end

function Model.StateSnapshot()
    return {
        pieces = nil, carriers = nil, beltCapacity = 0,
        status = Model.RunStatus.Playing, failReason = Model.FailReason.None,
        elapsedTime = 0, cannotCompleteTime = 0,
    }
end

function Model.ReleaseResult()
    return { accepted = false, reason = nil, pieces = Model.Array(0), interruptedReceives = Model.Array(0) }
end

function Model.FragmentLayout(columns, rows, depth)
    nonnegative_integer(columns, "Columns")
    nonnegative_integer(rows, "Rows")
    nonnegative_integer(depth, "Depth")
    assert(columns > 0 and rows > 0 and depth > 0, "Fragment dimensions must be positive")
    local values = {
        Columns = columns, Rows = rows, Depth = depth, Count = columns * rows * depth,
        CellSize = math.min(1.503 / columns, 1.503 / rows, 1.002 / depth) * .97,
    }
    return setmetatable({}, {
        __index = values,
        __newindex = function() error("Fragment layout is read-only", 2) end,
        __metatable = "Fragment layout",
    })
end

Model.GameState = {
    KeyCollection = function() return { carrier = 0, slot = 0, color = 0 } end,
}

Model.RuleArrays = {
    Ints = function(count) return Model.Array(count, 0) end,
    Pieces = function(count) return Model.Array(count) end,
    Carriers = function(count) return Model.Array(count) end,
    IntList = function() return Model.List() end,
}
local function copy_states(source)
    local result=Model.Array(source.Count)
    for i=0,source.Count-1 do result[i]=source[i]:Copy() end
    return result
end
local function restore_states(target,source)
    target:Clear()
    for i=0,source.Length-1 do target:Add(source[i]:Copy()) end
end
Model.RuleArrays.CopyPieces=copy_states
Model.RuleArrays.CopyCarriers=copy_states
Model.RuleArrays.RestorePieces=restore_states
Model.RuleArrays.RestoreCarriers=restore_states

return Model
