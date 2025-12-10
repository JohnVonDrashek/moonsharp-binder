Quickstart
==========

Set up MoonSharp Binder in minutes.

Prerequisites
-------------
- .NET Standard 2.0+ compatible project.
- MoonSharp.Interpreter available at runtime.

1) Install the package
----------------------
```bash
dotnet add package MoonSharpBinder
```

2) Add Lua files as AdditionalFiles
-----------------------------------
In your `.csproj`:
```xml
<ItemGroup>
  <AdditionalFiles Include="Content/scripts/*.lua" />
</ItemGroup>
```

3) (Optional) Configure defaults
--------------------------------
Create or update `.editorconfig`:
```ini
[*.cs]
moonsharp_binder.namespace = MyGame.Lua
moonsharp_binder.lua_directory = Content/scripts
```
Defaults: namespace `GeneratedLua`, directory `Content/scripts`.

4) Build to generate bindings
-----------------------------
Run `dotnet build`. Generated files appear under `obj/**/generated/`.

5) Use the generated class
--------------------------
```lua
-- sprite.lua
sprite = { x = 400, y = 300, size = 50 }
function update() sprite.x = sprite.x + 1 end
```

```csharp
using MoonSharp.Interpreter;
using GeneratedLua; // or your configured namespace

var script = new Script();
script.DoString(File.ReadAllText("sprite.lua"));

var lua = new SpriteScript(script);
lua.Update();
lua.Sprite.X = 500;
```

Next steps
----------
- Review [Configuration](Configuration) for more options.
- Check [Lua Authoring Guide](LuaAuthoringGuide) for annotations and conventions.

