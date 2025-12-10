C# Usage Patterns
=================

Loading and running scripts
---------------------------
```csharp
var script = new Script();
script.DoString(File.ReadAllText("game.lua"));
var lua = new GameScript(script); // generated class
```

Calling functions
-----------------
```csharp
lua.Update();
lua.ResetGame();
```

Working with tables
-------------------
```csharp
lua.Player.Health = 90;
var title = lua.Player.Stats.Meta.Title;
```

Mixing typed and dynamic access
-------------------------------
```csharp
lua.Player.RawTable["custom_field"] = 123;
```

Extending via partial classes
-----------------------------
```csharp
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

Tips
----
- Keep a single `Script` instance per generated binding instance.
- Rebuild after Lua changes to refresh generated code.

