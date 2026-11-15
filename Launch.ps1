$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
npm --prefix frontend ci
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
npm --prefix frontend run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$url = 'http://127.0.0.1:5187'
Write-Host "Starting Galactic Intelligence at $url. Press Ctrl+C here to stop."
$browser = Start-Job -ArgumentList $url -ScriptBlock {
    param($url)
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri "$url/api/resolver" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        }
        catch {
            Start-Sleep -Milliseconds 500
            continue
        }
        if ($response.StatusCode -eq 200) {
            Start-Process $url
            return
        }
    }
}

try {
    dotnet run --project src/Api --no-launch-profile
    exit $LASTEXITCODE
}
finally {
    Remove-Job -Job $browser -Force
}
