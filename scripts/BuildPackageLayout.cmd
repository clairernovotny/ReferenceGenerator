if not exist "%~dp0..\artifacts\build\dotnet" mkdir "%~dp0..\artifacts\build\dotnet"
if not exist "%~dp0..\artifacts\tools" mkdir "%~dp0..\artifacts\tools"

"%~dp0..\packages\ilmerge.2.14.1208\tools\ilmerge.exe" "%~dp0..\src\ReferenceGenerator\bin\%1\ReferenceGenerator.exe" "%~dp0..\src\ReferenceGenerator\bin\%1\System.Collections.Immutable.dll" "%~dp0..\src\ReferenceGenerator\bin\%1\System.Reflection.Metadata.dll" "%~dp0..\src\ReferenceGenerator\bin\%1\Newtonsoft.Json.dll" /out:"%~dp0..\artifacts\tools\RefGen.exe"  /internalize   /targetplatform:"v4,C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5" 
copy "%~dp0..\src\ReferenceGenerator\bin\%1\ReferenceGenerator.exe.config" "%~dp0..\artifacts\tools\RefGen.exe.config" /y
copy "%~dp0..\src\ReferenceGenerator\NuSpec.ReferenceGenerator.targets" "%~dp0..\artifacts\build\dotnet\NuSpec.ReferenceGenerator.targets" /y