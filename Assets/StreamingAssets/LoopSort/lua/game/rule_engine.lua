-- One rules implementation for the standalone view bridge and pure Lua sessions.
-- The injected model owns data construction, enums and zero-based collections.
return function(Core)
local Location, Status, Failure = Core.PieceLocation, Core.RunStatus, Core.FailReason
local Rules = {}
Rules.__index = Rules
local function length(a) return a and a.Length or 0 end
local function floor_group(value, count) return math.floor(value / count) * count end
local function reject(reason) local r = Core.ReleaseResult(); r.reason = reason; return r end
local function clear(t) for k in pairs(t) do t[k] = nil end end
-- APK 4.6.0 constructors: LoseConfig (0x24FE17C), PaceConfig (0x24FE1AC).
-- Keep remote switches explicit: their server-side overrides are not part of the APK.
local native_policy = {
    fail_on_full = false, loop_fail_threshold = 0,
    slower_when_empty = true, faster_when_full = false, hold_to_speed = false,
    hold_speed_multiplier = 1,
    empty_speed_scale = .2, fast_speed_scale = 1.8,
    fail_instead_of_breaking_ice = false,
}

function Rules.new(data)
    return setmetatable({ d = data, colors = {}, preferences = {}, cargo_summaries = {}, belt_groups = {}, occupied_slots = 0,
        belt_ids = {}, loop_ids = {}, receiving = {}, reveal_dirty = {}, key_groups = {}, outgoing_plans = {} }, Rules)
end

function Rules.fragment_layout(level)
    if length(level.points) < 3 then return Core.FragmentLayout(3, 3, 2) end
    local distance = level.splineLength
    assert(distance > 0, "The sampled conveyor length must be resolved before selecting block density")
    local candidates = {{3,3},{4,3},{4,4},{5,4},{5,5},{6,5},{6,6},{7,6},{7,7}}
    local target, best, chosen = distance / math.max(1, level.slots+(native_policy.fail_on_full and 1 or 0)) * 8, math.huge, candidates[1]
    for _, c in ipairs(candidates) do
        local error = math.abs(c[1] * c[2] * 2 - target)
        if error <= best then best, chosen = error, c end
    end
    return Core.FragmentLayout(chosen[1], chosen[2], 2)
end

function Rules:initialize(level, allow_fallback)
    assert(level and level.carriers and level.slots >= 1, "Invalid level")
    assert(level.IsSupported or allow_fallback, "Unverified special mechanic")
    local d = self.d
    self.level_id, self.time_limit, self.check_elapsed = level.id, level.timeLimitSeconds, 0
    d.fragments = Rules.fragment_layout(level)
    self.group_size, self.capacity = d.fragments.Count, d.fragments.Count * 4
    d.beltCapacity = (level.slots+(native_policy.fail_on_full and 1 or 0)) * self.group_size
    for i = 0, level.carriers.Length - 1 do
        local source = level.carriers[i]
        local carrier = self:add_carrier_data(source)
        local previous = -1
        local summary = {count=0,colors={},key=false}
        for j = 0, length(source.groups)-1 do
            local group, group_id = source.groups[j], math.floor(d.pieces.Count / self.group_size)
            local color,hidden,key,sticky = group.color,group.hidden,group.keyColor,group.sticky
            assert(color > 0, "Cargo color must be valid")
            if key > 0 then
                local groups = self.key_groups[key]
                if not groups then groups = {}; self.key_groups[key] = groups end
                groups[#groups+1] = d.pieces.Count
            end
            summary.count = summary.count+self.group_size
            summary.colors[color] = (summary.colors[color] or 0)+self.group_size
            summary.key = summary.key or key > 0
            for _ = 1, self.group_size do
                local p = Core.PieceState()
                p.id, p.color, p.carrier = d.pieces.Count, color, carrier.index
                p.hidden, p.keyColor, p.sticky = hidden, key, sticky
                p.bondedGroup, p.location = sticky and previous or -1, Location.Cargo
                carrier.cargo:Add(p.id); d.pieces:Add(p)
            end
            previous = group_id
        end
        assert(carrier.cargo.Count <= self:carrier_capacity(carrier), "Carrier has too much cargo")
        self.cargo_summaries[carrier.index] = summary
        self.reveal_dirty[carrier.index] = true
    end
    assert(d.pieces.Count > 0, "Level contains no cargo")
    self:reveal_accessible_groups()
end

function Rules:add_carrier(id)
    local c = Core.CarrierState(); c.id, c.index = id, self.d.carriers.Count
    self.d.carriers:Add(c); clear(self.preferences); return c
end
function Rules:add_carrier_data(source)
    local c = self:add_carrier(source.id)
    c.curtainColor, c.curtainOpen = source.curtainColor, source.curtainColor <= 0
    c.barrier, c.generator, c.tagColor = source.barrier, source.generator, source.tagColor
    c.capacityGroups, c.targetColor = math.max(4,source.capacityGroups), source.targetColor
    if source.iceRequires then c.iceRequires = source.iceRequires end
    for j = 0,length(source.lockColors)-1 do c.lockColors:Add(source.lockColors[j]) end
    for j = 0,length(source.ropeLocks)-1 do c.ropeLocks:Add(source.ropeLocks[j]) end
    for j = 0,length(source.carpetLocks)-1 do c.carpetLocks:Add(source.carpetLocks[j]) end
    c.carpetOrder = source.carpetOrder
    return c
end
function Rules:carrier_capacity(c) return math.max(1,c.capacityGroups)*self.group_size end
function Rules:occupied_belt_slots()
    return self.occupied_slots
end

function Rules:outgoing_plan(c)
    local plan = self.outgoing_plans[c.index]
    if plan then return plan end
    local pieces, cargo, size = self.d.pieces, c.cargo, self.group_size
    local available = c.generator and size or math.huge
    plan = {count=0}; self.outgoing_plans[c.index] = plan
    local finish = cargo.Count
    if finish == 0 then return plan end
    local color = pieces[cargo[finish-1]].color
    while finish > 0 do
        local first = floor_group(finish-1, size)
        local outer = pieces[cargo[finish-1]]
        if outer.hidden or outer.color ~= color or (plan.count > 0 and outer.sticky) then break end
        local chain_first = first
        while chain_first > 0 and not c.generator do
            local member = pieces[cargo[chain_first]]
            if member.bondedGroup < 0 or math.floor(cargo[chain_first-1] / size) ~= member.bondedGroup then break end
            chain_first = floor_group(chain_first-1, size)
        end
        local amount = finish - chain_first
        if amount > available - plan.count then break end
        local groups = {}
        for slot = chain_first, finish-1 do
            local id = cargo[slot]
            local p = pieces[id]
            if (slot >= first and (p.hidden or p.color ~= color)) or
                (p.location ~= Location.Cargo and p.location ~= Location.Receiving) then return plan end
            groups[math.floor(id / size)] = true
        end
        plan[#plan+1] = {amount=amount, groups=groups}
        plan.count, color, finish = plan.count + amount, pieces[cargo[chain_first]].color, chain_first
    end
    return plan
end

function Rules:releasable_count(index)
    local d = self.d
    if d.status ~= Status.Playing or index < 0 or index >= d.carriers.Count then return 0 end
    local c = d.carriers[index]
    if not c.CanEmit or c.packed or c.releasing > 0 or c.cargo.Count == 0 then return 0 end
    local slots = math.floor(d.beltCapacity/self.group_size)-self:occupied_belt_slots()
    local selected, count = {}, 0
    -- Reuse the cargo scan when only the belt budget has changed. A partially
    -- received original block already owns its slot, including in a sticky chain.
    for _,range in ipairs(self:outgoing_plan(c)) do
        local needed = 0
        for group in pairs(range.groups) do
            if not self.belt_groups[group] and not selected[group] then
                selected[group], needed = true, needed+1
            end
        end
        if needed > slots then break end
        slots, count = slots-needed, count+range.amount
    end
    return count
end

function Rules:adjust_belt_group(group, delta)
    local before = self.belt_groups[group] or 0
    local after = before+delta
    if before == 0 and after > 0 then self.occupied_slots = self.occupied_slots+1 end
    if before > 0 and after == 0 then self.occupied_slots = self.occupied_slots-1 end
    self.belt_groups[group] = after > 0 and after or nil
end

function Rules:adjust_color(color, delta, id)
    self.d.beltCount = self.d.beltCount + delta
    local previous = self.colors[color] or 0
    local count = previous + delta
    if previous == 0 or count == 0 then clear(self.preferences) end
    self.colors[color] = count ~= 0 and count or nil
    self:adjust_belt_group(math.floor(id/self.group_size), delta)
    self.d.beltOccupiedSlots = self.occupied_slots
end

function Rules:release(index)
    local d = self.d
    if d.status ~= Status.Playing then return reject("本局游戏已结束") end
    if index < 0 or index >= d.carriers.Count then return reject("无法选择这辆货车") end
    local c = d.carriers[index]
    if not c.curtainOpen then return reject("完成同色货车后即可打开幕布") end
    if c.Frozen then return reject("完成指定的关联货车后即可破壳") end
    if c.lockColors.Count > 0 then return reject("让同色钥匙露出，即可解锁这组货车") end
    if c.barrier then return reject("障碍货车只能接收，不能放出方块") end
    if c.ropeLocks.Count > 0 then return reject("完成绳索组内任意一辆货车后即可解开绳索") end
    if c.carpetLocks.Count > 0 then return reject("完成一辆货车，即可向前展开地毯") end
    if c.packed then return reject("这辆货车已完成收集") end
    if c.releasing > 0 then return reject("请等待当前方块完全移出货车") end
    if c.cargo.Count == 0 then return reject("货车中没有可以释放的方块") end
    local count = self:releasable_count(index)
    if count == 0 then return reject("传送带剩余空间不足") end
    local first, interrupted, groups = c.cargo.Count-count, Core.RuleArrays.IntList(), {}
    local ids = Core.RuleArrays.Ints(count)
    local previous_loops, released_colors = {}, {}
    for i = 0, count-1 do
        local id = c.cargo[c.cargo.Count-1-i]; ids[i] = id
        local group = math.floor(id/self.group_size)
        groups[group] = (groups[group] or 0)+1
        local p = d.pieces[id]
        local color = p.color
        released_colors[color] = (released_colors[color] or 0)+1
        if p.location == Location.Receiving then interrupted:Add(id) end
        previous_loops[i] = p.loopCarrier == index and p.loopCount or 0
    end
    -- TransferBlocks.MoveNext 0x23C8FD0..0x23C9330 preserves counts only
    -- for this release, clears the BlockLoop stash, then writes source/count+1.
    -- These are repeated releases from one truck, not laps around the belt.
    for _,id in ipairs(self.loop_ids) do
        local p = d.pieces[id]; p.loopCarrier, p.loopCount = -1,0
    end
    clear(self.loop_ids)
    d.pendingTransfers = d.pendingTransfers + count - interrupted.Count
    self.receiving[index] = (self.receiving[index] or 0)-interrupted.Count
    c.cargo:RemoveRange(first, count); c.releasing = count; c.lastReleasedColor = d.pieces[ids[0]].color
    self.cargo_summaries[index] = nil
    self.outgoing_plans[index] = nil
    for i = 0, count-1 do
        local id = ids[i]
        local p = d.pieces[id]
        p.transferVersion = p.transferVersion+1; p.hidden, p.sticky, p.bondedGroup = false, false, -1
        p.loopCarrier, p.loopCount = index, previous_loops[i]+1
        self.loop_ids[i+1] = id
        p.location = Location.Releasing
    end
    for color,amount in pairs(released_colors) do self.colors[color] = (self.colors[color] or 0)+amount end
    d.beltCount = d.beltCount+count
    for group,amount in pairs(groups) do
        self:adjust_belt_group(group, amount)
        local start, still_here = group*self.group_size, false
        for id = start, start+self.group_size-1 do if c.cargo:Contains(id) then still_here = true; break end end
        if not still_here then
            for id = start, start+self.group_size-1 do
                local p = d.pieces[id]
                if p.receiveTarget >= 0 then d.carriers[p.receiveTarget].incoming:Remove(id) end
                p.receiveTarget = -1
            end
        else
            for i = 0, count-1 do
                local id = ids[i]
                if math.floor(id/self.group_size) == group and d.pieces[id].receiveTarget == index and not c.incoming:Contains(id) then c.incoming:Add(id) end
            end
        end
    end
    d.beltOccupiedSlots = self.occupied_slots
    clear(self.preferences); self.reveal_dirty[index] = true
    self:reveal_accessible_groups()
    local result = Core.ReleaseResult(); result.accepted, result.pieces, result.interruptedReceives = true, ids, interrupted:ToArray()
    return result
end

function Rules:enter_belt(id, epoch)
    local d = self.d
    if epoch ~= d.epoch or id < 0 or id >= d.pieces.Count then return false end
    local p = d.pieces[id]; if p.location ~= Location.Releasing then return false end
    local source = d.carriers[p.carrier]; source.releasing = source.releasing-1
    d.pendingTransfers = d.pendingTransfers-1; p.carrier, p.hidden, p.location = -1, false, Location.Belt
    self.belt_ids[id] = true
    if source.releasing == 0 then clear(self.preferences); self:reveal_accessible_groups() end
    return true
end

function Rules:can_receive_locally(index, color)
    local d = self.d
    if index < 0 or index >= d.carriers.Count then return false end
    local c = d.carriers[index]
    -- TunnelCarrier.ProcessFeature overrides GetMaxBlockCount with one group;
    -- GetCompleteBlockCount deliberately remains the normal complete capacity.
    local capacity = c.generator and self.group_size or self:carrier_capacity(c)
    if not c.Accessible or c.packed or c.releasing > 0 or c.cargo.Count+c.incoming.Count+self.group_size > capacity then return false end
    if c.tagColor > 0 and c.tagColor ~= color then return false end
    if c.targetColor > 0 and c.targetColor ~= color then return false end
    if c.cargo.Count > 0 then return d.pieces[c.cargo[c.cargo.Count-1]].color == color end
    if c.incoming.Count > 0 then return d.pieces[c.incoming[0]].color == color end
    if color ~= c.lastReleasedColor or c.tagColor > 0 or c.targetColor > 0 or c.barrier or c.ropeLocks.Count > 0 then return true end
    for key, count in pairs(self.colors) do if key ~= color and count > 0 then return false end end
    return true
end

function Rules:native_priority(c, color)
    if c.tagColor == color or c.targetColor == color then return 2 end
    if c.barrier and c.cargo.Count > 0 then
        for i = 0, c.cargo.Count-1 do if self.d.pieces[c.cargo[i]].color ~= color then return 0 end end
        return 2
    end
    return 0
end
function Rules:preferred_receiver(color)
    local preferred = self.preferences[color]
    if preferred == nil then
        preferred = -1
        local best_priority, best_load = -1, -1
        for i = 0, self.d.carriers.Count-1 do
            if self:can_receive_locally(i, color) then
                local c = self.d.carriers[i]
                local priority = self:native_priority(c, color)
                local load = c.cargo.Count+c.incoming.Count
                if load == 0 and c.lastReleasedColor == color then load = -1 end
                -- Keep each colour collecting in one available cart. Incoming
                -- whole-group reservations count as occupied capacity, so a new
                -- empty cart cannot siphon off the next group mid-transfer.
                -- Mechanic-specific priority still outranks ordinary carts;
                -- equal loads use stable creation order, not fragment proximity.
                if priority > best_priority or priority == best_priority and load > best_load then
                    preferred, best_priority, best_load = i, priority, load
                end
            end
        end
        self.preferences[color] = preferred
    end
    return preferred
end
function Rules:can_receive(index, color)
    return self:can_receive_locally(index, color) and index == self:preferred_receiver(color)
end
function Rules:receive_priority(index, color)
    if not self:can_receive(index, color) then return -1 end
    return self:native_priority(self.d.carriers[index], color)
end
function Rules:can_receive_piece(index, id)
    local d = self.d
    if id < 0 or id >= d.pieces.Count or index < 0 or index >= d.carriers.Count then return false end
    local p = d.pieces[id]; if p.location ~= Location.Belt then return false end
    if p.receiveTarget < 0 then return self:can_receive(index, p.color) end
    local c = d.carriers[index]
    return p.receiveTarget == index and c.Accessible and not c.packed and c.releasing == 0
end
function Rules:receive_priority_for_piece(index, id)
    if not self:can_receive_piece(index, id) then return -1 end
    return self:native_priority(self.d.carriers[index], self.d.pieces[id].color)
end
function Rules:reserve_receive(id, index)
    local d = self.d
    if d.status ~= Status.Playing or not self:can_receive_piece(index, id) then return false end
    local p, c = d.pieces[id], d.carriers[index]
    if p.receiveTarget < 0 then
        local first = floor_group(id, self.group_size)
        for member = first, first+self.group_size-1 do
            local m = d.pieces[member]
            if m.receiveTarget >= 0 or (m.location ~= Location.Belt and m.location ~= Location.Releasing) then return false end
        end
        -- Ownership and capacity only: never move or teleport other fragments.
        for member = first, first+self.group_size-1 do d.pieces[member].receiveTarget = index; c.incoming:Add(member) end
        clear(self.preferences)
    end
    c.incoming:Remove(id); c.cargo:Add(id)
    self.cargo_summaries[index] = nil
    self.outgoing_plans[index] = nil
    self.receiving[index] = (self.receiving[index] or 0)+1
    self.reveal_dirty[index] = true
    self.belt_ids[id] = nil
    p.carrier, p.transferVersion, p.location = index, p.transferVersion+1, Location.Receiving
    d.pendingTransfers = d.pendingTransfers+1; self:adjust_color(p.color, -1,p.id); return true
end

function Rules:finish_receive(id, epoch)
    local d = self.d
    if epoch ~= d.epoch or id < 0 or id >= d.pieces.Count or d.pieces[id].location ~= Location.Receiving then return false end
    local p = d.pieces[id]; p.location = Location.Cargo; d.pendingTransfers = d.pendingTransfers-1
    local index = p.carrier
    local receiving = self.receiving[index]-1
    self.receiving[index], self.reveal_dirty[index] = receiving, true
    local c, complete = d.carriers[index], true
    if receiving == 0 and c.cargo.Count == self:carrier_capacity(c) then
        for i = 0, c.cargo.Count-1 do
            if d.pieces[c.cargo[i]].color ~= p.color then complete = false; break end
        end
        if complete then
            c.packed = true; d.packedCount = d.packedCount+1
            for i = 0, c.cargo.Count-1 do d.pieces[c.cargo[i]].location = Location.Packed end
            self:complete_features(c, p.color)
        end
    end
    if p.keyColor > 0 then self:reveal_accessible_groups() end
    self:evaluate(); return true
end
function Rules:evaluate()
    local d = self.d
    if d.status ~= Status.Playing or d.pendingTransfers > 0 then return end
    local packed = 0
    for index = 0,d.carriers.Count-1 do
        local c = d.carriers[index]; if c.packed then packed = packed+c.cargo.Count end
    end
    if packed == d.pieces.Count then d.status = Status.Won; return end
    if self:occupied_belt_slots()*self.group_size < d.beltCapacity then return end
    if native_policy.fail_on_full then
        d.status, d.failReason = Status.Failed, Failure.ConveyorFull; return
    end
    for id in pairs(self.belt_ids) do
        local p = d.pieces[id]
        local target, color = p.receiveTarget, p.color
        -- The reservation or the cached colour preference is the only truck
        -- can_receive_piece can accept. Check that truck directly when full.
        if target < 0 then target = self:preferred_receiver(color) end
        if target >= 0 and self:can_receive_piece(target, id) then
            if p.loopCarrier ~= target or p.loopCount < native_policy.loop_fail_threshold or
                self:native_priority(d.carriers[target], color) > 0 then return end
        end
    end
    d.status, d.failReason = Status.Failed, Failure.ConveyorFull
end
-- LoseSystem.CanCompleteCarrier/GetMinCompleteBlockCount (0x248CF40/0x248D7CC):
-- a colour must still have enough available blocks to complete the smallest enabled truck.
function Rules:can_complete_carrier()
    local d, minimum, colors, remaining = self.d, math.huge, {}, 0
    if d.pendingTransfers > 0 then return true end
    for index = 0, d.carriers.Count-1 do
        local c = d.carriers[index]
        if not c.packed then
            remaining = remaining+c.cargo.Count+c.releasing
            if c.Accessible then minimum = math.min(minimum,self:carrier_capacity(c)) end
        end
    end
    if minimum == math.huge then minimum = self.capacity end
    -- adjust_color already maintains every belt/releasing fragment. With no
    -- pending transfer these are the exact belt totals; avoid reading thousands
    -- of PieceState objects across the Lua bridge on each 0.2 second check.
    for color,count in pairs(self.colors) do
        if count >= minimum then return true end
        colors[color] = count
    end
    for index = 0,d.carriers.Count-1 do
        local c = d.carriers[index]
        if not c.packed and c.Accessible then
            -- Settled cargo does not change between 0.2-second deadlock checks.
            -- Cache exact fragment counts, including partial groups and keys,
            -- instead of repeatedly crossing the C# bridge for every fragment.
            local summary = self.cargo_summaries[index]
            if not summary or summary.count ~= c.cargo.Count then
                summary = {count=c.cargo.Count,colors={},key=false}
                for slot = 0,c.cargo.Count-1 do
                    local p = d.pieces[c.cargo[slot]]
                    summary.key = summary.key or p.keyColor > 0
                    summary.colors[p.color] = (summary.colors[p.color] or 0)+1
                end
                self.cargo_summaries[index] = summary
            end
            if summary.key then return true end
            for color,count in pairs(summary.colors) do
                colors[color] = (colors[color] or 0)+count
                if colors[color] >= minimum then return true end
            end
        end
    end
    return remaining == 0 and next(colors) == nil
end
function Rules:tick(delta, hold)
    local d = self.d
    if d.status ~= Status.Playing or delta <= 0 then return end
    d.elapsedTime = d.elapsedTime+delta
    if self.time_limit > 0 and d.elapsedTime >= self.time_limit then
        d.status, d.failReason = Status.Failed, Failure.TimeUp; return
    end
    -- ConveyorSpeedSystem.OnUpdate (0x2462FAC), recovered .2 / 1.8 literals.
    local active = 0
    for index = 0, d.carriers.Count-1 do
        local c = d.carriers[index]
        if not c.packed and (c.cargo.Count > 0 or c.releasing > 0 or c.incoming.Count > 0) then active = active+1 end
    end
    local speed = 1
    if native_policy.slower_when_empty and d.beltCount == 0 then speed = native_policy.empty_speed_scale
    elseif native_policy.hold_to_speed then speed = hold and native_policy.hold_speed_multiplier or 1
    elseif (self.level_id < 2 and d.beltCount > 0) or active == 1 then speed = native_policy.fast_speed_scale
    elseif native_policy.faster_when_full and self:occupied_belt_slots()*self.group_size >= d.beltCapacity then speed = native_policy.fast_speed_scale end
    d.conveyorSpeedScale = speed
    self.check_elapsed = self.check_elapsed+delta
    if self.check_elapsed < .2 then return end
    local elapsed = self.check_elapsed; self.check_elapsed = 0
    if d.pendingTransfers > 0 then d.cannotCompleteTime = 0; return end
    self:evaluate()
    if d.status ~= Status.Playing then return end
    if self:can_complete_carrier() then d.cannotCompleteTime = 0; return end
    d.cannotCompleteTime = d.cannotCompleteTime+elapsed
    if d.cannotCompleteTime < 3 then return end
    if native_policy.fail_instead_of_breaking_ice then
        d.status, d.failReason = Status.Failed, Failure.CantCompleteCarrier
    else
        local broken = false
        for index = 0, d.carriers.Count-1 do
            local c = d.carriers[index]
            if c.Frozen then c.iceBroken, broken = true, true; self.reveal_dirty[index] = true end
        end
        if broken then
            clear(self.preferences); self:reveal_accessible_groups(); d.featureRevision = d.featureRevision+1
            d.cannotCompleteTime = 0
        end
    end
end
function Rules:expand_belt()
    self.d.beltCapacity = self.d.beltCapacity+self.group_size
    if self.d.status == Status.Failed then self.d.status, self.d.failReason = Status.Playing, Failure.None end
    self.d.cannotCompleteTime = 0
end
function Rules:revive()
    if self.d.status~=Status.Failed then return false end
    local timed_out=self.d.failReason==Failure.TimeUp
    self:expand_belt()
    if timed_out then self.d.elapsedTime=0 end
    return true
end

local function cargo_units(pieces, c, group_size)
    local units = {}
    for first = 0, c.cargo.Count-1, group_size do
        local front = pieces[c.cargo[first]]
        local last = units[#units]
        local previous = first > 0 and pieces[c.cargo[first-1]] or nil
        local unit
        if last and ((front.bondedGroup >= 0 and math.floor(last[#last]/group_size) == front.bondedGroup) or
            (front.bondedGroup < 0 and previous and previous.bondedGroup < 0 and previous.color == front.color)) then unit = last
        else unit = {}; units[#units+1] = unit end
        for slot = first, math.min(first+group_size-1, c.cargo.Count-1) do
            local id = c.cargo[slot]
            if pieces[id].location == Location.Receiving then return nil end
            unit[#unit+1] = id
        end
    end
    return units
end
local function shuffle_units(d, index, group_size)
    if d.status ~= Status.Playing or index < 0 or index >= d.carriers.Count then return nil end
    local c = d.carriers[index]
    if not c.CanEmit or c.packed or c.releasing > 0 or c.incoming.Count > 0 or c.cargo.Count <= group_size then return nil end
    local units = cargo_units(d.pieces,c,group_size)
    if not units or #units < 2 then return nil end
    return units
end
-- Acceptance only checks eligibility. It must not reorder cargo or reveal its
-- colours before the booster icon has reached the selected carrier.
function Rules.can_shuffle_data(data, index)
    return shuffle_units(data,index,data.fragments.Count)~=nil
end
function Rules:can_shuffle(index) return Rules.can_shuffle_data(self.d,index) end
function Rules:shuffle(index, random)
    local d = self.d
    local units = shuffle_units(d,index,self.group_size)
    if not units then return false end
    local c = d.carriers[index]
    -- Extensions.ShuffleDeranged (0x25A48A4): shuffle, reject fixed points,
    -- retry at most 100 times, then rotate the last candidate once.
    local original = {}; for i,unit in ipairs(units) do original[i] = unit end
    for attempt = 1,100 do
        for i = #units,2,-1 do local j = random:Next(1,i+1); units[i],units[j] = units[j],units[i] end
        local deranged = true
        for i,unit in ipairs(units) do if unit == original[i] then deranged = false; break end end
        if deranged then break end
        if attempt == 100 then local first = table.remove(units,1); units[#units+1] = first end
    end
    c.cargo:Clear()
    for _,unit in ipairs(units) do for _,id in ipairs(unit) do c.cargo:Add(id) end end
    self.outgoing_plans[index] = nil
    clear(self.preferences); self.reveal_dirty[index] = true
    self:reveal_accessible_groups(); d.featureRevision = d.featureRevision+1; return true
end

-- Preserve capacity and added trucks in release snapshots without creating an
-- undo entry. A successful shuffle clears history in its presentation adapter.
function Rules.rebase_booster_snapshot(snapshot, data)
    snapshot.beltCapacity = math.max(snapshot.beltCapacity,data.beltCapacity)
    if snapshot.carriers.Length < data.carriers.Count then
        local carriers = Core.RuleArrays.Carriers(data.carriers.Count)
        for i = 0, snapshot.carriers.Length-1 do carriers[i] = snapshot.carriers[i] end
        for i = snapshot.carriers.Length, data.carriers.Count-1 do carriers[i] = data.carriers[i]:Copy() end
        snapshot.carriers = carriers
    end
end

function Rules:capture()
    local d, snapshot = self.d, Core.StateSnapshot()
    snapshot.pieces, snapshot.carriers = Core.RuleArrays.CopyPieces(d.pieces), Core.RuleArrays.CopyCarriers(d.carriers)
    snapshot.beltCapacity, snapshot.status = d.beltCapacity, d.status
    snapshot.elapsedTime, snapshot.cannotCompleteTime, snapshot.failReason = d.elapsedTime, d.cannotCompleteTime, d.failReason
    return snapshot
end
function Rules:restore(snapshot)
    local d = self.d
    assert(snapshot and snapshot.pieces.Length == d.pieces.Count, "Snapshot does not belong to this level")
    Core.RuleArrays.RestorePieces(d.pieces,snapshot.pieces)
    Core.RuleArrays.RestoreCarriers(d.carriers,snapshot.carriers)
    d.keyCollections:Clear()
    d.beltCapacity, d.status, d.epoch = snapshot.beltCapacity, snapshot.status, d.epoch+1
    d.elapsedTime, d.cannotCompleteTime, d.failReason = snapshot.elapsedTime, snapshot.cannotCompleteTime, snapshot.failReason
    self.check_elapsed = 0
    d.featureRevision, d.beltCount, d.pendingTransfers, d.packedCount = d.featureRevision+1, 0, 0, 0
    clear(self.colors); clear(self.preferences); clear(self.cargo_summaries); clear(self.belt_groups); self.occupied_slots, d.beltOccupiedSlots = 0,0
    clear(self.belt_ids); clear(self.loop_ids); clear(self.receiving); clear(self.reveal_dirty); clear(self.outgoing_plans)
    for id = 0, d.pieces.Count-1 do
        local p = d.pieces[id]
        if p.location == Location.Belt then self.belt_ids[id] = true end
        if p.location == Location.Receiving then self.receiving[p.carrier] = (self.receiving[p.carrier] or 0)+1 end
        if p.loopCarrier >= 0 or p.loopCount ~= 0 then self.loop_ids[#self.loop_ids+1] = id end
        if p.location == Location.Belt or p.location == Location.Releasing then self:adjust_color(p.color, 1,p.id) end
        if p.location == Location.Releasing or p.location == Location.Receiving then d.pendingTransfers = d.pendingTransfers+1 end
    end
    for i = 0, d.carriers.Count-1 do
        self.reveal_dirty[i] = true
        if d.carriers[i].packed then d.packedCount = d.packedCount+1 end
    end
end
function Rules:assert_conservation()
    local d, seen, identities, total, expected = self.d, {}, {}, 0, 0
    for i = 0, d.pieces.Count-1 do
        local p = d.pieces[i]; assert(not identities[p.id], "Duplicate cube identity"); identities[p.id] = true
        if p.location == Location.Cargo or p.location == Location.Receiving or p.location == Location.Packed then expected = expected+1 end
    end
    for c = 0, d.carriers.Count-1 do
        local carrier = d.carriers[c]
        for slot = 0, carrier.cargo.Count-1 do
            local id = carrier.cargo[slot]; local p = d.pieces[id]
            assert(not seen[id] and p.carrier == carrier.index and p.location ~= Location.Belt and p.location ~= Location.Releasing, "Cargo reservation is inconsistent")
            seen[id], total = true, total+1
        end
    end
    assert(total == expected, "A cargo cube is missing from its truck")
    assert(d.beltCount <= d.beltCapacity, "Conveyor overbooked")
end

function Rules:reveal_outer(c)
    local changed = false
    for slot = floor_group(c.cargo.Count-1, self.group_size), c.cargo.Count-1 do
        local p = self.d.pieces[c.cargo[slot]]
        if p.hidden then p.hidden, changed = false, true end
    end
    if changed then
        self.outgoing_plans[c.index] = nil
        self.d.featureRevision = self.d.featureRevision+1
    end
end
function Rules:complete_features(completed, color)
    local d, ropes, carpets = self.d, {}, {}
    local features_changed = false
    clear(self.preferences)
    for i = 0, completed.ropeLocks.Count-1 do ropes[completed.ropeLocks[i]] = true end
    for i = 0, d.carriers.Count-1 do
        local c = d.carriers[i]
        for j = 0, c.carpetLocks.Count-1 do
            local group = c.carpetLocks[j]
            if not carpets[group] or c.carpetOrder > carpets[group].carpetOrder then carpets[group] = c end
        end
    end
    for group, c in pairs(carpets) do
        c.carpetLocks:Remove(group)
        self.reveal_dirty[c.index], features_changed = true, true
    end
    for i = 0, d.carriers.Count-1 do
        local c = d.carriers[i]
        local changed = false
        if not c.curtainOpen and c.curtainColor == color then c.curtainOpen, changed = true, true end
        if c.Frozen then
            for j = 0, c.iceRequires.Length-1 do
                if c.iceRequires[j] == completed.id then c.iceBroken, changed = true, true; break end
            end
        end
        for j = c.ropeLocks.Count-1, 0, -1 do
            if ropes[c.ropeLocks[j]] then c.ropeLocks:RemoveAt(j); changed = true end
        end
        if changed then self.reveal_dirty[i], features_changed = true, true end
    end
    self:reveal_accessible_groups()
    if features_changed then d.featureRevision = d.featureRevision+1 end
end

function Rules:collect_key(c, slot, key)
    local d = self.d
    local event = Core.GameState.KeyCollection(); event.carrier, event.slot, event.color = c.index, slot, key
    d.keyCollections:Add(event)
    for _,first in ipairs(self.key_groups[key] or {}) do
        for id = first, first+self.group_size-1 do
            local p = d.pieces[id]
            if p.keyColor == key then p.keyColor = 0 end
        end
    end
    for i = 0, d.carriers.Count-1 do
        local target = d.carriers[i]
        for j = target.lockColors.Count-1, 0, -1 do
            if target.lockColors[j] == key then
                target.lockColors:RemoveAt(j)
                self.reveal_dirty[i] = true
            end
        end
    end
    clear(self.cargo_summaries); clear(self.preferences); d.featureRevision = d.featureRevision+1
end

function Rules:reveal_carrier(c)
    local size, cargo, pieces = self.group_size, c.cargo, self.d.pieces
    if not c.Accessible or c.packed or cargo.Count == 0 then return end
    self:reveal_outer(c)
    -- Most changed trucks have no key. Check group heads before scanning fragments.
    local has_key = false
    for first = 0, cargo.Count-size, size do
        if pieces[cargo[first]].keyColor > 0 then has_key = true; break end
    end
    if not has_key then return end
    local count = self:outgoing_plan(c).count
    local first = cargo.Count-count
    if first % size ~= 0 then return end
    for slot = first, cargo.Count-size, size do
        local key = pieces[cargo[slot]].keyColor
        if key > 0 then
            local settled = true
            for j = slot, slot+size-1 do
                if pieces[cargo[j]].location ~= Location.Cargo then settled = false; break end
            end
            if settled then self:collect_key(c, slot, key) end
        end
    end
end

function Rules:reveal_accessible_groups()
    local dirty, carriers = self.reveal_dirty, self.d.carriers
    while next(dirty) do
        -- Preserve carrier order when multiple keys of the same colour are reachable.
        for i = 0, carriers.Count-1 do
            if dirty[i] then
                dirty[i] = nil
                self:reveal_carrier(carriers[i])
            end
        end
    end
end
return Rules

end
