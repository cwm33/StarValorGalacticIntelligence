$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
npm --prefix frontend ci
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
npm --prefix frontend run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host 'Open http://127.0.0.1:5187 in your browser. Press Ctrl+C here to stop.'
dotnet run --project src/Api --no-launch-profile
exit $LASTEXITCODE
