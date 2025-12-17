Troubleshooting & FAQ
=====================

Generated files not appearing
-----------------------------
1. Ensure Lua files are added as `<AdditionalFiles>`, not `<Content>` or `<None>`.
2. Rebuild the project; generators run on build.
3. Check build output for informational diagnostics:
   - `MSHB003`: No .lua files found in AdditionalFiles
   - `MSHB004`: Lua files found but not in configured directory
   - `MSHB005`: Lua file has no exportable members (all locals)
   - `MSHB006`: Summary of what was generated
4. Generated files are compiled directly into your assembly - they don't appear as physical files on disk. Use "Go to Definition" (F12) on a generated type to view the source.

Wrong namespace
---------------
- Add `.editorconfig` with the setting in the **global `[*]` section** (not `[*.cs]`):
  ```ini
  [*]
  moonsharp_binder.namespace = YourNamespace
  ```
- Alternatively, use MSBuild properties in your .csproj (see [Configuration](Configuration.md)).
- Rebuild to regenerate.

Type inferred incorrectly
-------------------------
- Add LuaLS annotations (`---@type`, `---@param`, `---@return`) to force types.

Diagnostics reference
---------------------
| Code | Severity | Description |
| ---- | -------- | ----------- |
| `MSHB001` | Warning | Error processing a Lua file (exception during generation) |
| `MSHB002` | Warning | Lua parse error in a file |
| `MSHB003` | Info | No .lua files found in AdditionalFiles |
| `MSHB004` | Warning | Lua files found but none in configured directory |
| `MSHB005` | Info | Lua file has no exportable members |
| `MSHB006` | Info | Summary: N binding classes generated from M files |
| `MSHB999` | Error | Unexpected generator exception |

Frequently asked
----------------
- **Do locals get exposed?** No, only globals.
- **Where should Lua live?** Under the directory set by `moonsharp_binder.lua_directory` (default `Content/scripts`).
- **Can I extend generated classes?** Yes, they are `partial`.
- **Why can't I see the generated .g.cs files?** Source generators compile directly into your assembly. Use "Go to Definition" on a generated type to view the source in your IDE.
- **The generator doesn't seem to run until I use the type.** This is normal IDE behavior for incremental builds. The generator does run on every build, but the IDE may not show output until it's referenced. Check build output for MSHB006 diagnostic to confirm it ran.

