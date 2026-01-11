# Contributing to MoonSharp Binder

First off, **thank you** for considering contributing! I truly believe in open source and the power of community collaboration. Unlike many repositories, I actively welcome contributions of all kinds - from bug fixes to new features.

## My Promise to Contributors

- **I will respond to every PR and issue** - I guarantee feedback on all contributions
- **Bug fixes are obvious accepts** - If it fixes a bug, it's getting merged
- **New features are welcome** - I'm genuinely open to new ideas and enhancements
- **Direct line of communication** - If I'm not responding to a PR or issue, email me directly at johnvondrashek@gmail.com

## What Makes a Great Contribution

### Bug Fixes

Found a bug? Fix it and submit a PR! Bug fixes are the easiest contributions to accept. If you can include a test that reproduces the issue, even better.

### New Features

Some ideas for contributions:

- **Additional LuaLS annotation support** - Extend type inference with more annotation types
- **New Lua patterns** - Support for additional Lua constructs in the parser
- **Documentation improvements** - Better examples, tutorials, or clarifications
- **Performance optimizations** - Faster code generation or runtime bindings

### Tests

The project uses xUnit for testing. Tests live in `tests/MoonSharpBinder.Tests/`. Adding tests for edge cases or new features is always appreciated.

## Development Setup

1. Clone the repository
2. Open the solution in your IDE (Visual Studio, Rider, or VS Code with C# Dev Kit)
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Run tests:
   ```bash
   dotnet test
   ```

## Project Structure

- `src/MoonSharpBinder/` - The Roslyn source generator
- `tests/MoonSharpBinder.Tests/` - Unit tests
- `docs/wiki/` - Documentation

## Code Style

- Follow existing code conventions in the project
- Use meaningful variable and method names
- Keep methods focused and small
- Add XML documentation for public APIs

## Submitting Changes

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Run tests to ensure nothing is broken
5. Commit your changes with a clear commit message
6. Push to your fork
7. Open a Pull Request

## Code of Conduct

This project follows the [Rule of St. Benedict](CODE_OF_CONDUCT.md) as its code of conduct.

## Questions?

- Open an issue
- Email: johnvondrashek@gmail.com
