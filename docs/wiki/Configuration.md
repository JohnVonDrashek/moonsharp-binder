Configuration
=============

MoonSharp Binder can be configured via `.editorconfig` or MSBuild properties.

Options
-------
| Key | Description | Default | Example |
| --- | ----------- | ------- | ------- |
| `moonsharp_binder.namespace` | Namespace for generated classes. | `GeneratedLua` | `moonsharp_binder.namespace = MyGame.Lua` |
| `moonsharp_binder.lua_directory` | Root directory for Lua files (relative to project). | `Content/scripts` | `moonsharp_binder.lua_directory = Assets/Lua` |

Configuration via .editorconfig
-------------------------------
**Important:** Use the `[*]` global section, not `[*.cs]`. Source generators read global analyzer options, which require the global section.

```ini
[*]
moonsharp_binder.namespace = MyGame.Lua
moonsharp_binder.lua_directory = Content/Scripts
```

Configuration via MSBuild
-------------------------
Alternatively, configure via your `.csproj` file:

```xml
<PropertyGroup>
  <MoonSharpBinder_Namespace>MyGame.Lua</MoonSharpBinder_Namespace>
  <MoonSharpBinder_LuaDirectory>Content/Scripts</MoonSharpBinder_LuaDirectory>
</PropertyGroup>

<!-- Make properties visible to the analyzer -->
<ItemGroup>
  <CompilerVisibleProperty Include="MoonSharpBinder_Namespace" />
  <CompilerVisibleProperty Include="MoonSharpBinder_LuaDirectory" />
</ItemGroup>
```

Tips
----
- Keep Lua files under a single root; use globbing in `<AdditionalFiles>` for subfolders.
- If you have multiple projects, set per-project namespaces to avoid collisions.
- The generator emits informational diagnostics (MSHB003-MSHB006) to help verify it's working correctly.

