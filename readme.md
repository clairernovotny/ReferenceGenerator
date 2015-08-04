# NuSpec Reference Generator
## Overview
Package authoring for .NET Core based libraries (ASPNet 5, DNX, UWP) has an extra burden on the author as .NET dependencies must be listed in addition to any regular packages you depend on. This could be a long list and it's a challenge to get it right. If you use any of the meta-packages that brings "all" of .NET Core into your project as possible references, how do you know which you actually need?

This tool aims to help by reading your compiled libraries assembly metadata and determine what that list should be. It currently supports any `System.Runtime` based project, including "Profile 259"+ PCL's -- that is, a PCL that targets at least .NET 4.5, Windows 8 and Windows Phone 8.

## Usage
This tool uses some conventions to locate your `nuspec` file and input libraries. These can be overridden in your project file. The tool looks for a `.nuspec` file with the same name as your target library underneath the solution root directory. By default, it will add/update a `<dependencies>` group for the `dotnet` TFM, but you can have it generate others by overriding your project file value.

Using NuGet, add `NuSpec.ReferenceGenerator` to your library project. On build, it will add/update your nuspec with the correct dependency data for your libraries.

If you have existing package dependencies in your nuspec in the group that aren't picked up by this tool, they'll be silently ignored. This could happen in the case where a HintPath to a Package is missing and the package could not be detected. 

When you author your nuspec package, make sure that your library goes into the `\lib\dotnet` directory.

## Packages containing `dotnet` and existing libraries
If you have a package that contains a `dotnet` group and targets both new and old platforms, you might need an extra step in your nuspec. This depends on what packages you actually reference in your dotnet section. You might need to add some or all of the following:
```xml
<group targetFramework="net45" />
<group targetFramework="wp8" />
<group targetFramework="win8" />
<group targetFramework="wpa81" />
<group targetFramework="xamarin.ios" />
<group targetFramework="monotouch" />
<group targetFramework="monoandroid" />
```
Depending on the minimum platform versions you target and the minimum platforms supported by your `dotnet` dependencies. NuGet will evaluate `dotnet` for any "System.Runtime" based platform, so that effectively means, `net45`, `wp8`, `win8`, `wpa81`, `xamarin.ios`, `monotouch`, and `monoandroid`. Those platforms support System.Runtime 4.0.0. If you target a newer set of platforms, like `net451`, `Win81` and `wpa81` (Profile 151), then it's System.Runtime is 4.0.10.

For example, if you're putting a Profile 151 library in `dotnet`, then your System.Runtime is 4.0.10 and will run on .NET 4.5.1 and higher. For older platforms like .NET 4.5, you'll need to add a blank group 
```xml
<group targetFramework="net45" />
```
to ensure that those older platforms don't try to add references to the newer dependencies specified in your `dotnet` section.

To sum this up, look at the output of the tool for the `dotnet` section. If you have a System.Runtime higher than 4.0.0, and you want to to target `net45`, `wp8`, `win8`,  `xamarin.ios`, `monotouch`, or `monoandroid`, then you need to block the `dotnet` dependency group by adding blank dependency groups for the other platforms.

## Options and overriding default behavior

**NuSpec Library Content** 
The library files that should be checked for dependencies. Most packages should have a single assembly which the tool will detect. If you have more than one file packaged in your nupkg, then you need to to specify the following in your csproj/vbproj file. You'll also need to specify the project file for it in the next section:
```xml
<ItemGroup>
	<!-- output of this project -->
	<NuSpecLibContent Include="$(TargetPath)" /> 
	
	<!-- another library we're distributing in the same nupkg -->
	<NuSpecLibContent Include="$(TargetDir)AnotherLibrary.dll" />
</ItemGroup>
```

**NuSpec Project Files** 
The library files that should be checked for dependencies. Most packages should have a single assembly which the tool will detect. If you have more than one file packaged in your nupkg, then you need to to specify the following in your csproj/vbproj file:
```xml
<ItemGroup>
	<!-- this project -->
	<NuSpecProjectFile Include="$(MSBuildThisFileFullPath)" /> 
	
	<!-- another library we're distributing in the same nupkg -->
	<!-- Note: Order matters here; use the same order as for NuSpecLibContent --> 
	<NuSpecProjectFile Include="$(SolutionDir)AnotherLibrary\AnotherLibrary.csproj" />
</ItemGroup>
```

**NuSpec File**
By default, the tool will look for a .nuspec file with the same name as your library underneath your solution directory, recursively. If your .nuspec has a different filename, then you need to specify it in your csproj/vbproj file:
```xml
<ItemGroup>
	<!-- example NuSpec file that must be specified -->
	<NuSpecFile Include="$(SolutionDir)package\.nuspec" /> 
</ItemGroup>
```

**Target Frameworks**
By default, the tool will add/update a dependency group for the `dotnet` TFM. In some cases, you may need to also include another one like `uap10.0`. An example of this is if your package includes a `win8` or `win81` library but you'd like the .NET Core-based one to be used there. `dotnet` isn't enough as `win81` is more specific and would "win." Instead, just copy your `dotnet` library to also be under `\lib\uap10.0` and specify an additional TFM for the tool to add/update. This should be a semi-colon joined list.
```xml
<PropertyGroup>
	<!-- dotnet and uap10.0 tfms -->
	<NuSpecTfm>dotnet;uap10.0</NuSpecTfm>
</PropertyGroup>
```

## Command line
This tool is a command line that you can call in other ways. The parameters are as follows and they are all required:

```
// args 0: NuGetTargetMoniker: .NETPlatform,Version=v5.0  
// args 1: TFM's to generate, semi-colon joined. E.g.: dotnet;uap10.0 
// args 2: nuspec file, full path
// args 3: project file (csproj/vbproj, etc) full path. Used to look for packages.config/project.json and references. should match order of target files
// args 4: target files, semi-colon joined, full path
```

## Limitations
- This tool does not currently run on mono if you're using an "classic PCL". The tool needs all of the PCL contracts from the `Reference Assemblies` folder for comparison; if there's an equiv on Mono, then this could be fixed. Alternatively, if you only need project.json based projects, then there's no limitation.

## Changelog
- 1.1: Prevent included items (nuspec, project files, libraries) from showing up in Visual Studio. Changed NuSpecTfm to a `PropertyGroup` from an `ItemGroup`.
    - If you were setting `NuSpecTfm` you'll need to update your settings to use `PropertyGroup` for that item.
- 1.0.1: Add `Portable-` to ensure the package works for `packages.config` based projects.
- 1.0: Initial Release

## Support
If you find any issues, please open an issue in the tracker and/or ping me ([@onovotny](https://twitter.com/onovotny)) on Twitter and I'll try to help.