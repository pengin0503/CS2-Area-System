$ErrorActionPreference='Stop'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('area-core-guards-'+[guid]::NewGuid())
New-Item -ItemType Directory -Path "$fixture/src","$fixture/UI/src" | Out-Null
try {
 function Verify-Fixture([bool]$reject,[string]$message) {
  $output=& "$PSHOME/pwsh" -NoProfile -File "$PSScriptRoot/verify-source.ps1" -Root $fixture 2>&1 | Out-String
  $code=$LASTEXITCODE
  if (($reject -and ($code -eq 0 -or !$output.Contains($message))) -or (!$reject -and $code -ne 0)) {throw "Source guard expectation failed: code=$code output=$output"}
 }
 Set-Content "$fixture/src/Safe.cs" 'class Safe {}'
 Verify-Fixture $false ''
 foreach($forbidden in @('HarmonyLib','0Harmony','patch of shipped index.js','patch of shipped index.css')) {
  Set-Content "$fixture/src/Bad.cs" $forbidden
  Verify-Fixture $true 'Forbidden production dependency or shipped UI mutation'
 }
 Remove-Item "$fixture/src/Bad.cs"
 Set-Content "$fixture/src/Adapted.cs" '// Adapted from example'
 Verify-Fixture $true 'THIRD_PARTY.md'
 Set-Content "$fixture/THIRD_PARTY.md" 'Provenance recorded'
 Verify-Fixture $false ''
 Write-Host 'Source guard tests passed (clean, four forbidden patterns, provenance missing/present).'
} finally {Remove-Item -Recurse -Force $fixture}
