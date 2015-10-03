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