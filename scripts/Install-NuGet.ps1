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
    Invoke-WebRequest http://www.nuget.org/NuGet.exe -OutFile $nuGet
    Write-Host "Successfully installed NuGet."
}