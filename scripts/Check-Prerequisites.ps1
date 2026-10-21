$ErrorActionPreference = 'Stop'
Write-Host 'Node.js:'
& node --version
Write-Host '.NET SDKs:'
& dotnet --list-sdks
Write-Host 'Revit API installations:'
foreach ($year in '2024','2025') {
    [pscustomobject]@{ Revit = $year; API = Test-Path "C:/Program Files/Autodesk/Revit $year/RevitAPI.dll"; UI = Test-Path "C:/Program Files/Autodesk/Revit $year/RevitAPIUI.dll" }
}
Write-Host '.NET Framework 4.8 targeting pack:'
Test-Path 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
