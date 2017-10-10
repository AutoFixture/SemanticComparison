[![Build status](https://ci.appveyor.com/api/projects/status/1fgr4ijfcaoijfs2?svg=true)](https://ci.appveyor.com/project/AutoFixture/semanticcomparison) [![NuGet version](https://img.shields.io/nuget/v/SemanticComparison.svg)](https://www.nuget.org/packages/SemanticComparison)

# Announcement

## Assembly name and namespace

Recently the ownership of this project has been passed from Mark Seemann to maintainers of the AutoFixture organization. To reflect that change the default namespace prefix and assembly name were changed from `Phoeh.SemanticComparison` to `SemanticComparison`. Please use the text replace feature of your IDE to quickly fix all the namespace imports.

## Project relocation

The SemanticComparsion project has been extracted from the [AutoFixture](https://github.com/AutoFixture/AutoFixture) repository to its own repository. The primary goal of this change is to improve the release management, so the AutoFixture product releases don't produce new verisons of the SemanticComparison package. SemanticComparison is a totally separate product without any dependencies on the AutoFixture, so now it could live in its own nest :blush:

Refer to the old repository to find the issue and pull request history.

# SemanticComparison

SemanticComparison makes it easier to compare instances of various objects to each other. Instead of performing a normal equality comparison, SemanticComparison compares objects that look semantically similar - even if they are of different types.

## .NET platforms compatibility table

| Product            | .NET Framework            | .NET Standard            |
| ------------------ | ------------------------  | ------------------------ |
| SemanticComparison | :heavy_check_mark: 4.5.2  | :heavy_check_mark: 1.5   |

## Downloads

SemanticComparison is available via NuGet only. Use the Package Manager to install the `SemanticComparison` package.

## Versioning

AutoFixture follows [Semantic Versioning 2.0.0](http://semver.org/spec/v2.0.0.html) for the public releases (published to the [nuget.org](https://www.nuget.org/)).

## Build

SemanticComparison uses [FAKE](http://fsharp.github.io/FAKE/) as a build engine. If you would like to build the SemanticComparison locally, run the `Build.cmd` file and wait for the result.

The repository state (the last tag name and number of commits since the last tag) is used to determine the build version. If you would like to override the auto-generated SemanticComparison version, pass the `BuildVersion` parameter to the `Build.cmd` file. For example:
```
Build.cmd BuildVersion=4.0.0
```

Refer to the [Build.fsx](Build.fsx) file to get information about all the supported build keys.

