Installation & Project Setup
============================

Targets
-------
- Libraries: .NET Standard 2.0+.
- Apps: any framework that can reference the package; generator runs at build time.

Get the package
---------------
```bash
dotnet add package MoonSharpBinder
```

Add to `.csproj`
----------------
```xml
<ItemGroup>
  <PackageReference Include="MoonSharpBinder" Version="*" />
</ItemGroup>
```

Add Lua files as AdditionalFiles
--------------------------------
```xml
<ItemGroup>
  <AdditionalFiles Include="Content/scripts/*.lua" />
</ItemGroup>
```
Use globbing for subfolders as needed.

Build output locations
----------------------
- Generated bindings: `obj/<tfm>/generated/*Script.g.cs`.
- Package assets: `bin/<configuration>/<tfm>/`.

IDE notes
---------
- Visual Studio/Rider: rebuild after adding new Lua files to see generated code in IntelliSense.
- VS Code: OmniSharp/Roslyn should pick up generated files after build or restart.

