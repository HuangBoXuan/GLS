local M=require('game.rule_model')
local Rules=require('game.rule_engine')(M)
local checks=0
local function check(value,message) assert(value,message);checks=checks+1 end
local function cart(id,colors)
    local c=M.CarrierState();c.id=id;c.groups=M.Array(#colors)
    for i,color in ipairs(colors) do c.groups[i-1]={color=color,hidden=false,keyColor=0,sticky=false} end
    return c
end
local function setup(source,receiver)
    local d=M.RuleStateData();local r=Rules.new(d)
    local cars=M.Array(3);cars[0]=cart('source',source);cars[1]=cart('cart1',receiver);cars[2]=cart('other',{2})
    r:initialize({id=1,slots=7,IsSupported=true,carriers=cars},false)
    local move=r:release(0);check(move.accepted,'Source releases')
    for i=0,move.pieces.Length-1 do check(r:enter_belt(move.pieces[i],d.epoch),'Source lands') end
    return r,d,move.pieces
end
do
    local r,d,ids=setup({1,1},{1})
    check(r:can_receive(1,1),'Existing matching cart is eligible')
    r:add_carrier_data(cart('cart2',{}))
    check(not r:reserve_receive(ids[r.group_size],3),'Added empty cart cannot steal a same-colour group')
    check(r:reserve_receive(ids[0],1),'First fragment reserves its whole group')
    check(not r:reserve_receive(ids[r.group_size],3),'Incoming reservation keeps collecting into cart1')
    for i=1,ids.Length-1 do check(r:reserve_receive(ids[i],1),'Every fragment collects into cart1') end
    r:assert_conservation()
end
do
    local r,d,ids=setup({1,1},{})
    r:add_carrier_data(cart('cart2',{}))
    check(r:reserve_receive(ids[0],1),'Equal empty carts choose stable creation order')
    check(not r:reserve_receive(ids[r.group_size],3),'Two empty carts cannot split collection')
    check(not r:can_receive(1,2),'Incoming colour rejects different colours')
    r:assert_conservation()
end
do
    local r,d,ids=setup({1,1},{1,1,1})
    r:add_carrier_data(cart('cart2',{}))
    check(r:reserve_receive(ids[0],1),'Last free group belongs to cart1')
    check(r:reserve_receive(ids[r.group_size],3),'Once cart1 is fully reserved, the next group uses cart2')
    for i=1,r.group_size-1 do check(r:reserve_receive(ids[i],1),'Reserved fragments still reach full cart1') end
    for i=0,r.group_size-1 do check(r:finish_receive(ids[i],d.epoch),'Full cart finishes receiving') end
    check(d.carriers[1].packed,'Cart1 completes before cart2 fills')
    r:assert_conservation()
end
do
    local r,d,ids=setup({1,1},{1})
    check(r:can_receive(1,1),'Prime receiver cache before adding a specialised cart')
    local added=cart('special',{});added.tagColor=1;r:add_carrier_data(added)
    check(not r:can_receive(1,1) and r:can_receive(3,1),'Specialised cart retains original higher priority')
    check(not r:can_receive(3,2),'Specialised colour restriction remains')
    check(r:reserve_receive(ids[0],3),'Matching group enters specialised cart')
    r:assert_conservation()
end
do
    local r,d,ids=setup({1,1},{1})
    r:add_carrier_data(cart('cart2',{}));local snapshot=r:capture()
    check(r:reserve_receive(ids[0],1),'Reserve before undo')
    r:restore(snapshot)
    check(r:can_receive(1,1) and not r:can_receive(3,1),'Undo rebuilds collection preference')
    check(r:reserve_receive(ids[0],1),'Receive again after undo')
    r:assert_conservation()
end
do
    local r,d,ids=setup({1,1,1},{1})
    local function uncached_complete()
        if d.pendingTransfers>0 then return true end
        local minimum,remaining,colors=math.huge,0,{}
        for i=0,d.carriers.Count-1 do
            local c=d.carriers[i]
            if not c.packed then
                remaining=remaining+c.cargo.Count+c.releasing
                if c.Accessible then minimum=math.min(minimum,r:carrier_capacity(c)) end
            end
        end
        if minimum==math.huge then minimum=r.capacity end
        for color,count in pairs(r.colors) do if count>=minimum then return true end;colors[color]=count end
        for i=0,d.carriers.Count-1 do
            local c=d.carriers[i]
            if not c.packed and c.Accessible then
                for j=0,c.cargo.Count-1 do
                    local p=d.pieces[c.cargo[j]]
                    if p.keyColor>0 then return true end
                    colors[p.color]=(colors[p.color] or 0)+1
                    if colors[p.color]>=minimum then return true end
                end
            end
        end
        return remaining==0 and next(colors)==nil
    end
    local function verify(label)
        for _=1,3 do check(r:can_complete_carrier()==uncached_complete(),label) end
        r:assert_conservation()
    end
    verify('Cached completion agrees with full scan before receiving')
    local snapshot=r:capture()
    for i=0,ids.Length-1 do r:reserve_receive(ids[i],1);verify('Receiving invalidates cargo summary') end
    for i=0,ids.Length-1 do r:finish_receive(ids[i],d.epoch) end
    verify('Packed carts are excluded from completion totals')
    r:restore(snapshot);verify('Undo restores exact cargo counts')
    check(snapshot.pieces[ids[0]].location==M.PieceLocation.Belt,'Restoring leaves snapshot independent')
    local returning=r:release(1);check(returning.accepted,'Release after cached completion check')
    for i=0,returning.pieces.Length-1 do r:enter_belt(returning.pieces[i],d.epoch) end
    verify('Releasing invalidates cargo summary')
end
do
    local r,d,ids=setup({1},{1,1,1})
    for i=0,ids.Length-1 do check(r:reserve_receive(ids[i],1),'Reserve the last group') end
    check(r:finish_receive(ids[0],d.epoch),'First fragment settles')
    local snapshot=r:capture()
    check(r:finish_receive(ids[1],d.epoch),'Settle another fragment before undo')
    r:restore(snapshot)
    for i=1,ids.Length-2 do check(r:finish_receive(ids[i],d.epoch),'Restore remaining intake') end
    check(not d.carriers[1].packed,'Full cart waits for the last fragment after undo')
    check(r:finish_receive(ids[ids.Length-1],d.epoch) and d.carriers[1].packed,'Last arrival packs the restored cart')
    r:assert_conservation()
end
do
    local d=M.RuleStateData();local r=Rules.new(d)
    local cars=M.Array(2);cars[0]=cart('first',{3,1});cars[1]=cart('second',{3,2})
    r:initialize({id=1,slots=2,IsSupported=true,carriers=cars},false)
    for i=0,1 do
        local move=r:release(i);check(move.accepted,'Fill one belt slot')
        for j=0,move.pieces.Length-1 do r:enter_belt(move.pieces[j],d.epoch) end
    end
    r:evaluate()
    check(d.status==M.RunStatus.Failed and d.failReason==M.FailReason.ConveyorFull,'A full belt without a compatible truck fails')
end
do
    local d=M.RuleStateData();local r=Rules.new(d)
    local cars=M.Array(4)
    cars[0]=cart('last-lock',{3});cars[0].lockColors:Add(8);cars[0].groups[0].hidden=true
    cars[1]=cart('source',{1,2});cars[1].groups[0].keyColor=7;cars[1].groups[0].hidden=true
    cars[2]=cart('middle-lock',{2});cars[2].lockColors:Add(7);cars[2].groups[0].keyColor=8
    cars[3]=cart('duplicate-key',{4});cars[3].lockColors:Add(9);cars[3].groups[0].keyColor=7
    r:initialize({id=1,slots=7,IsSupported=true,carriers=cars},false)
    local snapshot=r:capture()
    for _=1,2 do
        local move=r:release(1)
        check(move.accepted and d.keyCollections.Count==2,'The exposed key opens the chained lock in the same release')
        check(d.keyCollections[0].color==7 and d.keyCollections[1].color==8,'Chained keys retain their collection order')
        check(d.carriers[0].Accessible and not d.pieces[d.carriers[0].cargo[0]].hidden,'A previously visited cart reveals after unlocking')
        check(not d.carriers[3].Accessible and d.pieces[d.carriers[3].cargo[0]].keyColor==0,'Collecting one key clears its duplicate')
        r:restore(snapshot)
        check(not d.carriers[0].Accessible and not d.carriers[2].Accessible,'Undo restores chained locks')
    end
end
return 'Cart receiving and snapshot regression checks passed: '..checks
