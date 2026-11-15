-- Bounded per-module catalog ownership. SDK ReadDataText caps each file at
-- 4 MiB, so catalogs use an index plus small shards in the game's data root.
local Json = require('game.json')
local Session = require('game.rule_session')
local Repository = {}
Repository.__index = Repository

local max_bytes = 4*1024*1024
local states = setmetatable({}, { __mode = 'k' })

local function live(self)
    local state = states[self]
    assert(state and not state.disposed, 'Level repository is disposed')
    return state
end

local function kind_name(kind)
    if kind == nil then return 'main' end
    assert(kind == 'main' or kind == 'daily', 'Unknown level catalog kind')
    return kind
end

local function positive_id(id)
    assert(type(id) == 'number' and id > 0 and id < math.huge and id % 1 == 0,
        'Level ID must be a positive integer')
    return id
end

local function root_path(path)
    assert(type(path) == 'string', 'Catalog root is required')
    local game_id = path:match('^data/([a-z][a-z0-9_]*)/levels$')
    assert(game_id and #game_id <= 64, 'Catalog root must be data/<gameId>/levels')
    return path
end

local function dense_count(array, description)
    assert(type(array) == 'table' and not Json.is_null(array), description..' must be an array')
    local count = #array
    assert(count > 0, description..' must not be empty')
    for key in pairs(array) do
        assert(type(key) == 'number' and key % 1 == 0 and key >= 1 and key <= count,
            description..' must be a dense array')
    end
    for i = 1, count do assert(array[i] ~= nil, description..' contains a missing entry') end
    return count
end

local function decode_file(state, relative)
    local path = state.root..'/'..relative
    local text = state.read_text(path)
    assert(type(text) == 'string' and #text > 0, 'Missing catalog data: '..path)
    assert(#text <= max_bytes, 'Catalog file exceeds SDK ReadDataText limit: '..path)
    local value = Json.decode(text, { max_bytes = max_bytes, max_depth = 64 })
    assert(type(value) == 'table' and not Json.is_null(value), 'Invalid catalog document: '..path)
    return value
end

local function load_index(state)
    if state.index then return state.index end
    local document = decode_file(state, 'index.json')
    assert(document.schema == 1, 'Unsupported level index schema')
    local catalogs = document.catalogs
    assert(type(catalogs) == 'table' and not Json.is_null(catalogs), 'Missing indexed catalogs')
    for kind in pairs(catalogs) do kind_name(kind) end
    local index, paths = {}, {}
    for _, kind in ipairs({'main', 'daily'}) do
        local catalog = catalogs[kind]
        assert(type(catalog) == 'table' and not Json.is_null(catalog), 'Missing '..kind..' catalog')
        assert(type(catalog.version) == 'string' and catalog.version:find('%S'), 'Invalid catalog version')
        local count = dense_count(catalog.shards, 'Catalog shards')
        local by_id, ids = {}, {}
        for i = 1, count do
            local shard = catalog.shards[i]
            assert(type(shard) == 'table' and not Json.is_null(shard), 'Invalid shard descriptor')
            local path = shard.path
            assert(type(path) == 'string' and path:match('^'..kind..'/%d%d%d%d%.json$'),
                'Invalid catalog shard path')
            assert(not paths[path], 'Duplicate shard path: '..path)
            paths[path] = true
            local size = dense_count(shard.ids, 'Shard IDs')
            local descriptor = { path = path, ids = {}, count = size, version = catalog.version }
            for j = 1, size do
                local id = positive_id(shard.ids[j])
                assert(not by_id[id], 'Duplicate '..kind..' level ID: '..tostring(id))
                descriptor.ids[id], by_id[id], ids[#ids+1] = true, descriptor, id
            end
        end
        table.sort(ids)
        index[kind] = { by_id = by_id, ids = ids, version = catalog.version }
    end
    -- Invalid indexes never replace a previously usable repository.
    state.index = index
    return index
end

local function touch(state, entry)
    state.clock = state.clock+1
    entry.used = state.clock
    return entry
end

local function load_shard(state, descriptor)
    local cached = state.cache[descriptor.path]
    if cached then return touch(state, cached) end
    local shard = decode_file(state, descriptor.path)
    assert(shard.version == descriptor.version, 'Shard version differs from catalog index')
    local count = dense_count(shard.levels, 'Shard levels')
    assert(count == descriptor.count, 'Shard level count differs from catalog index')
    local by_id = {}
    for i = 1, count do
        local level = shard.levels[i]
        assert(type(level) == 'table' and not Json.is_null(level), 'Invalid shard level')
        local id = positive_id(level.id)
        assert(descriptor.ids[id] and not by_id[id], 'Shard IDs differ from catalog index')
        by_id[id] = level
    end
    -- Distinct matching IDs plus equal counts guarantee the complete ID set.
    -- Evict only after validation so a failed read preserves other cached shards.
    if state.cache_count >= state.cache_limit then
        local oldest_path, oldest_time
        for path, entry in pairs(state.cache) do
            if not oldest_time or entry.used < oldest_time then oldest_path, oldest_time = path, entry.used end
        end
        state.cache[oldest_path] = nil
        state.cache_count = state.cache_count-1
    end
    cached = touch(state, { by_id = by_id })
    state.cache[descriptor.path], state.cache_count = cached, state.cache_count+1
    return cached
end

local function find(self, id, kind)
    positive_id(id)
    kind = kind_name(kind)
    local state = live(self)
    local descriptor = load_index(state)[kind].by_id[id]
    assert(descriptor, 'Unknown '..kind..' level ID: '..tostring(id))
    return load_shard(state, descriptor).by_id[id]
end

local function copy_json(value)
    if type(value) ~= 'table' or Json.is_null(value) then return value end
    local copy = {}
    for key, child in pairs(value) do copy[key] = copy_json(child) end
    return copy
end

function Repository.new(read_text, options)
    assert(type(read_text) == 'function', 'Catalog reader must be a function')
    assert(type(options) == 'table' and not Json.is_null(options), 'Catalog options with root are required')
    local root = root_path(options.root)
    local limit = options.cache_shards
    if limit == nil then limit = 2 end
    assert(type(limit) == 'number' and limit % 1 == 0 and limit >= 1 and limit <= 2,
        'cache_shards must be 1 or 2')
    local self = setmetatable({}, Repository)
    states[self] = { read_text = read_text, root = root, index = nil, cache = {},
        cache_limit = limit, cache_count = 0, clock = 0, disposed = false }
    return self
end

function Repository:get(id, kind)
    -- Callers can edit this copy without corrupting later runs or changing a
    -- cached level's ID after it was indexed.
    return copy_json(find(self, id, kind))
end

function Repository:list(kind)
    kind = kind_name(kind)
    local source, copy = load_index(live(self))[kind].ids, {}
    for i = 1, #source do copy[i] = source[i] end
    return copy
end

function Repository:create(id, kind, options)
    assert(options == nil or (type(options) == 'table' and not Json.is_null(options)),
        'Session options must be a table')
    options = options or {}
    assert(options.allow_fallback == nil or type(options.allow_fallback) == 'boolean',
        'allow_fallback must be a boolean')
    return Session.new(find(self, id, kind), options.sampled_length, options.allow_fallback)
end

function Repository:reset()
    local state = live(self)
    state.index, state.cache, state.cache_count, state.clock = nil, {}, 0, 0
end

function Repository:dispose()
    local state = states[self]
    if not state or state.disposed then return end
    state.cache, state.index, state.root, state.read_text, state.disposed = nil, nil, nil, nil, true
end

return Repository
