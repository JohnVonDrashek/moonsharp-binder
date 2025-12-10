// =============================================================================
// LuaParser.cs
// =============================================================================
// A lightweight Lua source code parser designed for compile-time code generation.
// This parser extracts structural information (functions, globals, type annotations)
// from Lua source files without executing them. It's intentionally simple and
// regex-based to work reliably in the constrained source generator environment.
//
// Key capabilities:
// - Parses function declarations (both global and local)
// - Extracts global variable assignments with type inference
// - Recognizes LuaLS type annotations (---@type, ---@param, ---@return)
// - Handles multi-line tables and nested structures
// - Skips multi-line comments
//
// Limitations (by design):
// - Does not handle all Lua syntax edge cases
// - Regex-based approach may miss complex patterns
// - Nested tables have limited depth support
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MoonSharpBinder;

/// <summary>
/// Represents the inferred data type of a Lua value.
/// </summary>
/// <remarks>
/// These types map directly to Lua's runtime types and are used to generate
/// appropriate C# type mappings in the binding classes.
/// </remarks>
public enum LuaValueType
{
    /// <summary>
    /// Type could not be determined from the source.
    /// Maps to <c>DynValue</c> in generated C#.
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Lua number type (all numbers in Lua are double-precision floating point).
    /// Maps to <c>double</c> in generated C#.
    /// </summary>
    Number,
    
    /// <summary>
    /// Lua string type.
    /// Maps to <c>string</c> in generated C#.
    /// </summary>
    String,
    
    /// <summary>
    /// Lua boolean type (true/false).
    /// Maps to <c>bool</c> in generated C#.
    /// </summary>
    Boolean,
    
    /// <summary>
    /// Lua table type (associative array / object).
    /// Generates a nested wrapper class in C#.
    /// </summary>
    Table,
    
    /// <summary>
    /// Lua function type.
    /// Maps to <c>DynValue</c> in generated C#.
    /// </summary>
    Function,
    
    /// <summary>
    /// Lua nil type (absence of value).
    /// Maps to <c>DynValue</c> in generated C#.
    /// </summary>
    Nil
}

/// <summary>
/// Represents a single field within a Lua table definition.
/// </summary>
/// <remarks>
/// Table fields are extracted when parsing table literal assignments like:
/// <code>
/// player = {
///     x = 100,        -- Creates a LuaTableField with Name="x", ValueType=Number
///     name = "hero",  -- Creates a LuaTableField with Name="name", ValueType=String
///     stats = { ... } -- Creates a LuaTableField with nested fields
/// }
/// </code>
/// </remarks>
public class LuaTableField
{
    /// <summary>
    /// Gets or sets the field name as it appears in the Lua source.
    /// </summary>
    /// <example>For <c>x = 100</c>, the Name would be "x".</example>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the inferred value type of this field.
    /// </summary>
    /// <remarks>
    /// Inferred from the assigned value's syntax (e.g., numbers, quoted strings, etc.)
    /// </remarks>
    public LuaValueType ValueType { get; set; } = LuaValueType.Unknown;
    
    /// <summary>
    /// Gets or sets the nested fields if this field's value is itself a table.
    /// </summary>
    /// <remarks>
    /// Populated only when <see cref="ValueType"/> is <see cref="LuaValueType.Table"/>.
    /// Enables recursive table structure generation in C#.
    /// </remarks>
    public List<LuaTableField> NestedFields { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the explicit type from a LuaLS annotation, if present.
    /// </summary>
    /// <example>
    /// For the annotation <c>---@type number</c>, this would be "number".
    /// </example>
    public string? ExplicitType { get; set; }
}

/// <summary>
/// Represents a parameter in a Lua function declaration.
/// </summary>
/// <remarks>
/// Parameters can have explicit types from LuaLS annotations:
/// <code>
/// ---@param damage number
/// ---@param target string
/// function apply_damage(damage, target)
/// </code>
/// </remarks>
public class LuaParameter
{
    /// <summary>
    /// Gets or sets the parameter name as declared in the function signature.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the explicit type from a <c>---@param</c> annotation, if present.
    /// </summary>
    /// <example>"number", "string", "boolean", "table", etc.</example>
    public string? ExplicitType { get; set; }
    
    /// <summary>
    /// Gets the <see cref="LuaValueType"/> inferred from the <see cref="ExplicitType"/> string.
    /// </summary>
    /// <remarks>
    /// Converts common LuaLS type strings to their enum equivalents.
    /// Returns <see cref="LuaValueType.Unknown"/> for unrecognized types.
    /// </remarks>
    public LuaValueType InferredType => ExplicitType switch
    {
        "number" => LuaValueType.Number,
        "string" => LuaValueType.String,
        "boolean" or "bool" => LuaValueType.Boolean,
        "table" => LuaValueType.Table,
        "function" => LuaValueType.Function,
        "nil" => LuaValueType.Nil,
        _ => LuaValueType.Unknown
    };
}

/// <summary>
/// Represents a Lua function declaration extracted from source code.
/// </summary>
/// <remarks>
/// Captures both the function's signature and any LuaLS type annotations.
/// Only global functions (not marked <c>local</c>) are exposed in generated bindings.
/// 
/// <para>Example Lua function:</para>
/// <code>
/// ---@param x number
/// ---@param y number
/// ---@return number
/// function calculate_distance(x, y)
///     return math.sqrt(x*x + y*y)
/// end
/// </code>
/// </remarks>
public class LuaFunction
{
    /// <summary>
    /// Gets or sets the function name as declared in Lua.
    /// </summary>
    /// <remarks>
    /// Converted to PascalCase for the generated C# method name.
    /// </remarks>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the list of parameters declared in the function signature.
    /// </summary>
    public List<LuaParameter> Parameters { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the return type from a <c>---@return</c> annotation, if present.
    /// </summary>
    /// <remarks>
    /// When null or empty, the generated C# method returns <c>void</c>.
    /// </remarks>
    public string? ReturnType { get; set; }
    
    /// <summary>
    /// Gets or sets whether this function is declared with the <c>local</c> keyword.
    /// </summary>
    /// <remarks>
    /// Local functions are not exposed in generated bindings since they're
    /// not accessible via the MoonSharp Globals table.
    /// </remarks>
    public bool IsLocal { get; set; }
}

/// <summary>
/// Represents a Lua global variable or table assignment.
/// </summary>
/// <remarks>
/// Captures top-level variable assignments in Lua source:
/// <code>
/// score = 0           -- Simple global (LuaValueType.Number)
/// player = { ... }    -- Table global (LuaValueType.Table with TableFields)
/// local temp = 10     -- Local (IsLocal=true, not exposed)
/// </code>
/// </remarks>
public class LuaGlobal
{
    /// <summary>
    /// Gets or sets the variable name as assigned in Lua.
    /// </summary>
    /// <remarks>
    /// Converted to PascalCase for the generated C# property name.
    /// </remarks>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the inferred value type based on the assigned value's syntax.
    /// </summary>
    public LuaValueType ValueType { get; set; } = LuaValueType.Unknown;
    
    /// <summary>
    /// Gets or sets the parsed fields if this global is a table.
    /// </summary>
    /// <remarks>
    /// Only populated when <see cref="ValueType"/> is <see cref="LuaValueType.Table"/>.
    /// Used to generate nested wrapper classes for table access.
    /// </remarks>
    public List<LuaTableField> TableFields { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the explicit type from a <c>---@type</c> annotation, if present.
    /// </summary>
    public string? ExplicitType { get; set; }
    
    /// <summary>
    /// Gets or sets whether this variable is declared with the <c>local</c> keyword.
    /// </summary>
    /// <remarks>
    /// Local variables are not exposed in generated bindings since they're
    /// not accessible via the MoonSharp Globals table.
    /// </remarks>
    public bool IsLocal { get; set; }
}

/// <summary>
/// Contains the complete results of parsing a Lua source file.
/// </summary>
/// <remarks>
/// This is the output of <see cref="LuaParser.Parse"/> and contains all
/// extracted functions, globals, and any errors encountered during parsing.
/// </remarks>
public class LuaParseResult
{
    /// <summary>
    /// Gets or sets the source file name (without extension).
    /// </summary>
    /// <remarks>
    /// Used to generate the binding class name (e.g., "sprite" → "SpriteScript").
    /// </remarks>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the list of global functions found in the Lua source.
    /// </summary>
    /// <remarks>
    /// Only contains non-local functions that can be called via MoonSharp.
    /// </remarks>
    public List<LuaFunction> Functions { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the list of global variables found in the Lua source.
    /// </summary>
    /// <remarks>
    /// Only contains non-local variables accessible via MoonSharp Globals.
    /// </remarks>
    public List<LuaGlobal> Globals { get; set; } = new();
    
    /// <summary>
    /// Gets or sets any errors encountered during parsing.
    /// </summary>
    /// <remarks>
    /// Parse errors don't necessarily stop generation—partial results may still be usable.
    /// </remarks>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Static parser that extracts function declarations and global assignments from Lua source code.
/// </summary>
/// <remarks>
/// <para>
/// This parser is designed to work at compile-time in a Roslyn source generator context.
/// It uses regex-based parsing rather than a full Lua AST parser for several reasons:
/// </para>
/// <list type="bullet">
///   <item>Source generators have limited dependencies available</item>
///   <item>We only need structural information, not execution semantics</item>
///   <item>Regex is fast and predictable for the patterns we care about</item>
/// </list>
/// 
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// var luaSource = File.ReadAllText("game.lua");
/// var result = LuaParser.Parse(luaSource, "game");
/// 
/// foreach (var func in result.Functions)
/// {
///     Console.WriteLine($"Found function: {func.Name}");
/// }
/// </code>
/// 
/// <para><strong>Supported Constructs:</strong></para>
/// <list type="bullet">
///   <item>Global and local function declarations</item>
///   <item>Global and local variable assignments</item>
///   <item>Table literals (single and multi-line)</item>
///   <item>LuaLS annotations: <c>---@type</c>, <c>---@param</c>, <c>---@return</c></item>
///   <item>Single-line and multi-line comments</item>
/// </list>
/// </remarks>
public static class LuaParser
{
    // ==========================================================================
    // Regular Expression Patterns
    // ==========================================================================
    // These patterns are compiled once and reused for better performance.
    
    /// <summary>
    /// Matches function declarations: <c>function name(params)</c> or <c>local function name(params)</c>
    /// </summary>
    /// <remarks>
    /// Captures:
    /// - <c>local</c>: Whether the function is local
    /// - <c>name</c>: The function identifier
    /// - <c>params</c>: The comma-separated parameter list
    /// </remarks>
    private static readonly Regex FunctionPattern = new(
        @"^(?<local>local\s+)?function\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\((?<params>[^)]*)\)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    
    /// <summary>
    /// Matches global/local variable assignments: <c>name = value</c> or <c>local name = value</c>
    /// </summary>
    /// <remarks>
    /// Captures:
    /// - <c>local</c>: Whether the variable is local
    /// - <c>name</c>: The variable identifier
    /// - <c>value</c>: The assigned value (used for type inference)
    /// </remarks>
    private static readonly Regex GlobalAssignmentPattern = new(
        @"^(?<local>local\s+)?(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(?<value>.+?)(?:$|(?=\s*--))",
        RegexOptions.Multiline | RegexOptions.Compiled);
    
    /// <summary>
    /// Matches LuaLS type annotations: <c>---@type typename</c>
    /// </summary>
    private static readonly Regex LuaLsTypeAnnotation = new(
        @"---@type\s+(?<type>\S+)",
        RegexOptions.Compiled);
    
    /// <summary>
    /// Matches LuaLS parameter annotations: <c>---@param name typename</c>
    /// </summary>
    private static readonly Regex LuaLsParamAnnotation = new(
        @"---@param\s+(?<name>\S+)\s+(?<type>\S+)",
        RegexOptions.Compiled);
    
    /// <summary>
    /// Matches LuaLS return type annotations: <c>---@return typename</c>
    /// </summary>
    private static readonly Regex LuaLsReturnAnnotation = new(
        @"---@return\s+(?<type>\S+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a Lua source file and extracts all functions and global variables.
    /// </summary>
    /// <param name="luaSource">The complete Lua source code as a string.</param>
    /// <param name="fileName">The file name (without extension) used for naming the generated class.</param>
    /// <returns>A <see cref="LuaParseResult"/> containing all extracted information.</returns>
    /// <remarks>
    /// <para>
    /// The parser processes the source line-by-line, collecting LuaLS annotations
    /// and associating them with the next function or variable declaration.
    /// </para>
    /// 
    /// <para><strong>Important behaviors:</strong></para>
    /// <list type="bullet">
    ///   <item>Local functions/variables are parsed but not included in results</item>
    ///   <item>Multi-line comments (<c>--[[ ]]</c>) are skipped entirely</item>
    ///   <item>Table contents are parsed separately to extract field information</item>
    ///   <item>Brace depth tracking prevents misinterpreting table contents as globals</item>
    /// </list>
    /// 
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var source = @"
    /// ---@param x number
    /// function move(x)
    ///     position = position + x
    /// end
    /// 
    /// player = { health = 100, name = 'hero' }
    /// ";
    /// 
    /// var result = LuaParser.Parse(source, "game");
    /// // result.Functions[0].Name == "move"
    /// // result.Functions[0].Parameters[0].ExplicitType == "number"
    /// // result.Globals[0].Name == "player"
    /// // result.Globals[0].TableFields.Count == 2
    /// </code>
    /// </remarks>
    public static LuaParseResult Parse(string luaSource, string fileName)
    {
        var result = new LuaParseResult { FileName = fileName };
        
        try
        {
            var lines = luaSource.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Collect annotations (---@type, ---@param, ---@return) to apply to the next declaration
            var pendingAnnotations = new List<string>();
            
            // Track brace depth to know when we're inside a table literal
            var braceDepth = 0;
            
            // Track multi-line comment state
            var inMultiLineComment = false;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.TrimStart();
                
                // Handle multi-line comments: --[[ ... ]]
                if (trimmedLine.StartsWith("--[["))
                {
                    inMultiLineComment = true;
                }
                if (inMultiLineComment)
                {
                    if (trimmedLine.Contains("]]"))
                    {
                        inMultiLineComment = false;
                    }
                    continue;
                }
                
                // Count braces to track table literal depth
                // This prevents us from treating table field assignments as global assignments
                foreach (var ch in line)
                {
                    if (ch == '{') braceDepth++;
                    if (ch == '}') braceDepth--;
                }
                
                // Skip lines that are inside table literals (except the line that starts the table)
                var startsTable = trimmedLine.Contains("= {") || trimmedLine.Contains("={");
                if (braceDepth > 0 && !startsTable)
                {
                    continue;
                }
                
                // Collect LuaLS annotations - these apply to the next declaration
                if (trimmedLine.StartsWith("---@"))
                {
                    pendingAnnotations.Add(trimmedLine);
                    continue;
                }
                
                // Skip regular comments (single-line)
                if (trimmedLine.StartsWith("--"))
                {
                    continue;
                }
                
                // Try to match a function declaration
                var funcMatch = FunctionPattern.Match(trimmedLine);
                if (funcMatch.Success)
                {
                    var func = ParseFunction(funcMatch, pendingAnnotations);
                    if (!func.IsLocal) // Only expose non-local functions
                    {
                        result.Functions.Add(func);
                    }
                    pendingAnnotations.Clear();
                    continue;
                }
                
                // Try to match a global/local variable assignment
                var assignMatch = GlobalAssignmentPattern.Match(trimmedLine);
                if (assignMatch.Success)
                {
                    var global = ParseGlobal(assignMatch, pendingAnnotations, lines, i);
                    if (global != null && !global.IsLocal) // Only expose non-local globals
                    {
                        result.Globals.Add(global);
                    }
                    pendingAnnotations.Clear();
                    continue;
                }
                
                // Clear accumulated annotations if we hit a non-matching, non-empty line
                // This prevents annotations from accidentally applying to wrong declarations
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    pendingAnnotations.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Parse error: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Parses a function declaration from a regex match and applies any pending LuaLS annotations.
    /// </summary>
    /// <param name="match">The regex match containing function name, parameters, and locality.</param>
    /// <param name="annotations">List of LuaLS annotation strings (---@param, ---@return) to apply.</param>
    /// <returns>A populated <see cref="LuaFunction"/> instance.</returns>
    private static LuaFunction ParseFunction(Match match, List<string> annotations)
    {
        var func = new LuaFunction
        {
            Name = match.Groups["name"].Value,
            IsLocal = match.Groups["local"].Success
        };
        
        // Parse the comma-separated parameter list
        var paramsStr = match.Groups["params"].Value;
        if (!string.IsNullOrWhiteSpace(paramsStr))
        {
            var paramNames = paramsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var paramName in paramNames)
            {
                var trimmedName = paramName.Trim();
                if (!string.IsNullOrEmpty(trimmedName))
                {
                    func.Parameters.Add(new LuaParameter { Name = trimmedName });
                }
            }
        }
        
        // Apply LuaLS annotations to parameters and return type
        foreach (var annotation in annotations)
        {
            // Match ---@param name type
            var paramMatch = LuaLsParamAnnotation.Match(annotation);
            if (paramMatch.Success)
            {
                var paramName = paramMatch.Groups["name"].Value;
                var paramType = paramMatch.Groups["type"].Value;
                
                // Find the parameter by name and set its explicit type
                var param = func.Parameters.Find(p => p.Name == paramName);
                if (param != null)
                {
                    param.ExplicitType = paramType;
                }
            }
            
            // Match ---@return type
            var returnMatch = LuaLsReturnAnnotation.Match(annotation);
            if (returnMatch.Success)
            {
                func.ReturnType = returnMatch.Groups["type"].Value;
            }
        }
        
        return func;
    }

    /// <summary>
    /// Parses a global variable assignment from a regex match and applies any pending LuaLS annotations.
    /// </summary>
    /// <param name="match">The regex match containing variable name, value, and locality.</param>
    /// <param name="annotations">List of LuaLS annotation strings (---@type) to apply.</param>
    /// <param name="lines">All source lines (needed for multi-line table parsing).</param>
    /// <param name="lineIndex">Current line index in the source.</param>
    /// <returns>A populated <see cref="LuaGlobal"/> instance, or null for function assignments.</returns>
    private static LuaGlobal? ParseGlobal(Match match, List<string> annotations, string[] lines, int lineIndex)
    {
        var name = match.Groups["name"].Value;
        var valueStr = match.Groups["value"].Value.Trim();
        var isLocal = match.Groups["local"].Success;
        
        // Skip function assignments like `foo = function()` - these are handled as functions
        if (valueStr.StartsWith("function"))
        {
            return null;
        }
        
        var global = new LuaGlobal
        {
            Name = name,
            IsLocal = isLocal,
            ValueType = InferValueType(valueStr)
        };
        
        // Apply ---@type annotation if present
        foreach (var annotation in annotations)
        {
            var typeMatch = LuaLsTypeAnnotation.Match(annotation);
            if (typeMatch.Success)
            {
                global.ExplicitType = typeMatch.Groups["type"].Value;
            }
        }
        
        // If the value is a table, recursively parse its fields
        if (global.ValueType == LuaValueType.Table)
        {
            global.TableFields = ParseTableFields(valueStr, lines, lineIndex);
        }
        
        return global;
    }

    /// <summary>
    /// Infers the <see cref="LuaValueType"/> from a Lua value's string representation.
    /// </summary>
    /// <param name="valueStr">The right-hand side of an assignment, trimmed.</param>
    /// <returns>The inferred type, or <see cref="LuaValueType.Unknown"/> if indeterminate.</returns>
    /// <remarks>
    /// <para>Type inference rules:</para>
    /// <list type="bullet">
    ///   <item>Starts with <c>{</c> → Table</item>
    ///   <item>Starts with <c>"</c>, <c>'</c>, or <c>[[</c> → String</item>
    ///   <item>Is <c>true</c> or <c>false</c> → Boolean</item>
    ///   <item>Is <c>nil</c> → Nil</item>
    ///   <item>Matches numeric pattern → Number</item>
    ///   <item>Starts with <c>function</c> → Function</item>
    ///   <item>Otherwise → Unknown</item>
    /// </list>
    /// </remarks>
    private static LuaValueType InferValueType(string valueStr)
    {
        valueStr = valueStr.Trim();
        
        // Table literal
        if (valueStr.StartsWith("{"))
        {
            return LuaValueType.Table;
        }
        
        // String literal (double quotes, single quotes, or long brackets)
        if (valueStr.StartsWith("\"") || valueStr.StartsWith("'") || valueStr.StartsWith("[["))
        {
            return LuaValueType.String;
        }
        
        // Boolean literals
        if (valueStr == "true" || valueStr == "false")
        {
            return LuaValueType.Boolean;
        }
        
        // Nil literal
        if (valueStr == "nil")
        {
            return LuaValueType.Nil;
        }
        
        // Number (integer or decimal, optionally negative)
        if (Regex.IsMatch(valueStr, @"^-?\d+\.?\d*$"))
        {
            return LuaValueType.Number;
        }
        
        // Function reference (anonymous function)
        if (valueStr.StartsWith("function"))
        {
            return LuaValueType.Function;
        }
        
        // Could be a variable reference or complex expression - can't determine type
        return LuaValueType.Unknown;
    }

    /// <summary>
    /// Parses the fields of a table literal assignment.
    /// </summary>
    /// <param name="valueStr">The value string starting with the table literal.</param>
    /// <param name="lines">All source lines (for multi-line tables).</param>
    /// <param name="startLine">The line index where the table starts.</param>
    /// <returns>A list of <see cref="LuaTableField"/> representing the table's fields.</returns>
    /// <remarks>
    /// Handles both single-line and multi-line table definitions:
    /// <code>
    /// -- Single line
    /// point = { x = 10, y = 20 }
    /// 
    /// -- Multi-line
    /// player = {
    ///     health = 100,
    ///     name = "hero",
    ///     position = { x = 0, y = 0 }
    /// }
    /// </code>
    /// </remarks>
    private static List<LuaTableField> ParseTableFields(string valueStr, string[] lines, int startLine)
    {
        var tableContent = ExtractTableContent(valueStr, lines, startLine);
        return ParseFields(tableContent);
    }

    /// <summary>
    /// Extracts the content between braces of a table literal, handling multi-line tables.
    /// </summary>
    /// <param name="valueStr">The value string that may contain the start of the table.</param>
    /// <param name="lines">All source lines.</param>
    /// <param name="startLine">The line index where the table starts.</param>
    /// <returns>The content between the opening and closing braces (excluding the braces).</returns>
    /// <remarks>
    /// Uses brace counting to correctly handle nested tables within the content.
    /// </remarks>
    private static string ExtractTableContent(string valueStr, string[] lines, int startLine)
    {
        // Check if the table is complete on a single line
        if (valueStr.Contains("}"))
        {
            var start = valueStr.IndexOf('{');
            var end = valueStr.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return valueStr.Substring(start + 1, end - start - 1);
            }
        }
        
        // Multi-line table: collect content until matching closing brace
        var content = new System.Text.StringBuilder();
        var braceCount = 0;
        var started = false;
        
        for (int i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var ch in line)
            {
                if (ch == '{')
                {
                    braceCount++;
                    started = true;
                    if (braceCount > 1)
                    {
                        content.Append(ch); // Keep nested braces for deeper tables
                    }
                    continue;
                }
                if (ch == '}')
                {
                    braceCount--;
                    if (braceCount == 0 && started)
                    {
                        return content.ToString();
                    }
                    if (braceCount >= 1)
                    {
                        content.Append(ch); // Preserve inner closing braces
                    }
                    continue;
                }
                if (started && braceCount > 0)
                {
                    content.Append(ch);
                }
            }
            // Replace newlines with spaces to normalize multi-line content
            if (started && braceCount > 0)
            {
                content.Append(' ');
            }
        }
        
        return content.ToString();
    }

    /// <summary>
    /// Parses fields from a nested table literal string.
    /// </summary>
    /// <param name="tableStr">A string containing a table literal (including braces).</param>
    /// <returns>A list of <see cref="LuaTableField"/> for the nested table's fields.</returns>
    private static List<LuaTableField> ParseNestedTable(string tableStr)
    {
        var content = TrimOuterBraces(tableStr);
        return ParseFields(content);
    }

    /// <summary>
    /// Parses table fields from a content string (without the outer braces).
    /// Handles nested tables by tracking brace depth to avoid splitting on commas inside nested braces.
    /// </summary>
    private static List<LuaTableField> ParseFields(string content)
    {
        var fields = new List<LuaTableField>();

        foreach (var segment in SplitTopLevelFields(content))
        {
            var eqIndex = segment.IndexOf('=');
            if (eqIndex <= 0) continue;

            var fieldName = segment.Substring(0, eqIndex).Trim();
            var fieldValue = segment.Substring(eqIndex + 1).Trim();
            if (string.IsNullOrEmpty(fieldName)) continue;

            var valueType = InferValueType(fieldValue);
            if (valueType == LuaValueType.Unknown && fieldValue.Contains("{"))
            {
                valueType = LuaValueType.Table;
            }

            var field = new LuaTableField
            {
                Name = fieldName,
                ValueType = valueType
            };

            if (valueType == LuaValueType.Table)
            {
                field.NestedFields = ParseNestedTable(fieldValue);
            }

            fields.Add(field);
        }

        return fields;
    }

    /// <summary>
    /// Splits a table content string into top-level field assignment segments, respecting nested brace depth.
    /// </summary>
    private static List<string> SplitTopLevelFields(string content)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in content)
        {
            if (ch == '{') depth++;
            if (ch == '}') depth = Math.Max(0, depth - 1);

            if (ch == ',' && depth == 0)
            {
                var segment = current.ToString().Trim();
                if (segment.Length > 0)
                {
                    segments.Add(segment);
                }
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        var last = current.ToString().Trim();
        if (last.Length > 0)
        {
            segments.Add(last);
        }

        return segments;
    }

    /// <summary>
    /// Removes the outermost braces from a table literal string, returning the inner content.
    /// </summary>
    private static string TrimOuterBraces(string tableStr)
    {
        var start = tableStr.IndexOf('{');
        var end = tableStr.LastIndexOf('}');
        if (start < 0 || end <= start) return tableStr;

        return tableStr.Substring(start + 1, end - start - 1);
    }
}
