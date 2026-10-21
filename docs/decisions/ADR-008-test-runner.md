# ADR-008: Dependency-free test runner for Core
Status: Accepted · Date: 2026-10-20

## Context
The base was built in an environment without NuGet access. Core tests had to run there to be verified.

## Decision
`tests/WheelConvert.Core.Tests` is a console app with a ~100-line `[Test]` runner (`TestFramework.cs`). Run with `dotnet run --project tests/WheelConvert.Core.Tests`; exit code 0 = pass. CI runs it on Windows.

Converter modules that need packages may add their own test projects with xUnit (`tests/WheelConvert.Converters.<Name>.Tests`), since they need NuGet anyway.

## Consequences
- Core tests run anywhere with only the .NET SDK.
- Migrating Core tests to xUnit later is mechanical (`[Test]` → `[Fact]`, `Assert.Equal` signatures match).
