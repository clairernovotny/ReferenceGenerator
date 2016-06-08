# NuSpec Reference Generator
## Overview
Package authoring for .NET Core based libraries (ASPNET Core 1.0, CoreCLR, UWP) has an extra burden on the author as .NET dependencies must be listed in addition to any regular packages you depend on. This could be a long list and it's a challenge to get it right. If you use any of the meta-packages that brings "all" of .NET Core into your project as possible references, how do you know which you actually need?

This tool aims to help by reading your compiled libraries assembly metadata and determine what that list should be. It currently supports any `System.Runtime` based project, including "Profile 259"+ PCL's -- that is, a PCL that targets at least .NET 4.5, Windows 8 and Windows Phone 8.

### Build Status
|Branch | Status|
|:-------|-------:|
| master |[![Build status](https://ci.appveyor.com/api/projects/status/6h5oj7x2ld4mi6at/branch/master?svg=true)](https://ci.appveyor.com/project/onovotny/referencegenerator/branch/master)|


## Usage
This tool uses some conventions to locate your `nuspec` file and input libraries. These can be overridden in your project file. The tool looks for a `.nuspec` file with the same name as your target library underneath the solution root directory. By default, it will add/update a `<dependencies>` group for the `dotnet` TFM, but you can have it generate others by overriding your project file value.

Using NuGet, add `NuSpec.ReferenceGenerator` to your library project. On build, it will add/update your nuspec with the correct dependency data for your libraries.

If you have existing package dependencies in your nuspec in the group that aren't picked up by this tool, they'll be silently ignored. This could happen in the case where a HintPath to a Package is missing and the package could not be detected.

When you author your nuspec package, make sure that your library goes into the `\lib\netstandard1.x` directory, where x depends on your library. PCL 259's are all `netstandard1.0`.

## Packages containing `netstandard` and existing libraries
If you have a package that contains a `netstandard` group and your users use `packages.config`, you may want to add blank dependency groups. Note that Xamarin Studio 6.1, currently in the alpha channel, supports `project.json` and that is the recommended solution.

```xml
<group targetFramework="net45" />
<group targetFramework="wp8" />
<group targetFramework="win8" />
<group targetFramework="wpa81" />
<group targetFramework="xamarin.ios" />
<group targetFramework="monotouch" />
<group targetFramework="monoandroid" />
```
Depending on the minimum platform versions you target and the minimum platforms supported by your `netstandard1.x` dependencies. NuGet will evaluate `netstandard1.x` for any "System.Runtime" based platform, so that effectively means, `net45`, `wp8`, `win8`, `wpa81`, `xamarin.ios`, `monotouch`, and `monoandroid`. Those platforms support System.Runtime 4.0.0. If you target a newer set of platforms, like `net451`, `Win81` and `wpa81` (Profile 151), then it's System.Runtime is 4.0.10.

For example, if you're putting a Profile 151 library in `netstandard1.1`, then your System.Runtime is 4.0.10 and will run on .NET 4.5.1 and higher. For older platforms like .NET 4.5, you'll need to add a blank group
```xml
<group targetFramework="net45" />
```
to ensure that those older platforms don't try to add references to the newer dependencies specified in your `netstandard` section.

To sum this up, look at the output of the tool for the `netstandard` section. If you have a System.Runtime higher than 4.0.0, and you want to to target `net45`, `wp8`, `win8`,  `xamarin.ios`, `monotouch`, or `monoandroid`, then you need to block the `netstandard` dependency group by adding blank dependency groups for the other platforms.

## Options and overriding default behavior

**NuSpec Library Content**
The library files that should be checked for dependencies. Most packages should have a single assembly which the tool will detect. If you have more than one file packaged in your nupkg, then you need to to specify the following in your csproj/vbproj file. You'll also need to specify the project file for it in the next section:
```xml
<ItemGroup>
  <!-- output of this project -->
  <NuSpecLibContent Include="$(TargetPath)">
    <Visible>False</Visible>
  </NuSpecLibContent>

  <!-- another library we're distributing in the same nupkg -->
  <NuSpecLibContent Include="$(TargetDir)AnotherLibrary.dll">
    <Visible>False</Visible>
  </NuSpecLibContent>
</ItemGroup>
```

**NuSpec Project Files**
The library files that should be checked for dependencies. Most packages should have a single assembly which the tool will detect. If you have more than one file packaged in your nupkg, then you need to to specify the following in your csproj/vbproj file:
```xml
<ItemGroup>
  <!-- this project -->
  <NuSpecProjectFile Include="$(MSBuildThisFileFullPath)">
    <Visible>False</Visible>
  </NuSpecProjectFile>

  <!-- another library we're distributing in the same nupkg -->
  <!-- Note: Order matters here; use the same order as for NuSpecLibContent -->
  <NuSpecProjectFile Include="$(SolutionDir)AnotherLibrary\AnotherLibrary.csproj">
    <Visible>False</Visible>
  </NuSpecProjectFile>
</ItemGroup>
```

**NuSpec File**
By default, the tool will look for a .nuspec file with the same name as your library underneath your solution directory, recursively. If your .nuspec has a different filename, then you need to specify it in your csproj/vbproj file:
```xml
<ItemGroup>
  <!-- example NuSpec file that must be specified -->
  <NuSpecFile Include="$(SolutionDir)package\.nuspec">
    <Visible>False</Visible>
  </NuSpecFile>
</ItemGroup>
```

**Target Frameworks**
By default, the tool will add/update a dependency group for the `netstandard1.x` TFM for a PCL or `uap10.0` for a UWP Class Library.
In some cases, you may need to have multiple dependency groups, like having both `netstandard1.x` and `uap10.0`. An example of this is if your package includes a `win8` or `win81` library but you'd like the .NET Core-based one to be used there. `netstandard1.x` isn't enough as `win81` is more specific and would "win." Instead, just copy your `netstandard1.x` library to also be under `\lib\uap10.0` and specify an additional TFM for the tool to add/update. This should be a semi-colon joined list.
```xml
<PropertyGroup>
<!-- netstandard1.x and uap10.0 tfms -->
  <NuSpecTfm>auto;uap10.0</NuSpecTfm>
</PropertyGroup>
```

## Command line (2.0)
This tool is a command line that you can call in other ways. The command line exposes additional features targeted at NuGet packages containing cross-compiled libraries. The general syntax for the command line is: `RefGen.exe` `command` `options`. All of the options are required for each command but the order doesn't matter.

There are two commands

| Command | Description |
|---------|-------------|
| generate-single | Single library wtih a fixed set of functionality (eg. PCL 259 or `netstandard1.0`). Usable with `csproj`/`vbproj`-based projects that use either `packages.config` or `project.json` for their packages |
| generate-cross | Library that's cross-compiled enabling more functionality on newer platforms (eg. `netstandard1.0` and `netstandard1.3`). Only usable with `xproj`-based project that use `project.json` for cross-compiling to several target frameworks |

### generate-single options
This is the same functionality that RefGen 1.0 had, extended with support for .NET Standard.

| Switch | Description |
| ----   | ---- |
| -m or --moniker | NuGetTargetMonikers -- .NETStandard,Version=v1.4 |
| -t or --tfm | TFM's to generate, semi-colon joined. E.g.: auto;uap10.0 |
| -n or --nuspec | Full path to NuSpec file |
| -p or --project | Full path to project file (csproj/vbproj) |
| -f or --file | Full path target files, semi-colon joined |

### generate-cross options
This functionality is new in version 2.0 and enables the use of xproj/project.json-based cross-compiling projects. It requires use a NuSpec file as calling `dotnet pack project.json` doesn't provide a way of trimming the dependencies list.

In this model, you'd typically have a `project.json` dependency on `NETStandard.Library`. The issue is that when creating your NuPkg, it's better to list the individual dependencies you require instead of a giant meta-package. This RefGen tool enables this scenario.

| Switch | Description |
| ---- | --- |
| -p or --project | Full path to project file (`project.json`) |
| -d or --directory | Path to base directory where the output folders are created per target framework). The output would be in `<directory>\netstandard1.0\MyLib.dll` for example. |
| -n or --nuspec | Full path to the nuspec file |
| -l or --library | Library name, including .dll. `MyLibrary.dll`, for example |  

### Upgrading from 1.x
The 2.0 version will detect/use the 1.x command line options. Updating should not break your scripts.

## Command line (1.x)
This tool is a command line that you can call in other ways. The parameters are as follows and they are all required:

```
// args 0: NuGetTargetMoniker: .NETPlatform,Version=v5.0
// args 1: TFM's to generate, semi-colon joined. E.g.: auto;uap10.0
// args 2: nuspec file, full path
// args 3: project file (csproj/vbproj, etc) full path. Used to look for packages.config/project.json and references. should match order of target files
// args 4: target files, semi-colon joined, full path
```

## Limitations
- This tool does not currently run on mono if you're using an "classic PCL". The tool needs all of the PCL contracts from the `Reference Assemblies` folder for comparison; if there's an equiv on Mono, then this could be fixed. Alternatively, if you only need project.json based projects, then there's no limitation.

## Changelog
- 2.0.0-beta-bld*xx*: Support for cross-compiled xproj. Updated command line options
- 2.0.0-beta1: Initial support for `netstandard` and .NET Core RC2
- 1.4.2: Add a more descriptive error message for NuSpec files that have incorrect XML namespace declarations
- 1.4.1: Issue warning and do not run RefGen on non-Windows systems until full mono compatibility is tested and verified. Prevents breaking builds.
- 1.4: Fix issue where BCL libs weren't detected correctly for PCL projects using project.json instead of packages.config
- 1.3.6: Fix TFM version parsing when running on cultures that use `,` instead of `.` ([#14](https://github.com/onovotny/ReferenceGenerator/issues/14))
- 1.3.5: Fix for pre-release number parsing so that it can work with a "number" group that is larger than `int.MaxValue` ([#13](https://github.com/onovotny/ReferenceGenerator/pull/13))
- 1.3.4: Set `developmentDependency=true` to prevent `NuSpec.ReferenceGenerator` from being seen a runtime dependency
- 1.3.3: Add backslash to NuSpecFile search path when SolutionDir isn't defined, don't define SolutionDir if it's not already defined
- 1.3.2: Check for blank `$(SolutionDir)` and default to parent directory of project
- 1.3.1: Set default TFM for UWP class libraries to `uap10.0`
- 1.3: Fix bug when using with UWP class libraries. Also include support for *projectName*.project.json added in NuGet 3.2.
- 1.2: Prevent included items (nuspec, project files, libraries) from showing up in Visual Studio. Changed NuSpecTfm to a `PropertyGroup` from an `ItemGroup`.
- If you were setting `NuSpecTfm` you'll need to update your settings to use `PropertyGroup` for that item.
- 1.0.1: Add `Portable-` to ensure the package works for `packages.config` based projects.
- 1.0: Initial Release

## Support
If you find any issues, please open an issue in the tracker and/or ping me ([@onovotny](https://twitter.com/onovotny)) on Twitter and I'll try to help.
