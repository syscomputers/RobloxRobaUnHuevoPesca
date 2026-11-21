# Repository Guidelines

## Project Structure & Module Organization

`Pesca.slnx` is the Visual Studio solution and references the console application in `Pesca/Pesca.csproj`. The project targets .NET 10, enables nullable reference types and implicit usings, and currently contains only a top-level `Program.cs` that prints `Hello, World!`.

Keep application source under `Pesca/`. There are currently no dedicated test or asset directories. `.vs/`, `bin/`, and `obj/` contain local IDE or generated build data; exclude them from contributions. Despite the directory name, this checkout contains no Roblox or Luau implementation.

## Build, Test, and Development Commands

Run these commands from the directory containing `Pesca.slnx`, using a .NET 10 SDK:

```powershell
dotnet restore Pesca.slnx
dotnet build Pesca.slnx --no-restore
dotnet run --project Pesca/Pesca.csproj
```

These restore dependencies, compile the solution, and run the console application. Visual Studio can also open `Pesca.slnx` directly.

## Coding Style & Naming Conventions

Use four-space indentation for C# code, PascalCase for types and public members, and camelCase for parameters and local variables. Keep the existing top-level entry point simple; move substantial behavior into focused classes as the application grows. Respect nullable annotations and resolve compiler warnings introduced by changes.

No formatter, linter, or `.editorconfig` is currently configured. Keep formatting changes limited to the code being edited.

## Testing Guidelines

No test project, testing framework, or coverage threshold is currently defined. For current changes, build the solution and run the application to verify console output. Document the observed result in the pull request.

When adding automated tests, place them in `tests/Pesca.Tests/`, add the project to the solution, and use descriptive names such as `Action_Condition_ExpectedResult`. Document the chosen framework and test command when introducing it.

## Commit & Pull Request Guidelines

This checkout has no Git metadata, so commit conventions cannot be established from local history. Use concise, imperative commit subjects and keep each change focused.

Pull requests should explain the change, link relevant issues, and report validation commands and results. Include example console output when behavior changes. Keep credentials and machine-specific settings out of contributions.
