// =============================================================================
// MoonSharpBinderGenerator.cs
// =============================================================================
// A Roslyn source generator that creates strongly-typed C# wrapper classes for
// Lua scripts. This enables IntelliSense, compile-time type checking, and
// cleaner code when working with MoonSharp-embedded Lua.
//
// How it works:
// 1. Scans AdditionalFiles for .lua files in the configured directory
// 2. Uses LuaParser to extract functions and globals from each Lua file
// 3. Generates a C# wrapper class for each Lua file with:
//    - Methods for each Lua function (with typed parameters/returns)
//    - Properties for each Lua global variable
//    - Nested classes for Lua table access
//
// Configuration (via .editorconfig or MSBuild properties):
// - moonsharp_binder.namespace: Output namespace (default: "GeneratedLua")
// - moonsharp_binder.lua_directory: Path to Lua files (default: "Content/scripts")
//
// Generated files follow the naming convention: {PascalCaseLuaFileName}Script.g.cs
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MoonSharpBinder;

/// <summary>
/// Roslyn source generator that creates strongly-typed C# binding classes from Lua source files.
/// </summary>
/// <remarks>
/// <para>
/// This generator implements <see cref="ISourceGenerator"/> to hook into the C# compilation
/// pipeline. It processes Lua files added as <c>&lt;AdditionalFiles&gt;</c> in the project
/// and generates corresponding C# wrapper classes.
/// </para>
/// 
/// <para><strong>Project Setup Requirements:</strong></para>
/// <list type="number">
///   <item>Add MoonSharpBinder as a package reference</item>
///   <item>Include Lua files as AdditionalFiles in .csproj:
///     <code>&lt;AdditionalFiles Include="Content/scripts/*.lua" /&gt;</code>
///   </item>
///   <item>Optionally configure namespace and directory in .editorconfig</item>
/// </list>
/// 
/// <para><strong>Generated Class Features:</strong></para>
/// <list type="bullet">
///   <item>Constructor accepting a MoonSharp <c>Script</c> instance</item>
///   <item>Methods wrapping each Lua function with proper parameter/return types</item>
///   <item>Properties for simple global variables (numbers, strings, booleans)</item>
///   <item>Nested wrapper classes for Lua table globals</item>
///   <item>Caching of <c>DynValue</c> lookups for performance</item>
///   <item>Partial class support for user extensions</item>
/// </list>
/// 
/// <para><strong>Example:</strong></para>
/// For a file <c>game.lua</c> containing:
/// <code>
/// score = 0
/// player = { x = 100, y = 200 }
/// 
/// function update()
/// end
/// 
/// ---@param amount number
/// function add_score(amount)
///     score = score + amount
/// end
/// </code>
/// 
/// The generator produces <c>GameScript.g.cs</c>:
/// <code>
/// public partial class GameScript
/// {
///     public GameScript(Script script) { ... }
///     
///     public void Update() { ... }
///     public void AddScore(double amount) { ... }
///     
///     public double Score { get; set; }
///     public PlayerTable Player { get; }
///     
///     public class PlayerTable { ... }
/// }
/// </code>
/// </remarks>
[Generator]
public class MoonSharpBinderGenerator : ISourceGenerator
{
    // ==========================================================================
    // Configuration Constants
    // ==========================================================================
    
    /// <summary>
    /// Default namespace for generated binding classes when not configured.
    /// </summary>
    private const string DefaultNamespace = "GeneratedLua";
    
    /// <summary>
    /// Default directory path where Lua files are expected to be found.
    /// Files outside this directory (that are AdditionalFiles) are ignored.
    /// </summary>
    private const string DefaultLuaDirectory = "Content/scripts";
    
    /// <summary>
    /// Configuration key for namespace in .editorconfig format.
    /// Example: <c>moonsharp_binder.namespace = MyGame.Lua</c>
    /// </summary>
    private const string ConfigKeyNamespace = "moonsharp_binder.namespace";
    
    /// <summary>
    /// Configuration key for Lua directory in .editorconfig format.
    /// Example: <c>moonsharp_binder.lua_directory = Scripts</c>
    /// </summary>
    private const string ConfigKeyLuaDirectory = "moonsharp_binder.lua_directory";

    /// <summary>
    /// Called once when the generator is first loaded. Used to register syntax receivers.
    /// </summary>
    /// <param name="context">The initialization context provided by Roslyn.</param>
    /// <remarks>
    /// This generator doesn't need a syntax receiver since it processes AdditionalFiles
    /// rather than C# syntax trees.
    /// </remarks>
    public void Initialize(GeneratorInitializationContext context)
    {
        // No syntax receiver needed - we process AdditionalFiles directly in Execute
    }

    /// <summary>
    /// Main entry point called during compilation to generate source code.
    /// </summary>
    /// <param name="context">The execution context providing access to compilation, files, and diagnostics.</param>
    /// <remarks>
    /// <para>
    /// This method is called once per compilation. It:
    /// </para>
    /// <list type="number">
    ///   <item>Reads configuration values (namespace, directory)</item>
    ///   <item>Filters AdditionalFiles to find .lua files in the configured directory</item>
    ///   <item>Parses each Lua file using <see cref="LuaParser"/></item>
    ///   <item>Generates a C# binding class for files with exposed functions/globals</item>
    ///   <item>Reports any errors as diagnostics</item>
    /// </list>
    /// 
    /// <para>
    /// Errors during generation are reported as warnings rather than errors to avoid
    /// breaking the build when a Lua file has syntax issues.
    /// </para>
    /// </remarks>
    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            // Read configuration from .editorconfig or MSBuild properties
            var namespaceName = GetConfigurationValue(context, ConfigKeyNamespace, DefaultNamespace);
            var luaDirectory = GetConfigurationValue(context, ConfigKeyLuaDirectory, DefaultLuaDirectory);

            // Find all Lua files in AdditionalFiles that are in the configured directory
            var luaFiles = context.AdditionalFiles
                .Where(f =>
                {
                    // Must be a .lua file
                    if (!f.Path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Normalize path separators for cross-platform comparison
                    var path = f.Path.Replace('\\', '/');
                    var normalizedDir = luaDirectory.Replace('\\', '/');

                    // Check if the file is in the configured directory
                    return path.Contains(normalizedDir, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            // Report informational diagnostic about what we're processing
            var allLuaFiles = context.AdditionalFiles
                .Where(f => f.Path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allLuaFiles.Count == 0)
            {
                // Warning: No Lua files at all - likely a configuration issue
                ReportDiagnostic(context, "MSHB003", "No Lua files found",
                    "MoonSharpBinder: No .lua files found in AdditionalFiles. Add Lua files with <AdditionalFiles Include=\"**/*.lua\" /> in your .csproj.",
                    DiagnosticSeverity.Warning);
            }
            else if (luaFiles.Count == 0)
            {
                // Warning: Lua files exist but not in configured directory
                ReportDiagnostic(context, "MSHB004", "No Lua files in configured directory",
                    $"MoonSharpBinder: Found {allLuaFiles.Count} .lua file(s) in AdditionalFiles, but none match the configured directory '{luaDirectory}'. Check moonsharp_binder.lua_directory setting.",
                    DiagnosticSeverity.Warning);
            }

            var generatedCount = 0;
            var generatedTypes = new List<string>();

            // Process each Lua file
            foreach (var luaFile in luaFiles)
            {
                try
                {
                    var luaContent = luaFile.GetText()?.ToString();
                    if (string.IsNullOrEmpty(luaContent)) continue;

                    // Parse the Lua source to extract functions and globals
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(luaFile.Path);
                    var parseResult = LuaParser.Parse(luaContent!, fileName);

                    // Surface parse errors as diagnostics
                    foreach (var error in parseResult.Errors)
                    {
                        ReportDiagnostic(context, "MSHB002", "Lua parse warning",
                            $"Error parsing {luaFile.Path}: {error}",
                            DiagnosticSeverity.Warning);
                    }

                    // Only generate a binding class if there are exposed items
                    if (parseResult.Functions.Any() || parseResult.Globals.Any())
                    {
                        var generatedCode = GenerateBindingClass(parseResult, namespaceName);
                        var sourceText = SourceText.From(generatedCode, Encoding.UTF8);
                        var typeName = $"{ToPascalCase(fileName)}Script";
                        var outputName = $"{typeName}.g.cs";
                        context.AddSource(outputName, sourceText);
                        generatedCount++;
                        generatedTypes.Add(typeName);
                    }
                    else
                    {
                        ReportDiagnostic(context, "MSHB005", "No exportable members",
                            $"MoonSharpBinder: '{fileName}.lua' has no global functions or variables to export. Only non-local members are generated.",
                            DiagnosticSeverity.Warning);
                    }
                }
                catch (Exception ex)
                {
                    // Report file-specific errors as warnings
                    ReportDiagnostic(context, "MSHB001", "Error generating bindings",
                        $"Error processing {luaFile.Path}: {ex.Message}",
                        DiagnosticSeverity.Warning);
                }
            }

            // Summary diagnostic - use Warning so it appears in default build output
            if (generatedCount > 0)
            {
                var typesList = string.Join(", ", generatedTypes);
                ReportDiagnostic(context, "MSHB006", "MoonSharpBinder generation complete",
                    $"MoonSharpBinder: Generated {generatedCount} type(s) in namespace '{namespaceName}': {typesList}",
                    DiagnosticSeverity.Warning);
            }
            else if (luaFiles.Count > 0)
            {
                // We processed files but generated nothing
                ReportDiagnostic(context, "MSHB007", "No types generated",
                    $"MoonSharpBinder: Processed {luaFiles.Count} Lua file(s) but generated no types. Check that files contain global (non-local) functions or variables.",
                    DiagnosticSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            // Report unexpected generator exceptions as errors
            ReportDiagnostic(context, "MSHB999", "Generator exception",
                $"MoonSharpBinder threw an exception: {ex}",
                DiagnosticSeverity.Error);
        }
    }

    // ==========================================================================
    // Code Generation Methods
    // ==========================================================================

    /// <summary>
    /// Generates the complete C# source code for a binding class from parsed Lua data.
    /// </summary>
    /// <param name="parseResult">The parsed Lua file containing functions and globals.</param>
    /// <param name="namespaceName">The namespace to place the generated class in.</param>
    /// <returns>Complete C# source code as a string.</returns>
    /// <remarks>
    /// The generated class includes:
    /// <list type="bullet">
    ///   <item>Auto-generated header comment</item>
    ///   <item>Nullable enable directive</item>
    ///   <item>Required using statements</item>
    ///   <item>Partial class declaration for extensibility</item>
    ///   <item>Script reference and cached DynValue fields</item>
    ///   <item>Constructor accepting Script instance</item>
    ///   <item>Function wrapper methods</item>
    ///   <item>Global property accessors</item>
    ///   <item>Nested table wrapper classes</item>
    /// </list>
    /// </remarks>
    private string GenerateBindingClass(LuaParseResult parseResult, string namespaceName)
    {
        var className = $"{ToPascalCase(parseResult.FileName)}Script";
        var sb = new StringBuilder();

        // File header with auto-generated warning
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"// This file was generated by MoonSharpBinder from {parseResult.FileName}.lua");
        sb.AppendLine("// Do not edit this file manually.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using MoonSharp.Interpreter;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        
        // Class documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Strongly-typed bindings for {parseResult.FileName}.lua");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public partial class {className}");
        sb.AppendLine("{");
        
        // Private field for the MoonSharp Script instance
        sb.AppendLine("    private readonly Script _script;");
        
        // Cache fields for function DynValues (avoids repeated lookups)
        foreach (var func in parseResult.Functions)
        {
            sb.AppendLine($"    private DynValue? _cached{ToPascalCase(func.Name)};");
        }
        
        sb.AppendLine();
        
        // Constructor
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new binding wrapper for the Lua script.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"script\">The MoonSharp Script instance (after DoString/DoFile has been called)</param>");
        sb.AppendLine($"    public {className}(Script script)");
        sb.AppendLine("    {");
        sb.AppendLine("        _script = script ?? throw new ArgumentNullException(nameof(script));");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate wrapper methods for each Lua function
        foreach (var func in parseResult.Functions)
        {
            GenerateFunctionWrapper(sb, func);
        }

        // Generate property accessors for each Lua global
        foreach (var global in parseResult.Globals)
        {
            if (global.ValueType == LuaValueType.Table)
            {
                // Tables get a dedicated accessor property with a nested wrapper class
                GenerateTableAccessor(sb, global, className);
            }
            else
            {
                // Simple values get a straightforward get/set property
                GenerateSimpleGlobalAccessor(sb, global);
            }
        }

        // Generate nested wrapper classes for table globals
        foreach (var global in parseResult.Globals.Where(g => g.ValueType == LuaValueType.Table))
        {
            GenerateTableClass(sb, global, className);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a C# method that wraps a Lua function call.
    /// </summary>
    /// <param name="sb">StringBuilder to append the generated code to.</param>
    /// <param name="func">The Lua function to wrap.</param>
    /// <remarks>
    /// <para>Generated methods include:</para>
    /// <list type="bullet">
    ///   <item>XML documentation comment</item>
    ///   <item>Lazy caching of the function's DynValue lookup</item>
    ///   <item>Runtime type checking (throws if global isn't a function)</item>
    ///   <item>Parameter conversion via DynValue.FromObject</item>
    ///   <item>Return value conversion for typed returns</item>
    /// </list>
    /// 
    /// <para>Example output for <c>function update()</c>:</para>
    /// <code>
    /// public void Update()
    /// {
    ///     _cachedUpdate ??= _script.Globals.Get("update");
    ///     if (_cachedUpdate.Type != DataType.Function)
    ///         throw new InvalidOperationException("...");
    ///     _script.Call(_cachedUpdate);
    /// }
    /// </code>
    /// </remarks>
    private void GenerateFunctionWrapper(StringBuilder sb, LuaFunction func)
    {
        var methodName = ToPascalCase(func.Name);
        var returnType = MapReturnType(func.ReturnType);
        var hasReturn = returnType != "void";

        // XML documentation
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Calls the Lua function '{func.Name}'");
        sb.AppendLine($"    /// </summary>");
        
        // Build the parameter declaration list
        var paramDecls = new List<string>();
        var paramNames = new List<string>();
        foreach (var param in func.Parameters)
        {
            var csType = MapParameterType(param.ExplicitType);
            paramDecls.Add($"{csType} {param.Name}");
            paramNames.Add(param.Name);
        }

        var paramDeclStr = string.Join(", ", paramDecls);
        
        // Method signature
        sb.AppendLine($"    public {returnType} {methodName}({paramDeclStr})");
        sb.AppendLine("    {");
        
        // Lazy cache the function lookup (avoids repeated string lookups)
        sb.AppendLine($"        _cached{methodName} ??= _script.Globals.Get(\"{func.Name}\");");
        
        // Runtime validation that the global is actually a function
        sb.AppendLine($"        if (_cached{methodName}.Type != DataType.Function)");
        sb.AppendLine($"            throw new InvalidOperationException(\"Lua global '{func.Name}' is not a function\");");
        sb.AppendLine();

        // Generate the function call with appropriate parameter/return handling
        if (paramNames.Count > 0)
        {
            // Convert all parameters to DynValues for the call
            var callArgs = string.Join(", ", paramNames.Select(p => $"DynValue.FromObject(_script, {p})"));
            if (hasReturn)
            {
                sb.AppendLine($"        var result = _script.Call(_cached{methodName}, {callArgs});");
                sb.AppendLine($"        return {GenerateReturnConversion(returnType)};");
            }
            else
            {
                sb.AppendLine($"        _script.Call(_cached{methodName}, {callArgs});");
            }
        }
        else
        {
            // No parameters
            if (hasReturn)
            {
                sb.AppendLine($"        var result = _script.Call(_cached{methodName});");
                sb.AppendLine($"        return {GenerateReturnConversion(returnType)};");
            }
            else
            {
                sb.AppendLine($"        _script.Call(_cached{methodName});");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a property accessor for a Lua table global, with lazy instantiation of the wrapper class.
    /// </summary>
    /// <param name="sb">StringBuilder to append the generated code to.</param>
    /// <param name="global">The Lua table global to create an accessor for.</param>
    /// <param name="parentClassName">The name of the parent binding class (unused but kept for consistency).</param>
    /// <remarks>
    /// <para>
    /// Table accessors use lazy initialization to:
    /// </para>
    /// <list type="bullet">
    ///   <item>Avoid unnecessary lookups if the table isn't accessed</item>
    ///   <item>Cache the wrapper instance for repeated access</item>
    ///   <item>Validate that the global is actually a table at runtime</item>
    /// </list>
    /// 
    /// <para>Example output for <c>player = { ... }</c>:</para>
    /// <code>
    /// private PlayerTable? _cachedPlayerTable;
    /// 
    /// public PlayerTable Player
    /// {
    ///     get
    ///     {
    ///         if (_cachedPlayerTable == null)
    ///         {
    ///             var tableValue = _script.Globals.Get("player");
    ///             if (tableValue.Type != DataType.Table)
    ///                 throw new InvalidOperationException("...");
    ///             _cachedPlayerTable = new PlayerTable(tableValue.Table);
    ///         }
    ///         return _cachedPlayerTable;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    private void GenerateTableAccessor(StringBuilder sb, LuaGlobal global, string parentClassName)
    {
        var propertyName = ToPascalCase(global.Name);
        var tableClassName = $"{propertyName}Table";

        // Cache field for the wrapper instance
        sb.AppendLine($"    private {tableClassName}? _cached{propertyName}Table;");
        sb.AppendLine();
        
        // Property with lazy initialization
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Access the '{global.Name}' table from Lua");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {tableClassName} {propertyName}");
        sb.AppendLine("    {");
        sb.AppendLine("        get");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (_cached{propertyName}Table == null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var tableValue = _script.Globals.Get(\"{global.Name}\");");
        sb.AppendLine($"                if (tableValue.Type != DataType.Table)");
        sb.AppendLine($"                    throw new InvalidOperationException(\"Lua global '{global.Name}' is not a table\");");
        sb.AppendLine($"                _cached{propertyName}Table = new {tableClassName}(tableValue.Table);");
        sb.AppendLine("            }");
        sb.AppendLine($"            return _cached{propertyName}Table;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a simple get/set property for a non-table Lua global variable.
    /// </summary>
    /// <param name="sb">StringBuilder to append the generated code to.</param>
    /// <param name="global">The Lua global variable to create an accessor for.</param>
    /// <remarks>
    /// <para>
    /// Simple globals (numbers, strings, booleans) get straightforward property access
    /// that reads from and writes to the Script.Globals table directly.
    /// </para>
    /// 
    /// <para>Example output for <c>score = 0</c>:</para>
    /// <code>
    /// public double Score
    /// {
    ///     get => _script.Globals.Get("score").Number;
    ///     set => _script.Globals["score"] = value;
    /// }
    /// </code>
    /// </remarks>
    private void GenerateSimpleGlobalAccessor(StringBuilder sb, LuaGlobal global)
    {
        var propertyName = ToPascalCase(global.Name);
        var csType = MapValueType(global.ValueType, global.ExplicitType);

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets or sets the '{global.Name}' global from Lua");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {csType} {propertyName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        get => {GenerateGetterConversion(global.ValueType, global.Name)};");
        sb.AppendLine($"        set => _script.Globals[\"{global.Name}\"] = {GenerateSetterConversion(global.ValueType, "value")};");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a nested wrapper class for a Lua table global.
    /// </summary>
    /// <param name="sb">StringBuilder to append the generated code to.</param>
    /// <param name="global">The Lua table global to create a wrapper class for.</param>
    /// <param name="parentClassName">The name of the parent binding class (unused but kept for consistency).</param>
    /// <remarks>
    /// <para>
    /// Table wrapper classes provide typed access to table fields and include:
    /// </para>
    /// <list type="bullet">
    ///   <item>Internal constructor accepting a MoonSharp Table</item>
    ///   <item>Typed get/set properties for each known field</item>
    ///   <item>RawTable property for advanced/dynamic access</item>
    /// </list>
    /// 
    /// <para>Example output for <c>player = { x = 100, y = 200 }</c>:</para>
    /// <code>
    /// public class PlayerTable
    /// {
    ///     private readonly Table _table;
    ///     
    ///     internal PlayerTable(Table table) { ... }
    ///     
    ///     public double X { get; set; }
    ///     public double Y { get; set; }
    ///     
    ///     public Table RawTable => _table;
    /// }
    /// </code>
    /// </remarks>
    private void GenerateTableClass(StringBuilder sb, LuaGlobal global, string parentClassName)
    {
        var tableClassName = $"{ToPascalCase(global.Name)}Table";

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Wrapper class for the '{global.Name}' Lua table");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public class {tableClassName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly Table _table;");
        sb.AppendLine();

        // Cache nested tables for reuse
        foreach (var tableField in global.TableFields.Where(f => f.ValueType == LuaValueType.Table))
        {
            var fieldPropertyName = ToPascalCase(tableField.Name);
            sb.AppendLine($"        private {fieldPropertyName}Table? _cached{fieldPropertyName}Table;");
        }
        if (global.TableFields.Any(f => f.ValueType == LuaValueType.Table))
        {
            sb.AppendLine();
        }
        
        // Internal constructor (only the parent class should instantiate)
        sb.AppendLine($"        internal {tableClassName}(Table table)");
        sb.AppendLine("        {");
        sb.AppendLine("            _table = table ?? throw new ArgumentNullException(nameof(table));");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate property for each known table field
        foreach (var field in global.TableFields)
        {
            var fieldPropertyName = ToPascalCase(field.Name);

            if (field.ValueType == LuaValueType.Table)
            {
                var nestedClassName = $"{fieldPropertyName}Table";
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Access the nested table '{field.Name}'");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public {nestedClassName} {fieldPropertyName}");
                sb.AppendLine("        {");
                sb.AppendLine("            get");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (_cached{fieldPropertyName}Table == null)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var tableValue = _table.Get(\"{field.Name}\");");
                sb.AppendLine($"                    if (tableValue.Type != DataType.Table)");
                sb.AppendLine($"                        throw new InvalidOperationException(\"Lua field '{field.Name}' is not a table\");");
                sb.AppendLine($"                    _cached{fieldPropertyName}Table = new {nestedClassName}(tableValue.Table);");
                sb.AppendLine("                }");
                sb.AppendLine($"                return _cached{fieldPropertyName}Table;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }
            else
            {
                var fieldCsType = MapValueType(field.ValueType, field.ExplicitType);
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Gets or sets the '{field.Name}' field");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public {fieldCsType} {fieldPropertyName}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => {GenerateTableGetterConversion(field.ValueType, field.Name)};");
                sb.AppendLine($"            set => _table[\"{field.Name}\"] = {GenerateSetterConversion(field.ValueType, "value")};");
                sb.AppendLine("        }");
            }

            sb.AppendLine();
        }

        // Expose the underlying Table for advanced usage
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the underlying MoonSharp Table for advanced access");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public Table RawTable => _table;");
        sb.AppendLine();

        // Generate nested wrapper classes for table fields
        foreach (var tableField in global.TableFields.Where(f => f.ValueType == LuaValueType.Table))
        {
            GenerateNestedTableClass(sb, tableField, "        ");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a nested table wrapper class for a table field, including deeper nested fields.
    /// </summary>
    private void GenerateNestedTableClass(StringBuilder sb, LuaTableField field, string indent)
    {
        var className = $"{ToPascalCase(field.Name)}Table";
        var deeperTables = field.NestedFields.Where(f => f.ValueType == LuaValueType.Table).ToList();

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Wrapper for nested table '{field.Name}'");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public class {className}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    private readonly Table _table;");
        sb.AppendLine();

        foreach (var nestedTable in deeperTables)
        {
            var nestedName = ToPascalCase(nestedTable.Name);
            sb.AppendLine($"{indent}    private {nestedName}Table? _cached{nestedName}Table;");
        }
        if (deeperTables.Any())
        {
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}    internal {className}(Table table)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        _table = table ?? throw new ArgumentNullException(nameof(table));");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();

        foreach (var nestedField in field.NestedFields)
        {
            var propertyName = ToPascalCase(nestedField.Name);

            if (nestedField.ValueType == LuaValueType.Table)
            {
                var nestedClassName = $"{propertyName}Table";
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// Access the nested table '{nestedField.Name}'");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public {nestedClassName} {propertyName}");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        get");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            if (_cached{propertyName}Table == null)");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                var tableValue = _table.Get(\"{nestedField.Name}\");");
                sb.AppendLine($"{indent}                if (tableValue.Type != DataType.Table)");
                sb.AppendLine($"{indent}                    throw new InvalidOperationException(\"Lua field '{nestedField.Name}' is not a table\");");
                sb.AppendLine($"{indent}                _cached{propertyName}Table = new {nestedClassName}(tableValue.Table);");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}            return _cached{propertyName}Table;");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");
            }
            else
            {
                var fieldCsType = MapValueType(nestedField.ValueType, nestedField.ExplicitType);
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// Gets or sets the '{nestedField.Name}' field");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public {fieldCsType} {propertyName}");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        get => {GenerateTableGetterConversion(nestedField.ValueType, nestedField.Name)};");
                sb.AppendLine($"{indent}        set => _table[\"{nestedField.Name}\"] = {GenerateSetterConversion(nestedField.ValueType, "value")};");
                sb.AppendLine($"{indent}    }}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// Gets the underlying MoonSharp Table for advanced access");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    public Table RawTable => _table;");
        sb.AppendLine();

        foreach (var deeper in deeperTables)
        {
            GenerateNestedTableClass(sb, deeper, indent + "    ");
        }

        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    // ==========================================================================
    // Type Mapping Methods
    // ==========================================================================

    /// <summary>
    /// Maps a Lua value type to its corresponding C# type string.
    /// </summary>
    /// <param name="valueType">The inferred Lua value type.</param>
    /// <param name="explicitType">Optional explicit type from LuaLS annotation.</param>
    /// <returns>C# type name as a string (e.g., "double", "string", "DynValue").</returns>
    /// <remarks>
    /// Explicit types from annotations take precedence over inferred types.
    /// Unknown or complex types fall back to <c>DynValue</c> for flexibility.
    /// </remarks>
    private string MapValueType(LuaValueType valueType, string? explicitType)
    {
        // Explicit annotation takes precedence
        if (!string.IsNullOrEmpty(explicitType))
        {
            return explicitType switch
            {
                "number" => "double",
                "string" => "string",
                "boolean" or "bool" => "bool",
                "integer" or "int" => "int",
                _ => "DynValue" // Unknown types use DynValue for safety
            };
        }

        // Fall back to inferred type
        return valueType switch
        {
            LuaValueType.Number => "double",
            LuaValueType.String => "string",
            LuaValueType.Boolean => "bool",
            LuaValueType.Table => "Table",
            LuaValueType.Nil => "DynValue",
            _ => "DynValue"
        };
    }

    /// <summary>
    /// Maps a LuaLS parameter type annotation to a C# type string.
    /// </summary>
    /// <param name="explicitType">The type string from a <c>---@param</c> annotation.</param>
    /// <returns>C# type name as a string.</returns>
    /// <remarks>
    /// Parameters without explicit types use <c>DynValue</c> to accept any Lua value.
    /// </remarks>
    private string MapParameterType(string? explicitType)
    {
        if (string.IsNullOrEmpty(explicitType))
            return "DynValue";

        return explicitType switch
        {
            "number" => "double",
            "string" => "string",
            "boolean" or "bool" => "bool",
            "integer" or "int" => "int",
            "table" => "Table",
            "function" => "DynValue",
            _ => "DynValue"
        };
    }

    /// <summary>
    /// Maps a LuaLS return type annotation to a C# type string.
    /// </summary>
    /// <param name="explicitType">The type string from a <c>---@return</c> annotation.</param>
    /// <returns>C# type name as a string, or "void" for no return type.</returns>
    /// <remarks>
    /// Functions without explicit return types generate <c>void</c> methods.
    /// </remarks>
    private string MapReturnType(string? explicitType)
    {
        if (string.IsNullOrEmpty(explicitType))
            return "void";

        return explicitType switch
        {
            "number" => "double",
            "string" => "string",
            "boolean" or "bool" => "bool",
            "integer" or "int" => "int",
            "nil" or "void" => "void",
            "table" => "Table",
            _ => "DynValue"
        };
    }

    // ==========================================================================
    // Value Conversion Code Generation
    // ==========================================================================

    /// <summary>
    /// Generates the C# expression to read a global variable from the Script.Globals table.
    /// </summary>
    /// <param name="valueType">The Lua value type being read.</param>
    /// <param name="globalName">The Lua global variable name.</param>
    /// <returns>C# expression string that retrieves and converts the value.</returns>
    /// <example>
    /// For Number type and "score": <c>_script.Globals.Get("score").Number</c>
    /// </example>
    private string GenerateGetterConversion(LuaValueType valueType, string globalName)
    {
        return valueType switch
        {
            LuaValueType.Number => $"_script.Globals.Get(\"{globalName}\").Number",
            LuaValueType.String => $"_script.Globals.Get(\"{globalName}\").String",
            LuaValueType.Boolean => $"_script.Globals.Get(\"{globalName}\").Boolean",
            _ => $"_script.Globals.Get(\"{globalName}\")"
        };
    }

    /// <summary>
    /// Generates the C# expression to read a field from a MoonSharp Table.
    /// </summary>
    /// <param name="valueType">The Lua value type being read.</param>
    /// <param name="fieldName">The table field name.</param>
    /// <returns>C# expression string that retrieves and converts the value.</returns>
    /// <example>
    /// For Number type and "x": <c>_table.Get("x").Number</c>
    /// </example>
    private string GenerateTableGetterConversion(LuaValueType valueType, string fieldName)
    {
        return valueType switch
        {
            LuaValueType.Number => $"_table.Get(\"{fieldName}\").Number",
            LuaValueType.String => $"_table.Get(\"{fieldName}\").String",
            LuaValueType.Boolean => $"_table.Get(\"{fieldName}\").Boolean",
            _ => $"_table.Get(\"{fieldName}\")"
        };
    }

    /// <summary>
    /// Generates the C# expression to convert a C# value for assignment to Lua.
    /// </summary>
    /// <param name="valueType">The target Lua value type.</param>
    /// <param name="valueName">The C# variable name holding the value to assign.</param>
    /// <returns>C# expression string suitable for assignment to a Lua table or global.</returns>
    /// <remarks>
    /// Most types can be assigned directly due to MoonSharp's implicit conversions.
    /// Booleans require explicit <c>DynValue.NewBoolean()</c> wrapping.
    /// </remarks>
    private string GenerateSetterConversion(LuaValueType valueType, string valueName)
    {
        return valueType switch
        {
            LuaValueType.Number => valueName,   // MoonSharp handles double conversion
            LuaValueType.String => valueName,   // MoonSharp handles string conversion
            LuaValueType.Boolean => $"DynValue.NewBoolean({valueName})",  // Explicit wrapping needed
            _ => valueName
        };
    }

    /// <summary>
    /// Generates the C# expression to convert a Lua function call result to the target C# type.
    /// </summary>
    /// <param name="returnType">The target C# return type.</param>
    /// <returns>C# expression string that extracts the typed value from <c>result</c>.</returns>
    /// <example>
    /// For "double": <c>result.Number</c>
    /// For "int": <c>(int)result.Number</c>
    /// </example>
    private string GenerateReturnConversion(string returnType)
    {
        return returnType switch
        {
            "double" => "result.Number",
            "string" => "result.String",
            "bool" => "result.Boolean",
            "int" => "(int)result.Number",  // Lua numbers are always doubles
            "Table" => "result.Table",
            _ => "result"
        };
    }

    // ==========================================================================
    // Utility Methods
    // ==========================================================================

    /// <summary>
    /// Converts a snake_case Lua identifier to PascalCase for C# naming conventions.
    /// </summary>
    /// <param name="name">The Lua identifier (e.g., "player_health", "update").</param>
    /// <returns>PascalCase version (e.g., "PlayerHealth", "Update").</returns>
    /// <remarks>
    /// Handles multiple underscores and preserves case within segments.
    /// Empty or null input returns unchanged.
    /// </remarks>
    /// <example>
    /// <code>
    /// ToPascalCase("update")         // "Update"
    /// ToPascalCase("player_health")  // "PlayerHealth"
    /// ToPascalCase("get_x_position") // "GetXPosition"
    /// </code>
    /// </example>
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Split on underscores and capitalize each segment
        var parts = name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1).ToLowerInvariant());
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Reads a configuration value from available sources (analyzer options, MSBuild, etc.).
    /// </summary>
    /// <param name="context">The generator execution context.</param>
    /// <param name="key">The configuration key to look up.</param>
    /// <param name="defaultValue">Value to return if the key is not found.</param>
    /// <returns>The configured value, or <paramref name="defaultValue"/> if not found.</returns>
    /// <remarks>
    /// <para>Configuration sources are checked in order:</para>
    /// <list type="number">
    ///   <item>Global analyzer options (from .editorconfig)</item>
    ///   <item>MSBuild properties (via build_property.MoonSharpBinder_*)</item>
    ///   <item>Per-file analyzer options</item>
    /// </list>
    /// </remarks>
    private string GetConfigurationValue(GeneratorExecutionContext context, string key, string defaultValue)
    {
        // Try global analyzer options (.editorconfig)
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key, out var globalValue) && !string.IsNullOrWhiteSpace(globalValue))
        {
            return globalValue;
        }

        // Try MSBuild properties (build_property.MoonSharpBinder_Namespace format)
        var msbuildKey = $"build_property.{key.Replace("moonsharp_binder.", "MoonSharpBinder_")}";
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(msbuildKey, out var msbuildValue) && !string.IsNullOrWhiteSpace(msbuildValue))
        {
            return msbuildValue;
        }

        // Try per-file options
        foreach (var file in context.AdditionalFiles)
        {
            var options = context.AnalyzerConfigOptions.GetOptions(file);
            if (options.TryGetValue(key, out var fileValue) && !string.IsNullOrWhiteSpace(fileValue))
            {
                return fileValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Reports a diagnostic message to the compilation (warnings, errors, etc.).
    /// </summary>
    /// <param name="context">The generator execution context.</param>
    /// <param name="id">Diagnostic ID (e.g., "MSHB001").</param>
    /// <param name="title">Short title for the diagnostic.</param>
    /// <param name="message">Detailed message describing the issue.</param>
    /// <param name="severity">The severity level (Warning, Error, etc.).</param>
    /// <remarks>
    /// Diagnostics appear in the IDE error list and build output.
    /// Using Warning severity for parse errors prevents build failures on malformed Lua.
    /// </remarks>
    private void ReportDiagnostic(GeneratorExecutionContext context, string id, string title, string message, DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor(id, title, message, "MoonSharpBinder", severity, true);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
    }
}
