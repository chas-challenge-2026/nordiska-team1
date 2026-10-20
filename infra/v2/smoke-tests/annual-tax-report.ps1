param(
    [string]$AccessToken,

    [long]$AccountId = 0,

    [string]$Email = "anna@example.com",

    [string]$Password = "password123",

    [int]$TaxYear = ((Get-Date).Year - 1),

    [string]$BaseUrl = "http://localhost:8080",

    [string]$OutputPath = "./annual-tax-report.pdf",

    [int]$MaximumWaitSeconds = 60
)

$ErrorActionPreference = "Stop"

$requestAuthentication = @{}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $loginBody = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json

    Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/auth/login" `
        -WebSession $session `
        -ContentType "application/json" `
        -Body $loginBody | Out-Null

    $requestAuthentication.WebSession = $session
}
else {
    $requestAuthentication.Headers = @{
        Authorization = "Bearer $AccessToken"
    }
}

if ($AccountId -eq 0) {
    $accounts = Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/api/accounts" `
        @requestAuthentication

    $firstAccount = @($accounts)[0]

    if ($null -eq $firstAccount) {
        throw "The authenticated customer has no account for the smoke test."
    }

    $AccountId = $firstAccount.id
}

$requestBody = @{
    accountId = $AccountId
    taxYear = $TaxYear
} | ConvertTo-Json

$accepted = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/api/reports/tax" `
    @requestAuthentication `
    -ContentType "application/json" `
    -Body $requestBody

$deadline = (Get-Date).AddSeconds($MaximumWaitSeconds)
$status = $null

do {
    $status = Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/api/reports/tax/jobs/$($accepted.jobId)" `
        @requestAuthentication

    if ($status.status -eq "Failed") {
        throw "Tax report job $($accepted.jobId) failed: $($status.error)"
    }

    if ($status.status -ne "Completed") {
        Start-Sleep -Seconds 1
    }
} while (
    $status.status -ne "Completed" -and
    (Get-Date) -lt $deadline
)

if ($status.status -ne "Completed") {
    throw "Tax report job $($accepted.jobId) did not complete within $MaximumWaitSeconds seconds."
}

Invoke-WebRequest `
    -Method Get `
    -Uri "$BaseUrl/api/reports/tax/$($accepted.taxReportId)/pdf" `
    @requestAuthentication `
    -OutFile $OutputPath

$headerBytes = Get-Content `
    -LiteralPath $OutputPath `
    -AsByteStream `
    -TotalCount 5

$pdfHeader = [Text.Encoding]::ASCII.GetString($headerBytes)

if ($pdfHeader -ne "%PDF-") {
    throw "Downloaded file is not a PDF. Header was '$pdfHeader'."
}

$resolvedOutput = (Resolve-Path -LiteralPath $OutputPath).Path

Write-Host "Tax report job $($accepted.jobId) completed."
Write-Host "PDF: $resolvedOutput"
