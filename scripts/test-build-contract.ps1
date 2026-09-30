$ErrorActionPreference='Stop'
. "$PSScriptRoot/msbuild-json.ps1"
# Suppress first-run/logo output; extraction below still tolerates any diagnostics that remain.
$env:DOTNET_NOLOGO='1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$project=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/CS2AreaSystem/CS2AreaSystem.csproj'
$native=& dotnet msbuild $project -nologo -target:BuildGetFullPaths -getProperty:NeedBuild,DeployDir
if($LASTEXITCODE -ne 0){throw 'Official target initialization failed.'}
$paths=ConvertFrom-MSBuildPropertyOutput $native
if($paths.NeedBuild -ne 'true' -or [string]::IsNullOrWhiteSpace($paths.DeployDir)) {throw "Official build must initialize postprocessing/deployment: NeedBuild=$($paths.NeedBuild), DeployDir=$($paths.DeployDir)"}
$api=& dotnet msbuild $project -nologo -target:BuildAreaApiCheckPaths -p:AreaApiCheck=true -getProperty:NeedBuild,DeployDir
if($LASTEXITCODE -ne 0){throw 'API profile target initialization failed.'}
# The API profile emits its explicit non-playable message before JSON.
$paths=ConvertFrom-MSBuildPropertyOutput $api
if($paths.NeedBuild -ne 'false' -or ![string]::IsNullOrWhiteSpace($paths.DeployDir)) {throw 'API-check must not configure deployment.'}
Write-Host 'Official/API profile target-evaluation contracts passed.'
