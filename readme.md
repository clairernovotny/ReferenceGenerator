# NuSpec Reference Generator
## Overview
Package authoring for .NET Core based libraries (ASPNet 5, DNX, UWP) has an extra burden on the author as .NET dependencies must be listed in addition to any regular packages you depend on. This could be a long list and it's a challenge to get it right. If you use any of the meta-packages that brings "all" of .NET Core into your project as possible references, how do you know which you actually need?

This tool aims to help by reading your compiled libraries assembly metadata and determine what that list should be. It currently supports any `System.Runtime` based project, including "Profile 259"+ PCL's -- that is, a PCL that targets at least .NET 4.5, Windows 8 and Windows Phone 8.

## Usage
This tool uses some conventions to locate your `nuspec` file and input libraries. These can be overridden in your project file. The tool looks for a `.nuspec` file with the same name as your target library underneath the solution root directory. By default, it will add/update a `<dependencies>` group for the `dotnet` TFM, but you can have it generate others by overriding your project file value.

Using NuGet, add `NuSpec.ReferenceGenerator` to your library project. On build, it will add/update your nuspec with the correct dependency data for your libraries.

If you have existing package dependencies in your nuspec in the group that aren't picked up by this tool, they'll be silently ignored. This could happen in the case where a HintPath to a Package is missing and the package could not be detected. 

## Limitations
- This tool does not currently run on mono if you're using an "classic PCL". The tool needs all of the PCL contracts from the `Reference Assemblies` folder for comparison; if there's an equiv on Mono, then this could be fixed. Alternatively, if you only need project.json based projects, then there's no limitation.

- **DNX Core** The DNX Core uses the OSS .NET Core libraries, which are still in beta. The [roadmap](https://github.com/aspnet/Home/wiki/Roadmap) has the release in Q1 2016. Until then, packages that that want to target DNX Core need to add an extra dependencies section that targets their beta packages. No changes are required to your library, just your nuspec. Any Profile259+/System.Runtime PCL can run on DNX, you do NOT have to create a kproj and compile specially for it. This tool will generate a dnxcore5 dependency group by default by guessing the BCL libraries as there's no foolproof way to detect them by looking at assembly references. (The package is in beta, the assembly ref has a version).


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
	<NuSpecLibContent Include="$(SolutionDir)package\.nuspec" /> 
</ItemGroup>
```

**Target Frameworks**
By default, the tool will add/update a dependency group for the `dotnet` TFM. In some cases, you may need to also include another one like `uap10.0`. An example of this is if your package includes a `win8` or `win81` library but you'd like the .NET Core-based one to be used there. `dotnet` isn't enough as `win81` is more specific and would "win." Instead, just copy your `dotnet` library to also be under `\lib\uap10.0` and specify an additional TFM for the tool to add/update.
```xml
<ItemGroup>
	<!-- dotnet tfm -->
	<NuSpecTfm Include="dotnet" /> 
	
	<!-- uap10.0 tfm -->
	<NuSpecTfm Include="uap10.0" />
</ItemGroup>
```
**DNXCore 5**
To overcome the current limitation where DNX needs to use its -beta-tagged BCL libraries, by default this tool will emit its best guess of which assemblies are -beta and mark them accordingly. This is a HACK as the tool is assuming that System.* libs are -beta (along with a couple select Microsoft ones). If you wish to disable the generation of the dnxcore5 section due to bad guessing, set the following properties in your project. Also, you can control the prerelease tag used so you can match DNX updates by setting `NuSpecDnxCoreTag` property.
```xml
<PropertyGroup>
	<NuSpecIncludeDnxCore>False</NuSpecIncludeDnxCore>
	<NuSpecDnxCoreTag>-beta-23109</NuSpecDnxCoreTag>
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
// args 5: generate dnxcore5 workaround: True/False
// args 6: dnx core beta tag: -beta-23109
```

## Support
If you find any issues, please open an issue in the tracker and/or ping me ([@onovotny](https://twitter.com/onovotny)) on Twitter and I'll try to help.