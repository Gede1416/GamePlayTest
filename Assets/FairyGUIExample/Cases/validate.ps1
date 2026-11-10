$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scenes = Get-ChildItem $PSScriptRoot -Filter '*.unity'
if ($scenes.Count -ne 10) { throw 'Expected 10 scenes' }
$indices = @()
foreach ($scene in $scenes) {
    $text = Get-Content $scene.FullName -Raw
    $package = [regex]::Match($text, 'packageName: (\S+)').Groups[1].Value
    $component = [regex]::Match($text, 'componentName: ([^\r\n]+)').Groups[1].Value
    $index = [int][regex]::Match($text, 'caseIndex: (\d+)').Groups[1].Value
    $indices += $index
    [xml]$manifest = Get-Content "$root/UIProject~/assets/$package/package.xml" -Raw
    $entry = $manifest.packageDescription.resources.component | Where-Object name -eq "$component.xml"
    if (!$entry -or $entry.exported -ne 'true') { throw "Missing exported component: $package/$component" }
    if (!(Test-Path "$root/Resources/UI/${package}_fui.bytes")) { throw "Missing published package: $package" }
    Write-Output "PASS $($scene.Name): $package/$component [$index]"
}
if (($indices | Sort-Object -Unique).Count -ne 10) { throw 'Duplicate scene indices' }
