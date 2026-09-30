# Extracts the structured result of `dotnet msbuild -getProperty:A,B` from stdout.
# stdout may start with diagnostics such as the .NET first-run banner or project messages,
# so the JSON object is located instead of assuming stdout is pure JSON.
function ConvertFrom-MSBuildPropertyOutput {
 param([AllowNull()][AllowEmptyCollection()][object[]]$Output)
 $lines=@(($Output|Out-String) -split "\r?\n")
 for($i=0;$i -lt $lines.Count;$i++) {
  if($lines[$i] -ne '{') {continue}
  $candidate=($lines[$i..($lines.Count-1)] -join "`n")
  try {$parsed=$candidate|ConvertFrom-Json -ErrorAction Stop} catch {continue}
  if($null -ne $parsed.Properties) {return $parsed.Properties}
 }
 throw 'MSBuild property output did not contain a JSON object with Properties.'
}
