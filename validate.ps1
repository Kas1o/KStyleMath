$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @(
    "corpus/goldens.json",
    "corpus/extra.json",
    "corpus/variants.json",
    "corpus/commands.json",
    "corpus/macros.json",
    "corpus/api.json"
)
$failed = $false
foreach ($file in $files) {
    Write-Host "== $file ==" -ForegroundColor Cyan
    & dotnet run --project "$root/src/KStyleMath.Cli" -c Release --no-build -- "$root/$file"
    if ($LASTEXITCODE -ne 0) { $failed = $true }
}
if ($failed) {
    Write-Host "Validation FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "All corpus files passed." -ForegroundColor Green
