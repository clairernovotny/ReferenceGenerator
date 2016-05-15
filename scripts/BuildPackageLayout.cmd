if not exist "%~dp0..\artifacts\build\netstandard1.0" mkdir "%~dp0..\artifacts\build\netstandard1.0"
if not exist "%~dp0..\artifacts\build\portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10" mkdir "%~dp0..\artifacts\build\portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10"
if not exist "%~dp0..\artifacts\tools" mkdir "%~dp0..\artifacts\tools"

@rem build command param
@echo off
setlocal 

for /f %%f in ('dir /b %~dp0..\src\ReferenceGenerator\bin\%1\*.dll') do call :concat %~dp0..\src\ReferenceGenerator\bin\%1\%%f

"%HOMEDRIVE%%HOMEPATH%\.nuget\packages\ILMerge\2.14.1208\tools\ilmerge.exe" "%~dp0..\src\ReferenceGenerator\bin\%1\ReferenceGenerator.exe" %a% /out:"%~dp0..\artifacts\tools\RefGen.exe"  /internalize   /targetplatform:"v4,C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5" 
copy "%~dp0..\src\ReferenceGenerator\bin\%1\ReferenceGenerator.exe.config" "%~dp0..\artifacts\tools\RefGen.exe.config" /y
copy "%~dp0..\src\ReferenceGenerator\NuSpec.ReferenceGenerator.targets" "%~dp0..\artifacts\build\netstandard1.0\NuSpec.ReferenceGenerator.targets" /y
copy "%~dp0..\src\ReferenceGenerator\NuSpec.ReferenceGenerator.targets" "%~dp0..\artifacts\build\portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10\NuSpec.ReferenceGenerator.targets" /y

goto :eof

:concat
set a=%a% %1
goto :eof