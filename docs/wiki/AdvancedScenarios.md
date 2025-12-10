Advanced Scenarios
==================

Multiple Lua roots
------------------
- Use globbing in `<AdditionalFiles>` to include subfolders.
- Keep `.editorconfig` `lua_directory` aligned with your root to avoid surprises.

Per-project namespaces
----------------------
- In solutions with multiple projects, set `moonsharp_binder.namespace` per project to avoid name collisions.

Deeply nested data
------------------
- Binder generates nested wrapper classes automatically; prefer tables over flat string keys to keep types.

Type tweaks
-----------
- If inference picks `double` but you need `int`, annotate with `---@type int` or `---@param value int`.

Interop edges
-------------
- For mixed dynamic/typed scenarios, use `RawTable` to access fields not modeled in Lua yet.
- If you change Lua shape frequently, consider keeping a small set of annotated, stable globals for critical paths.

