Generated Code Reference
========================

Class naming
------------
- `foo.lua` → `FooScript`.
- Namespace comes from `.editorconfig` (`moonsharp_binder.namespace`, default `GeneratedLua`).

Constructor pattern
-------------------
```csharp
public partial class FooScript
{
    private readonly Script _script;
    public FooScript(Script script) { /* stores script, binds globals */ }
}
```

Members produced
----------------
- Global numbers/strings/bools → get/set properties.
- Global tables → nested wrapper classes with typed properties and `RawTable`.
- Global functions → methods with typed parameters/returns (annotations respected).

Nested tables
-------------
Each table produces a nested class so you can traverse deep structures:
```csharp
lua.Player.Stats.Meta.Title = "hero";
```

Raw table access
----------------
Each table wrapper exposes `RawTable` for advanced MoonSharp operations when you need dynamic access.

Partial classes
---------------
Generated classes are `partial`; add extension files to customize behavior without touching generated code.

