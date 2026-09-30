$ErrorActionPreference='Stop'
. "$PSScriptRoot/msbuild-json.ps1"
$json=@('{','  "Properties": {','    "NeedBuild": "true",','    "DeployDir": "/mods/CS2AreaSystem"','  }','}')
$banner=@('','Welcome to .NET 8.0!','---------------------','SDK Version: 8.0.131','','Telemetry','---------','Write your first app: https://aka.ms/dotnet-hello-world','----------------','')
function Assert-Parsed($output,$label) {
 $p=ConvertFrom-MSBuildPropertyOutput $output
 if($p.NeedBuild -ne 'true' -or $p.DeployDir -ne '/mods/CS2AreaSystem') {throw "$label parsed incorrectly: $($p|ConvertTo-Json -Compress)"}
}
function Assert-Rejected($output,$label) {
 try {ConvertFrom-MSBuildPropertyOutput $output|Out-Null} catch {return}
 throw "$label was accepted without a Properties JSON object"
}
Assert-Parsed $json 'plain JSON'
Assert-Parsed ($banner+$json) 'first-run banner before JSON'
Assert-Parsed (@('  API-check profile: compiles against supplied game DLLs.')+$json) 'project message before JSON'
Assert-Parsed (@('{ not json','{')+$banner+$json) 'brace-prefixed diagnostics before JSON'
Assert-Rejected $banner 'banner without JSON'
Assert-Rejected @() 'empty output'
Assert-Rejected @('{','  "Other": {}','}') 'JSON without Properties'
Write-Host 'MSBuild JSON extraction tests passed (plain, banner, message, brace noise, three rejections).'
