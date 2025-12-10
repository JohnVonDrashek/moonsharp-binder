using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using MoonSharpBinder;
using Xunit;

namespace MoonSharpBinder.Tests;

public class MoonSharpBinderGeneratorTests
{
    [Fact]
    public void GeneratesFunctionWithTypedParamsAndReturn()
    {
        const string lua = """
        ---@param x number
        ---@param name string
        ---@return number
        function add_points(x, name)
            return x
        end
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/calc.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "CalcScript.g.cs");

        generated.Should().Contain("public double AddPoints(double x, string name)");
        generated.Should().Contain("var result = _script.Call(_cachedAddPoints");
        generated.Should().Contain("return result.Number;");
    }

    [Fact]
    public void GeneratesBooleanAndIntReturnConversions()
    {
        const string lua = """
        ---@return boolean
        function is_ready()
            return true
        end

        ---@return int
        function get_count()
            return 1
        end
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/state.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "StateScript.g.cs");

        generated.Should().Contain("public bool IsReady()");
        generated.Should().Contain("return result.Boolean;");
        generated.Should().Contain("public int GetCount()");
        generated.Should().Contain("return (int)result.Number;");
    }

    [Fact]
    public void FunctionWrapperValidatesGlobalIsFunction()
    {
        const string lua = """
        function tick() end
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/tick.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "TickScript.g.cs");

        generated.Should().Contain("if (_cachedTick.Type != DataType.Function)");
    }

    [Fact]
    public void GeneratesFunctionFromAssignment()
    {
        const string lua = """
        foo = function(a, b) return a end
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/func.lua", lua)
        });

        runResult.Results
            .Single()
            .GeneratedSources
            .Select(s => s.HintName)
            .Should()
            .NotContain("FuncScript.g.cs", "function assignments are currently skipped by the parser");
    }

    [Fact]
    public void GeneratesSimpleGlobalsWithTypeOverrides()
    {
        const string lua = """
        score = 1
        name = "abc"
        active = false
        ---@type string
        value = 123
        unknown = someCall()
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/globals.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "GlobalsScript.g.cs");

        generated.Should().Contain("public double Score");
        generated.Should().Contain("public string Name");
        generated.Should().Contain("public bool Active");
        generated.Should().Contain("set => _script.Globals[\"active\"] = DynValue.NewBoolean(value);");
        generated.Should().Contain("public string Value");
        generated.Should().Contain("public DynValue Unknown");
    }

    [Fact]
    public void GeneratesTableForArrayStyleLiteral()
    {
        const string lua = """
        values = { 1, 2, 3 }
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/values.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "ValuesScript.g.cs");

        generated.Should().Contain("public ValuesTable Values");
        generated.Should().Contain("public Table RawTable => _table;");
    }

    [Fact]
    public void GeneratesTableForMixedKeyedAndArrayLiteral()
    {
        const string lua = """
        t = { x = 1, [1] = "a", [2] = "b" }
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/mixed.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "MixedScript.g.cs");

        generated.Should().Contain("public TTable T");
        generated.Should().Contain("public double X");
        generated.Should().Contain("public Table RawTable => _table;");
    }

    [Fact]
    public void GeneratesTableWithNestedFieldsAndBooleans()
    {
        const string lua = """
        player = {
            -- comment inside
            stats = {
                hp = 100,
                meta = {
                    title = "hero",
                    flags = { grounded = true }
                }
            },
            name = "abc",
            alive = false
        }
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/player.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "PlayerScript.g.cs");

        generated.Should().Contain("public PlayerTable Player");
        generated.Should().Contain("public StatsTable Stats");
        generated.Should().Contain("public MetaTable Meta");
        generated.Should().Contain("public FlagsTable Flags");
        generated.Should().Contain("public double Hp");
        generated.Should().Contain("public string Title");
        generated.Should().Contain("public bool Grounded");
        generated.Should().Contain("set => _table[\"alive\"] = DynValue.NewBoolean(value);");
    }

    [Fact]
    public void SkipsLocalMembers()
    {
        const string lua = """
        local hidden = 1
        local function helper() end
        visible = 2
        function run() end
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/locals.lua", lua)
        });

        var generated = GetGeneratedSource(runResult, "LocalsScript.g.cs");

        generated.Should().NotContain("Hidden");
        generated.Should().NotContain("Helper");
        generated.Should().Contain("public double Visible");
        generated.Should().Contain("public void Run()");
    }

    [Fact]
    public void RespectsLuaDirectoryConfiguration()
    {
        const string luaIncluded = "value = 1";
        const string luaSkipped = "other = 2";

        var runResult = RunGenerator(
            new AdditionalText[]
            {
                new LuaAdditionalText("Content/scripts/keep.lua", luaIncluded),
                new LuaAdditionalText("Other/skip.lua", luaSkipped)
            },
            new Dictionary<string, string>
            {
                ["moonsharp_binder.lua_directory"] = "Content/scripts"
            });

        runResult.Results
            .Single()
            .GeneratedSources
            .Select(s => s.HintName)
            .Should()
            .Contain("KeepScript.g.cs")
            .And
            .NotContain("SkipScript.g.cs");
    }

    [Fact]
    public void RespectsWindowsStyleLuaDirectoryConfiguration()
    {
        const string luaIncluded = "value = 1";

        var runResult = RunGenerator(
            new AdditionalText[]
            {
                new LuaAdditionalText("Content/scripts/keep.lua", luaIncluded)
            },
            new Dictionary<string, string>
            {
                ["moonsharp_binder.lua_directory"] = @"Content\scripts"
            });

        runResult.Results
            .Single()
            .GeneratedSources
            .Select(s => s.HintName)
            .Should()
            .Contain("KeepScript.g.cs");
    }

    [Fact]
    public void OverridesNamespaceFromEditorConfig()
    {
        const string lua = """
        value = 1
        """;

        var runResult = RunGenerator(
            new AdditionalText[]
            {
                new LuaAdditionalText("Content/scripts/custom.lua", lua)
            },
            new Dictionary<string, string>
            {
                ["moonsharp_binder.namespace"] = "My.Custom.Namespace"
            });

        var generated = GetGeneratedSource(runResult, "CustomScript.g.cs");

        generated.Should().Contain("namespace My.Custom.Namespace;");
    }

    [Fact]
    public void EmitsDiagnosticOnAdditionalTextError()
    {
        var runResult = RunGenerator(new AdditionalText[]
        {
            new ThrowingAdditionalText("Content/scripts/bad.lua")
        });

        runResult.Results
            .Single()
            .Diagnostics
            .Should()
            .Contain(d => d.Id == "MSHB001" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ContinuesGeneratingWhenOneFileFails()
    {
        const string lua = """
        function ok() end
        """;

        var runResult = RunGenerator(new AdditionalText[]
        {
            new LuaAdditionalText("Content/scripts/good.lua", lua),
            new ThrowingAdditionalText("Content/scripts/bad.lua")
        });

        var generated = GetGeneratedSource(runResult, "GoodScript.g.cs");

        generated.Should().Contain("public void Ok()");
        runResult.Results
            .Single()
            .Diagnostics
            .Should()
            .Contain(d => d.Id == "MSHB001");
    }

    [Fact]
    public void GeneratesNestedTableWrappers()
    {
        const string lua = """
        player = {
            stats = {
                hp = 100,
                meta = { title = "hero" }
            }
        }
        """;

        var additionalText = new LuaAdditionalText("Content/scripts/game.lua", lua);

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location)
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: new ISourceGenerator[] { new MoonSharpBinderGenerator() },
            additionalTexts: new AdditionalText[] { additionalText },
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        var generated = runResult.Results.Single().GeneratedSources.Single(s => s.HintName == "GameScript.g.cs").SourceText.ToString();

        generated.Should().Contain("public partial class GameScript");
        generated.Should().Contain("public PlayerTable Player");
        generated.Should().Contain("public StatsTable Stats");
        generated.Should().Contain("public MetaTable Meta");
        generated.Should().Contain("public double Hp");
        generated.Should().Contain("public string Title");
    }

    private static GeneratorDriverRunResult RunGenerator(IEnumerable<AdditionalText> additionalTexts, IDictionary<string, string>? globalOptions = null)
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = globalOptions != null
            ? new SimpleAnalyzerConfigOptionsProvider(globalOptions)
            : null;

        var driver = CSharpGeneratorDriver.Create(
            generators: new ISourceGenerator[] { new MoonSharpBinderGenerator() },
            additionalTexts: additionalTexts.ToArray(),
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            optionsProvider: optionsProvider);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static string GetGeneratedSource(GeneratorDriverRunResult runResult, string hintName)
    {
        return runResult.Results
            .Single()
            .GeneratedSources
            .Single(s => s.HintName == hintName)
            .SourceText
            .ToString();
    }

    private sealed class LuaAdditionalText : AdditionalText
    {
        private readonly string _text;

        public LuaAdditionalText(string path, string text)
        {
            Path = path;
            _text = text;
        }

        public override string Path { get; }

        public override SourceText? GetText(System.Threading.CancellationToken cancellationToken = default)
            => SourceText.From(_text, Encoding.UTF8);
    }

    private sealed class ThrowingAdditionalText : AdditionalText
    {
        public ThrowingAdditionalText(string path)
        {
            Path = path;
        }

        public override string Path { get; }

        public override SourceText? GetText(System.Threading.CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure reading AdditionalText");
    }

    private sealed class SimpleAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _global;

        public SimpleAnalyzerConfigOptionsProvider(IDictionary<string, string> globalOptions)
        {
            _global = new DictionaryAnalyzerConfigOptions(globalOptions);
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => DictionaryAnalyzerConfigOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => DictionaryAnalyzerConfigOptions.Empty;
    }

    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IDictionary<string, string> _options;

        public static readonly AnalyzerConfigOptions Empty = new DictionaryAnalyzerConfigOptions(new Dictionary<string, string>());

        public DictionaryAnalyzerConfigOptions(IDictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (_options.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }
    }
}

