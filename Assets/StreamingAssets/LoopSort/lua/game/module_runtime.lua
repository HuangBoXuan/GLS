-- API 7 lifecycle coordinator. A real Lua presentation is required; this is
-- not an empty main.lua and never attaches the standalone C# GameController.
local Repository = require('game.level_repository')
local Runtime = {}
local Context = {}
Context.__index = Context

local function live(self)
    assert(not self.closed, 'Module session is closed')
end

local function delta(value)
    assert(type(value) == 'number' and value >= 0 and value < math.huge, 'Invalid frame delta')
    return value
end

local function launch_id(host, repository, kind, view)
    local id = host.Launch and host.Launch.LevelId
    if id == nil or id == '' then
        local ids = repository:list(kind)
        -- The presentation owns account progress. Validate its saved selection
        -- against this catalog before loading, so the home-page preload cannot
        -- replace a returning player's current level with the first level.
        local saved = type(view.initial_level_id) == 'function' and view:initial_level_id(kind) or nil
        for _, candidate in ipairs(ids) do if candidate == saved then return candidate end end
        return ids[1]
    end
    assert(type(id) == 'string' and id:match('^[1-9]%d*$'), 'Launch LevelId must be a positive decimal ID')
    local value = tonumber(id)
    assert(value and value <= 2147483647 and value % 1 == 0, 'Launch LevelId is out of range')
    return value
end

function Context:track(cancel)
    live(self)
    assert(type(cancel) == 'function', 'Cleanup must be a Lua function')
    local action = cancel
    local function once()
        if action then
            action()
            action = nil
        end
    end
    self.cleanups[#self.cleanups+1] = once
    return once
end

-- Use for UI listeners that live across level changes. The returned closure
-- stops retaining the original action when the session is disposed.
function Context:callback(action)
    live(self)
    assert(type(action) == 'function', 'Callback must be a Lua function')
    local target = action
    self:track(function() target = nil end)
    return function(...)
        if not self.closed and target then return target(...) end
    end
end

-- Use for animation/async completions. A new level, undo epoch or exit makes
-- the old callback inert; it cannot mutate a replacement rules instance.
function Context:level_callback(action)
    live(self)
    assert(self.rules, 'Open a level before creating level callbacks')
    assert(type(action) == 'function', 'Callback must be a Lua function')
    local generation, epoch = self.generation, self.rules.d.epoch
    local target = action
    self.level_cleanups[#self.level_cleanups+1] = function() target = nil end
    return function(...)
        if not self.closed and self.rules and self.generation == generation and self.rules.d.epoch == epoch then
            if target then return target(...) end
        else
            target = nil
        end
    end
end

function Context:is_paused()
    return self.closed or self.host_paused or self.menu_paused or self.loading
end

function Context:apply_pause()
    if self.view then self.view:set_paused(self.closed or self.host_paused or self.menu_paused, self.loading) end
end

function Context:set_menu_paused(value)
    live(self)
    assert(type(value) == 'boolean', 'Pause flag must be boolean')
    self.menu_paused = value
    self:apply_pause()
end

function Context:release_ready()
    self.ready_pending = self.ready_pending-1
    if self.ready_pending == 0 then self.loading = false; self:apply_pause() end
end

-- Request during load_level for each async load or entrance sequence. The
-- returned completion is one-shot and invalidated by level replacement/exit.
function Context:hold_ready()
    live(self)
    assert(self.loading and self.rules, 'Readiness can only be held while loading a level')
    self.ready_pending = self.ready_pending+1
    local pending = true
    return self:level_callback(function()
        if pending then pending = false; self:release_ready() end
    end)
end

function Context:open_level(id, kind)
    live(self)
    assert(self.view, 'Presentation is not attached')
    kind = kind or self.kind
    -- Validate/read the new level before invalidating the existing scene.
    local rules, level, spline = self.repository:create(id, kind, {allow_fallback = self.allow_fallback})
    self.loading, self.ready_pending, self.menu_paused = true, 1, false
    self.generation = self.generation+1
    for i = #self.level_cleanups, 1, -1 do self.level_cleanups[i]() end
    self.level_cleanups = {}
    self:apply_pause()
    self.rules, self.level, self.spline, self.kind = rules, level, spline, kind
    local generation = self.generation
    -- load_level owns replacing this module's previous level instances.
    -- If it fails, leave the session paused; propagate the error to the host.
    self.view:load_level(self)
    if not self.closed and self.generation == generation then self:release_ready() end
end

function Context:dispose()
    if self.disposed then return end
    self.closed = true
    if not self.pending_disposal then
        self.generation = self.generation+1
        local actions, view, repository = {}, self.view, self.repository
        if view then
            if type(view.set_paused) == 'function' then actions[#actions+1] = function() view:set_paused(true, true) end end
            if type(view.dispose) == 'function' then actions[#actions+1] = function() view:dispose() end end
        end
        for i = #self.cleanups, 1, -1 do actions[#actions+1] = self.cleanups[i] end
        for i = #self.level_cleanups, 1, -1 do actions[#actions+1] = self.level_cleanups[i] end
        actions[#actions+1] = function() repository:dispose() end
        self.pending_disposal = actions
        self.cleanups, self.level_cleanups = {}, {}
    end
    local remaining, errors = {}, {}
    for _, action in ipairs(self.pending_disposal) do
        local ok, message = pcall(action)
        if not ok then remaining[#remaining+1] = action; errors[#errors+1] = tostring(message) end
    end
    -- Preserve failed cancellation handles for explicit retry / the host's
    -- one disposal callback. Never report success while cleanup still failed.
    self.pending_disposal = remaining
    if #errors > 0 then error('Module cleanup failed: '..table.concat(errors, '; '), 0) end
    self.rules, self.level, self.spline, self.view, self.host, self.repository = nil, nil, nil, nil, nil, nil
    self.pending_disposal, self.disposed = nil, true
end

-- create_view(context, host) must return a Lua presentation implementing:
-- load_level(context), update(context, dt), late_update(context, dt),
-- set_paused(hostOrMenuPaused, entering), dispose(). It owns native physics /
-- animation pause, keeping gameplay bodies inactive during entrance while
-- permitting entrance animation. dispose and unsubscriptions must be retryable.
function Runtime.create(create_view, options)
    assert(type(create_view) == 'function', 'A real module presentation factory is required')
    if options == nil then options = {} end
    assert(type(options) == 'table', 'Module options must be a table')
    local kind = options.kind or 'main'
    assert(kind == 'main' or kind == 'daily', 'Unknown initial catalog kind')
    assert(options.allow_fallback == nil or type(options.allow_fallback) == 'boolean', 'Invalid fallback flag')
    local allow_fallback = options.allow_fallback == true
    local current, pending_disposal
    local entry = {}

    function entry.exit()
        local previous = current or pending_disposal
        current = nil
        pending_disposal = previous
        if previous then previous:dispose() end
        pending_disposal = nil
    end

    function entry.enter(host)
        entry.exit()
        assert(host, 'Module host is required')
        local game_id = host.CurrentGameId
        assert(type(game_id) == 'string' and #game_id <= 64 and game_id:match('^[a-z][a-z0-9_]*$'),
            'Invalid host game identity')
        local repository = Repository.new(function(path) return host:ReadDataText(path) end,
            {root = 'data/'..game_id..'/levels', cache_shards = 2})
        local context = setmetatable({host = host, repository = repository, kind = kind,
            generation = 0, cleanups = {}, level_cleanups = {}, closed = false, loading = true,
            host_paused = false, menu_paused = false, allow_fallback = allow_fallback}, Context)
        current = context
        local ok, message = pcall(function()
            host:TrackCallback(function() context:dispose() end)
            local view = create_view(context, host)
            assert(type(view) == 'table', 'Presentation factory must return a Lua table')
            context.view = view
            for _, name in ipairs({'load_level', 'update', 'late_update', 'set_paused', 'dispose'}) do
                assert(type(context.view[name]) == 'function', 'Presentation is missing '..name)
            end
            context:open_level(launch_id(host, repository, kind, view), kind)
        end)
        if not ok then
            local cleaned, cleanup_error = pcall(entry.exit)
            if not cleaned then message = tostring(message)..'; '..tostring(cleanup_error) end
            error(message, 0)
        end
    end

    function entry.update(dt)
        dt = delta(dt)
        local context = current
        if not context or context.closed or context.host_paused then return end
        local generation = context.generation
        local was_ready = not context.loading
        context.view:update(context, dt)
        if was_ready and current == context and context.generation == generation and not context:is_paused() and not context.booster_busy then
            context.rules:tick(dt, false)
        end
    end

    function entry.late_update(dt)
        dt = delta(dt)
        local context = current
        if context and not context.closed and not context.host_paused then
            context.view:late_update(context, dt)
        end
    end

    function entry.pause()
        if current and not current.closed then current.host_paused = true; current:apply_pause() end
    end

    function entry.resume()
        if current and not current.closed then current.host_paused = false; current:apply_pause() end
    end

    return entry
end

return Runtime
