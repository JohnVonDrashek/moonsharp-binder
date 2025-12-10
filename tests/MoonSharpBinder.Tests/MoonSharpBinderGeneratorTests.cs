using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using MoonSharpBinder;
using Xunit;

namespace MoonSharpBinder.Tests;

public class MoonSharpBinderGeneratorTests
{
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
}

