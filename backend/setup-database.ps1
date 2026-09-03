$ErrorActionPreference = "Stop"

$apiProject = "src\Nordiska.FrontendApi/Nordiska.FrontendApi.csproj"

Write-Host "Setting up Nordiska development database..."



if (Test-Path ".env") {
    Write-Host ""
    Write-Host ".env already exists."
    Write-Host "Setup aborted to avoid replacing existing database credentials."
    exit 1
}


$passwordBytes =
    [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)

$password =
    [Convert]::ToHexString($passwordBytes)



@"
POSTGRES_PASSWORD=$password
"@ | Set-Content ".env"

Write-Host "Created local .env"



$connectionString =
    "Host=127.0.0.1;Port=5432;Database=nordiska;Username=postgres;Password=$password"



dotnet user-secrets set `
    "ConnectionStrings:BankingDatabase" `
    $connectionString `
    --project $apiProject

dotnet user-secrets set `
    "ConnectionStrings:ReportingDatabase" `
    $connectionString `
    --project $apiProject

dotnet user-secrets set `
    "ConnectionStrings:FaqDatabase" `
    $connectionString `
    --project $apiProject

Write-Host "Configured ASP.NET Core User Secrets"


dotnet tool restore
dotnet restore



docker compose up -d

Write-Host "PostgreSQL started"


dotnet ef database update `
    --project src/Modules/Banking/Nordiska.Modules.Banking.csproj `
    --startup-project $apiProject `
    --context BankingDbContext



dotnet ef database update `
    --project src/Modules/Reporting/Nordiska.Modules.Reporting.csproj `
    --startup-project $apiProject `
    --context ReportDbContext



dotnet ef database update `
    --project src/Modules/Faq/Nordiska.Modules.Faq.csproj `
    --startup-project $apiProject `
    --context FaqDbContext


dotnet build


$password = $null
$connectionString = $null
$passwordBytes = $null

Write-Host ""
Write-Host "Nordiska development environment is ready!"
Write-Host ""
Write-Host "PostgreSQL database: nordiska"
Write-Host "Banking migrations:   applied"
Write-Host "Reporting migrations: applied"
Write-Host "FAQ migrations:       applied"