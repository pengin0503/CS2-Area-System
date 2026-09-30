param([switch]$ApiCheck)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & "$PSScriptRoot/verify-source.ps1"
    & "$PSScriptRoot/test-source-guards.ps1"
    & "$PSScriptRoot/test-build-contract.ps1"
    $buildArgs = @('-c', 'Debug')
    if ($ApiCheck) { $buildArgs += '-p:AreaApiCheck=true' }
    & dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj @buildArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj @buildArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (Test-Path UI/package.json) {
        Push-Location UI
        try {
            & npm test -- --run
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
            & npm run build
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        } finally { Pop-Location }
    }
} finally { Pop-Location }
