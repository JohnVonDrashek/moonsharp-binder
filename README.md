# MoonSharp Binder

![NuGet](https://img.shields.io/nuget/v/MoonSharpBinder?logo=nuget)
![License](https://img.shields.io/github/license/JohnVonDrashek/moonsharp-binder)
![.NET](https://img.shields.io/badge/.NET-Standard%202.0-blue)

![MoonSharp Binder](src/MoonSharpBinder/icon.png)

A Roslyn source generator that creates strongly-typed C# bindings from Lua scripts for MoonSharp. **Define once in Lua, get full IntelliSense in C#.**

> Part of [csharp-forge](../AGENTS.md) - experimental C# projects and potential NuGet packages.

## The Problem

When using MoonSharp to embed Lua in your C# application, you end up writing tedious, error-prone code like this:

```csharp
// 😫 Manual string-based access
var updateFunc = _luaScript.Globals.Get("update");
if (updateFunc.Type == DataType.Function)
{
    _luaScript.Call(updateFunc);
}

_spritePosition.X = (float)_spriteTable.Get("x").Number;
_spritePosition.Y = (float)_spriteTable.Get("y").Number;
```

## The Solution

With MoonSharpBinder, your Lua file **is the source of truth**. The generator parses it at compile time and creates typed C# bindings:

```lua
-- sprite.lua
sprite = { x = 400, y = 300, size = 50 }

function update()
    sprite.x = sprite.x + 1
end

---@param r number
---@param g number  
---@param b number
function set_color(r, g, b)
    -- ...
end
```

```csharp
// ✨ Auto-generated, full IntelliSense!
var script = new Script();
script.DoString(luaCode);

var lua = new SpriteScript(script);

lua.Update();              // ✓ Method exists
lua.Sprite.X = 500;        // ✓ Property exists
lua.SetColor(1.0, 0.5, 0); // ✓ Parameters typed
```

## Installation

```bash
dotnet add package MoonSharpBinder
```

Or add to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="MoonSharpBinder" Version="1.0.0" />
</ItemGroup>
```

## Usage

### 1. Add Lua Files as AdditionalFiles

In your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Content/scripts/*.lua" />
</ItemGroup>
```

### 2. Configure (Optional)

Create or update `.editorconfig` in your project root:

```ini
[*.cs]
# Namespace for generated binding classes
moonsharp_binder.namespace = MyGame.Lua

# Directory where Lua files are located
moonsharp_binder.lua_directory = Content/scripts
```

Defaults:
- Namespace: `GeneratedLua`
- Directory: `Content/scripts`

### 3. Use Generated Classes

For a file named `sprite.lua`, the generator creates `SpriteScript`:

```csharp
using MyGame.Lua;
using MoonSharp.Interpreter;

// Load and execute the Lua script
var script = new Script();
script.DoString(File.ReadAllText("sprite.lua"));

// Create the typed binding
var lua = new SpriteScript(script);

// Use with full IntelliSense!
lua.Update();
lua.Sprite.X = 100;
lua.Sprite.Y = 200;
var size = lua.Sprite.Size;
```

## Type Inference

The generator automatically infers types from Lua values:

| Lua Value | C# Type |
|-----------|---------|
| `x = 400` | `double` |
| `name = "hello"` | `string` |
| `active = true` | `bool` |
| `data = { ... }` | Nested class |
| `function foo()` | `void Foo()` |

## LuaLS Type Annotations

For explicit typing, use [LuaLS](https://luals.github.io/wiki/annotations/) annotations:

```lua
---@param damage number
---@param target string
---@return boolean
function apply_damage(damage, target)
    -- ...
    return true
end
```

Generates:

```csharp
public bool ApplyDamage(double damage, string target)
{
    // ...
}
```

### Supported Annotations

- `---@param name type` — Parameter type
- `---@return type` — Return type
- `---@type type` — Variable type

### Supported Types

- `number` → `double`
- `integer` / `int` → `int`
- `string` → `string`
- `boolean` / `bool` → `bool`
- `table` → `Table`
- `function` → `DynValue`

## Generated Code Structure

For `game.lua`:

```lua
player = { x = 0, y = 0, health = 100 }
score = 0

function update()
end

function reset_game()
end
```

Generates `GameScript.g.cs`:

```csharp
namespace GeneratedLua;

public partial class GameScript
{
    private readonly Script _script;
    
    public GameScript(Script script) { ... }
    
    // Functions
    public void Update() { ... }
    public void ResetGame() { ... }
    
    // Simple globals
    public double Score { get; set; }
    
    // Table accessor
    public PlayerTable Player { get; }
    
    // Nested table class
    public class PlayerTable
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Health { get; set; }
        public Table RawTable { get; }
    }
}
```

### Nested Tables

Deeply nested tables produce nested wrapper classes so you get typed access all the way down:

```lua
player = {
    stats = {
        hp = 100,
        meta = { title = "hero" }
    }
}
```

Generates accessors like:

```csharp
lua.Player.Stats.Hp = 90;
var title = lua.Player.Stats.Meta.Title;
```

## Naming Conventions

- Lua `snake_case` → C# `PascalCase`
- `update` → `Update()`
- `player_health` → `PlayerHealth`
- `reset_game` → `ResetGame()`

## Local vs Global

Only **global** functions and variables are exposed. Local declarations are ignored:

```lua
local helper = 10       -- NOT exposed
local function foo()    -- NOT exposed
end

counter = 0             -- Exposed as Counter
function bar()          -- Exposed as Bar()
end
```

## Advanced: Raw Table Access

Each table wrapper exposes `RawTable` for advanced MoonSharp operations:

```csharp
var lua = new GameScript(script);

// Use generated properties
lua.Player.Health = 50;

// Or access raw table for dynamic operations
lua.Player.RawTable["custom_field"] = 123;
```

## Partial Classes

Generated classes are `partial`, so you can extend them:

```csharp
// GameScript.Extensions.cs
namespace GeneratedLua;

public partial class GameScript
{
    public void FullReset()
    {
        ResetGame();
        Score = 0;
        Player.Health = 100;
    }
}
```

## Requirements

- .NET Standard 2.0 or later
- MoonSharp.Interpreter

## Troubleshooting

### Generated files not appearing?

1. Ensure Lua files are added as `<AdditionalFiles>` not `<Content>` or `<None>`
2. Rebuild the project (generators run on build)
3. Check the `obj/` folder for `.g.cs` files

If a Lua file fails to parse, the generator emits `MSHB002` warnings with the error details.

### Wrong namespace?

Add `.editorconfig` with `moonsharp_binder.namespace = YourNamespace`

### Type not inferred correctly?

Use LuaLS annotations:
```lua
---@type number
my_value = some_complex_expression()
```

## License

MIT License

