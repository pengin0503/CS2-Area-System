$ErrorActionPreference='Stop'
$project=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/CS2AreaSystem/CS2AreaSystem.csproj'
$native=& dotnet msbuild $project -nologo -target:BuildGetFullPaths -getProperty:NeedBuild,DeployDir
if($LASTEXITCODE -ne 0){throw 'Official target initialization failed.'}
$paths=($native|Out-String|ConvertFrom-Json).Properties
if($paths.NeedBuild -ne 'true' -or [string]::IsNullOrWhiteSpace($paths.DeployDir)) {throw "Official build must initialize postprocessing/deployment: NeedBuild=$($paths.NeedBuild), DeployDir=$($paths.DeployDir)"}
$api=& dotnet msbuild $project -nologo -target:BuildAreaApiCheckPaths -p:AreaApiCheck=true -getProperty:NeedBuild,DeployDir
if($LASTEXITCODE -ne 0){throw 'API profile target initialization failed.'}
# The API profile emits its explicit non-playable message before JSON.
$json=($api|Out-String);$json=$json.Substring($json.IndexOf('{'))
$paths=($json|ConvertFrom-Json).Properties
if($paths.NeedBuild -ne 'false' -or ![string]::IsNullOrWhiteSpace($paths.DeployDir)) {throw 'API-check must not configure deployment.'}
Write-Host 'Official/API profile target-evaluation contracts passed.'
