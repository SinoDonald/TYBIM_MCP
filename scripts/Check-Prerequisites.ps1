param([ValidateSet('2024','2025','2026')][string[]]$RevitVersion=@('2024','2025','2026'))
$ErrorActionPreference = 'Stop'
$root=Split-Path $PSScriptRoot -Parent
Write-Host 'Node.js:'
& node --version
Write-Host '.NET SDKs:'
& dotnet --list-sdks
Write-Host 'Revit API installations:'
foreach ($year in $RevitVersion) {
    $installed="C:/Program Files/Autodesk/Revit $year"
    $reference=if(Test-Path -LiteralPath "$installed/RevitAPI.dll"){$installed}else{Join-Path $root "artifacts/references/$year"}
    [pscustomobject]@{ Revit=$year;Installed=(Test-Path -LiteralPath "$installed/RevitAPI.dll");ReferenceDirectory=$reference
        API=(Test-Path -LiteralPath (Join-Path $reference 'RevitAPI.dll'));UI=(Test-Path -LiteralPath (Join-Path $reference 'RevitAPIUI.dll')) }
}
Write-Host '.NET Framework 4.8 targeting pack:'
Test-Path 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
