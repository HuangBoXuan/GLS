-- Strict, dependency-free JSON decoding for host data and module-local saves.
-- Tables are ordinary Lua tables; arrays are dense and one-based. JSON null
-- is a sentinel so null array entries and object fields never disappear.
local Json = {}
Json.null = setmetatable({}, {
    __tostring = function() return 'null' end,
    __newindex = function() error('JSON null is read-only', 2) end,
    __metatable = 'JSON null',
})
function Json.is_null(value) return value == Json.null end

local short_escapes = {
    [34] = '"', [92] = '\\', [47] = '/', [98] = '\b',
    [102] = '\f', [110] = '\n', [114] = '\r', [116] = '\t',
}

-- Options: max_bytes (default 4 MiB), max_depth (default 64, at most 256).
-- Positions always refer to bytes of the original text, including a BOM.
function Json.decode(text, options)
    local position = 1
    local function fail(message, at)
        error('JSON decode error at byte '..(at or position)..': '..message, 0)
    end
    if type(text) ~= 'string' then fail('expected a string') end
    if options ~= nil and type(options) ~= 'table' then fail('options must be a table') end
    options = options or {}
    local max_bytes = options.max_bytes
    local max_depth = options.max_depth
    if max_bytes == nil then max_bytes = 4*1024*1024 end
    if max_depth == nil then max_depth = 64 end
    local function positive_integer(value)
        return type(value) == 'number' and value > 0 and value < math.huge and value % 1 == 0
    end
    if not positive_integer(max_bytes) then fail('max_bytes must be a positive integer') end
    if not positive_integer(max_depth) or max_depth > 256 then fail('max_depth must be between 1 and 256') end
    local length = #text
    if length > max_bytes then fail('input exceeds max_bytes', max_bytes+1) end
    local valid, invalid = utf8.len(text)
    if not valid then fail('invalid UTF-8 sequence', invalid) end
    if text:sub(1, 3) == '\239\187\191' then position = 4 end

    local function whitespace()
        while true do
            local c = text:byte(position)
            if c ~= 32 and c ~= 9 and c ~= 10 and c ~= 13 then return end
            position = position+1
        end
    end
    local function digit(at)
        local c = text:byte(at)
        return c ~= nil and c >= 48 and c <= 57
    end
    local function hex4(at)
        local value = text:sub(at, at+3)
        if #value ~= 4 or not value:match('^[%da-fA-F]+$') then fail('expected four hexadecimal digits', at) end
        return tonumber(value, 16)
    end
    local function string_value()
        position = position+1 -- opening quote
        local parts = {}
        while true do
            local marker = text:find('["\\%z\1-\31]', position)
            if not marker then fail('unterminated string', length+1) end
            if marker > position then parts[#parts+1] = text:sub(position, marker-1) end
            local c = text:byte(marker)
            if c == 34 then
                position = marker+1
                return table.concat(parts)
            end
            if c < 32 then fail('unescaped control character in string', marker) end
            local escaped = text:byte(marker+1)
            if short_escapes[escaped] then
                parts[#parts+1] = short_escapes[escaped]
                position = marker+2
            elseif escaped == 117 then
                local code = hex4(marker+2)
                position = marker+6
                if code >= 0xD800 and code <= 0xDBFF then
                    if text:sub(position, position+1) ~= '\\u' then fail('missing low surrogate', position) end
                    local low = hex4(position+2)
                    if low < 0xDC00 or low > 0xDFFF then fail('invalid low surrogate', position+2) end
                    code = 0x10000+(code-0xD800)*0x400+low-0xDC00
                    position = position+6
                elseif code >= 0xDC00 and code <= 0xDFFF then
                    fail('unpaired low surrogate', marker)
                end
                parts[#parts+1] = utf8.char(code)
            else
                fail('invalid string escape', marker+1)
            end
        end
    end
    local function number_value()
        local start = position
        if text:byte(position) == 45 then position = position+1 end
        if text:byte(position) == 48 then
            position = position+1
            if digit(position) then fail('leading zero in number') end
        else
            local c = text:byte(position)
            if not c or c < 49 or c > 57 then fail('expected a digit in number') end
            repeat position = position+1 until not digit(position)
        end
        if text:byte(position) == 46 then
            position = position+1
            if not digit(position) then fail('expected a digit after decimal point') end
            repeat position = position+1 until not digit(position)
        end
        local exponent = text:byte(position)
        if exponent == 101 or exponent == 69 then
            position = position+1
            local sign = text:byte(position)
            if sign == 43 or sign == 45 then position = position+1 end
            if not digit(position) then fail('expected exponent digits') end
            repeat position = position+1 until not digit(position)
        end
        local value = tonumber(text:sub(start, position-1))
        if not value or value ~= value or math.abs(value) == math.huge then
            fail('number is outside the finite numeric range', start)
        end
        return value
    end

    local value
    local function array(depth)
        local result = {}
        position = position+1
        whitespace()
        if text:byte(position) == 93 then position = position+1; return result end
        while true do
            result[#result+1] = value(depth)
            whitespace()
            local c = text:byte(position)
            if c == 93 then position = position+1; return result end
            if c ~= 44 then fail('expected a comma or closing bracket') end
            position = position+1
            whitespace()
            if text:byte(position) == 93 then fail('trailing comma in array') end
        end
    end
    local function object(depth)
        local result = {}
        position = position+1
        whitespace()
        if text:byte(position) == 125 then position = position+1; return result end
        while true do
            if text:byte(position) ~= 34 then fail('expected a quoted object key') end
            local key_at = position
            local key = string_value()
            if result[key] ~= nil then fail('duplicate object key', key_at) end
            whitespace()
            if text:byte(position) ~= 58 then fail('expected a colon after object key') end
            position = position+1
            whitespace()
            result[key] = value(depth)
            whitespace()
            local c = text:byte(position)
            if c == 125 then position = position+1; return result end
            if c ~= 44 then fail('expected a comma or closing brace') end
            position = position+1
            whitespace()
            if text:byte(position) == 125 then fail('trailing comma in object') end
        end
    end
    value = function(depth)
        local c = text:byte(position)
        if c == 34 then return string_value() end
        if c == 45 or digit(position) then return number_value() end
        if c == 123 or c == 91 then
            if depth >= max_depth then fail('maximum nesting depth exceeded') end
            if c == 123 then return object(depth+1) end
            return array(depth+1)
        end
        if text:sub(position, position+3) == 'true' then position = position+4; return true end
        if text:sub(position, position+4) == 'false' then position = position+5; return false end
        if text:sub(position, position+3) == 'null' then position = position+4; return Json.null end
        fail('expected a JSON value')
    end
    whitespace()
    local result = value(0)
    whitespace()
    if position <= length then fail('unexpected content after JSON value') end
    return result
end

return Json
