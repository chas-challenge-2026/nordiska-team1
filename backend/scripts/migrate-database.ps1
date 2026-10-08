# PowerShell script to apply EF Core migrations across all Nordiska modules
param(
    [Parameter(Position=0)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"

if (-not $ConnectionString) {
    $ConnectionString = $env:MIGRATION_CONNECTION_STRING
}
if (-not $ConnectionString) {
    $ConnectionString = $env:ConnectionStrings__MigrationDatabase
}

if (-not $ConnectionString) {
    Write-Error "Migration connection string is required. Provide it as parameter or set `$env:MIGRATION_CONNECTION_STRING."
    exit 1
}

$env:MIGRATION_CONNECTION_STRING = $ConnectionString

$backendDir = Split-Path -Parent $PSScriptRoot
Push-Location $backendDir

try {
    Write-Host "Restoring dotnet-ef tool..."
    dotnet tool restore

    $modules = @("Banking", "Inbox", "Faq", "Reporting")

    foreach ($module in $modules) {
        Write-Host "==========================================" -ForegroundColor Cyan
        Write-Host "Applying migrations for: $module" -ForegroundColor Cyan
        Write-Host "==========================================" -ForegroundColor Cyan

        dotnet tool run dotnet-ef database update `
            --project "src/Modules/$module/Nordiska.Modules.$module.csproj" `
            --startup-project "src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj" `
            --context "${module}DbContext"

        Write-Host "Successfully migrated $module." -ForegroundColor Green
    }

    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "All module database migrations applied successfully!" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
}
finally {
    Pop-Location
}