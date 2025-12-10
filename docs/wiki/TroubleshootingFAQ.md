Troubleshooting & FAQ
=====================

Generated files not appearing
-----------------------------
1. Ensure Lua files are added as `<AdditionalFiles>`, not `<Content>` or `<None>`.
2. Rebuild the project; generators run on build.
3. Check `obj/<tfm>/generated/` for `*.g.cs`.

Wrong namespace
---------------
- Add `.editorconfig` with `moonsharp_binder.namespace = YourNamespace`.
- Rebuild to regenerate.

Type inferred incorrectly
-------------------------
- Add LuaLS annotations (`---@type`, `---@param`, `---@return`) to force types.

Warnings during build
---------------------
- `MSHB002`: emitted when a Lua file fails to parse. Inspect the warning text for the parse error and fix the Lua script.

Frequently asked
----------------
- **Do locals get exposed?** No, only globals.
- **Where should Lua live?** Under the directory set by `moonsharp_binder.lua_directory` (default `Content/scripts`).
- **Can I extend generated classes?** Yes, they are `partial`.

