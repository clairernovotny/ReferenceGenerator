Function Install-NuGet(
    [parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$nuGet)
{

    If (Test-Path $nuGet)
    {
        Write-Host "NuGet.exe is already installed."
        return
    }

    Write-Host "Installating NuGet.exe..."
    Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nuGet
    Write-Host "Successfully installed NuGet."
}

$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath

$NuGetDir = "$dir\..\.nuget"
$NuGetExe = "$NuGetDir\nuget.exe"
$SlnDir = "$dir\.."

if(!(Test-Path -Path $NugetDir))
{
  Write-Host "Creating dir $NuGetDir"
  mkdir $NuGetDir
}

Install-NuGet($NuGetExe)

msbuild ""$SlnDir\ReferenceGenerator.sln""  /t:Build /p:Configuration=Release 

& $NuGetExe pack ""$SlnDir\NuSpec.ReferenceGenerator.nuspec"" -basepath ""$SlnDir\artifacts""
