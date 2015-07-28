# NuSpec Reference Generator
## Overview
Package authoring for .NET Core based libraries (ASPNet 5, DNX, UWP) has an extra burden on the author as .NET dependencies must be listed in addition to any regular packages you depend on. This could be a long list and it's a challenge to get it right. If you use any of the meta-packages that brings "all" of .NET Core into your project as possible references, how do you know which you actually need?

This tool aims to help by reading your compiled libraries assembly metadata and determine what that list should be. It currently supports any `System.Runtime` based project, including "Profile 259"+ PCL's -- that is, a PCL that targets at least .NET 4.5, Windows 8 and Windows Phone 8.

## Usage
This tool uses some conventions to locate your `nuspec` file and input libraries. These can be overridden in your project file. The tool looks for a `.nuspec` file with the same name as your target library underneath the solution root directory. By default, it will add/update a `<dependencies>` group for the `dotnet` TFM, but you can have it generate others by overriding your project file value.

Using NuGet, add `NuSpec.ReferenceGenerator` to your library project. On build, it will add/update your nuspec with the correct dependency data for your libraries.

## Limitations
- This tool does not currently run on mono if you're using an "classic PCL". The tool needs all of the PCL contracts from the `Reference Assemblies` folder for comparison; if there's an equiv on Mono, then this could be fixed. Alternatively, if you only need project.json based projects, then there's no limitation.

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

## Support
If you find any issues, please open an issue in the tracker and/or ping me ([@onovotny](https://twitter.com/onovotny)) on Twitter and I'll try to help.