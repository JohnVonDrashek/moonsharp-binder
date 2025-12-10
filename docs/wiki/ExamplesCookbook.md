Examples & Cookbook
===================

Sprite update loop
------------------
```lua
sprite = { x = 0, y = 0 }
function update(dt) sprite.x = sprite.x + dt end
```
```csharp
var lua = new SpriteScript(script);
lua.Update(0.016);
```

Inventory table access
----------------------
```lua
inventory = { slots = { "sword", "potion" } }
```
```csharp
var first = lua.Inventory.Slots[0];
lua.Inventory.Slots[1] = "shield";
```

Annotated function with return
------------------------------
```lua
---@param damage number
---@param target string
---@return boolean
function apply_damage(damage, target)
    return true
end
```
```csharp
bool ok = lua.ApplyDamage(10, "orc");
```

Combining typed and raw access
------------------------------
```csharp
lua.Player.Health = 50;               // typed
lua.Player.RawTable["custom"] = 123;  // dynamic
```

Extending with partial class
----------------------------
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

