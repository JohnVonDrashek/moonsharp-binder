Configuration
=============

Configure MoonSharp Binder via `.editorconfig` under the `[*.cs]` section.

Options
-------
| Key | Description | Default | Example |
| --- | ----------- | ------- | ------- |
| `moonsharp_binder.namespace` | Namespace for generated classes. | `GeneratedLua` | `moonsharp_binder.namespace = MyGame.Lua` |
| `moonsharp_binder.lua_directory` | Root directory for Lua files (relative to project). | `Content/scripts` | `moonsharp_binder.lua_directory = Assets/Lua` |

Sample `.editorconfig`
----------------------
```ini
[*.cs]
moonsharp_binder.namespace = MyGame.Lua
moonsharp_binder.lua_directory = Content/scripts
```

Tips
----
- Keep Lua files under a single root; use globbing in `<AdditionalFiles>` for subfolders.
- If you have multiple projects, set per-project namespaces to avoid collisions.

