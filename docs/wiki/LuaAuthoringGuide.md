Lua Authoring Guide
===================

What gets bound
---------------
- **Global** variables, tables, and functions become C# properties/methods.
- **Local** variables/functions are ignored.

Naming and shape
----------------
- Lua `snake_case` becomes C# `PascalCase`.
- Tables become nested classes so deep structures stay typed.

Type inference
--------------
- Numbers → `double`
- Strings → `string`
- Booleans → `bool`
- Tables → nested wrapper class
- Functions → methods

Using LuaLS annotations
-----------------------
Prefer annotations for clarity and non-default types:
```lua
---@param damage number
---@param target string
---@return boolean
function apply_damage(damage, target)
    return true
end
```
Supported annotations: `---@param`, `---@return`, `---@type`.
Supported types: `number`, `integer`/`int`, `string`, `boolean`/`bool`, `table`, `function`.

Globals vs locals example
-------------------------
```lua
local helper = 10       -- ignored
local function foo() end -- ignored

counter = 0             -- exposed as Counter
function bar() end      -- exposed as Bar()
```

Authoring tips
--------------
- Keep globals intentional; avoid polluting the global table.
- Prefer explicit annotations for functions with mixed or complex types.
- Structure data as tables rather than stringly-typed blobs to get nested classes.

