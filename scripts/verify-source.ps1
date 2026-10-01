param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$production=@(Get-ChildItem "$Root/src","$Root/UI/src" -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.ts','.tsx','.scss' -and $_.FullName -notmatch '[\\/](bin|obj|node_modules|build)[\\/]' })
$adapted=$false
foreach($file in $production) {
 $source=Get-Content $file.FullName -Raw
 if($source -match '0Harmony|HarmonyLib|patch of shipped index\.(js|css)|(?:WriteAllText|writeFile|Copy-Item)[^\r\n]*(?:gameui|Cities2_Data|StreamingAssets)[^\r\n]*index\.(?:js|css)') {
  throw "Forbidden production dependency or shipped UI mutation: $($file.FullName)"
 }
 if($source -match 'Adapted from|Copied from') {$adapted=$true}
}
# Packaging config may also include adaptations.
if(Test-Path "$Root/UI/webpack.config.js") {if((Get-Content "$Root/UI/webpack.config.js" -Raw) -match 'Adapted from|Copied from') {$adapted=$true}}
if(Test-Path "$Root/UI/types") {
 foreach($file in Get-ChildItem "$Root/UI/types" -Recurse -File -Filter '*.ts') {
  if((Get-Content $file.FullName -Raw) -match 'Adapted from|Copied from') {$adapted=$true}
 }
}
if($adapted -and !(Test-Path "$Root/THIRD_PARTY.md")) {throw 'THIRD_PARTY.md required for adapted/copied production source.'}
Write-Host 'Area Core source/policy guards passed.'
