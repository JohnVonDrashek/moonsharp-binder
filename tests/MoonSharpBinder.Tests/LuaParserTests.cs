using System.Linq;
using FluentAssertions;
using MoonSharpBinder;
using Xunit;

namespace MoonSharpBinder.Tests;

public class LuaParserTests
{
    [Fact]
    public void ParsesAnnotatedFunctionAndGlobals()
    {
        const string lua = """
        ---@param damage number
        ---@param target string
        ---@return boolean
        function apply_damage(damage, target)
            return true
        end

        score = 0
        active = true
        """;

        var result = LuaParser.Parse(lua, "combat");

        result.Errors.Should().BeEmpty();
        result.Functions.Should().HaveCount(1);

        var func = result.Functions.Single();
        func.Name.Should().Be("apply_damage");
        func.Parameters.Select(p => p.Name).Should().BeEquivalentTo("damage", "target");
        func.Parameters[0].ExplicitType.Should().Be("number");
        func.Parameters[1].ExplicitType.Should().Be("string");
        func.ReturnType.Should().Be("boolean");

        result.Globals.Should().HaveCount(2);
        result.Globals.Should().ContainSingle(g => g.Name == "score" && g.ValueType == LuaValueType.Number);
        result.Globals.Should().ContainSingle(g => g.Name == "active" && g.ValueType == LuaValueType.Boolean);
    }

    [Fact]
    public void ParsesNestedTablesRecursively()
    {
        const string lua = """
        player = {
            x = 1,
            stats = {
                hp = 100,
                meta = { title = "hero" }
            }
        }
        """;

        var result = LuaParser.Parse(lua, "game");

        var player = result.Globals.Single(g => g.Name == "player");
        player.ValueType.Should().Be(LuaValueType.Table);

        var stats = player.TableFields.Single(f => f.Name == "stats");
        stats.ValueType.Should().Be(LuaValueType.Table);
        stats.NestedFields.Should().ContainSingle(f => f.Name == "hp" && f.ValueType == LuaValueType.Number);

        var meta = stats.NestedFields.Single(f => f.Name == "meta");
        meta.ValueType.Should().Be(LuaValueType.Table);
        meta.NestedFields.Should().ContainSingle(f => f.Name == "title" && f.ValueType == LuaValueType.String);
    }
}

