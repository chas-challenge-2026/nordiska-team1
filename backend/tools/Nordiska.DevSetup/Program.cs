using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Npgsql;
namespace Nordiska.DevSetup;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "  dotnet run --project backend/tools/Nordiska.DevSetup -- <repo-root>\n" +
                "  dotnet run --project backend/tools/Nordiska.DevSetup -- <repo-root> seed-transactions [customerId] [batchSize]\n" +
                "  dotnet run --project backend/tools/Nordiska.DevSetup -- <repo-root> report-pressure-test [customerId] [accountId] [year] [batchSize] [delayMs]\n" +
                "  dotnet run --project backend/tools/Nordiska.DevSetup -- <repo-root> direct-report-pressure-test [customerId] [accountId] [year] [batchSize] [delayMs] [baseUrl] [bearerToken]");

            return 1;
        }

        try
        {
            var root = Path.GetFullPath(args[0]);

            if (args.Length > 1 &&
                string.Equals(args[1], "seed-transactions", StringComparison.OrdinalIgnoreCase))
            {
                var customerId = args.Length > 2 ? long.Parse(args[2]) : 1;
                var batchSize = args.Length > 3 ? int.Parse(args[3]) : 1000;
                await SeedTransactionsUntilStoppedAsync(root, customerId, batchSize);
                return 0;
            }

            if (args.Length > 1 &&
                string.Equals(args[1], "report-pressure-test", StringComparison.OrdinalIgnoreCase))
            {
                var customerId = args.Length > 2 ? long.Parse(args[2]) : 1;
                var accountId = args.Length > 3 ? long.Parse(args[3]) : 0;
                var year = args.Length > 4 ? int.Parse(args[4]) : DateTime.UtcNow.Year;
                var batchSize = args.Length > 5 ? int.Parse(args[5]) : 10;
                var delayMs = args.Length > 6 ? int.Parse(args[6]) : 250;
                await PressureTestReportsUntilStoppedAsync(root, customerId, accountId, year, batchSize, delayMs);
                return 0;
            }

            if (args.Length > 1 &&
                string.Equals(args[1], "direct-report-pressure-test", StringComparison.OrdinalIgnoreCase))
            {
                var customerId = args.Length > 2 ? long.Parse(args[2]) : 1;
                var accountId = args.Length > 3 ? long.Parse(args[3]) : 0;
                var year = args.Length > 4 ? int.Parse(args[4]) : DateTime.UtcNow.Year;
                var batchSize = args.Length > 5 ? int.Parse(args[5]) : 10;
                var delayMs = args.Length > 6 ? int.Parse(args[6]) : 250;
                var baseUrl = args.Length > 7 ? args[7] : "http://localhost:5031";
                var bearerToken = args.Length > 8 ? args[8] : null;
                await PressureTestDirectReportsUntilStoppedAsync(root, customerId, accountId, year, batchSize, delayMs, baseUrl, bearerToken);
                return 0;
            }

            if (args.Length != 1)
            {
                Console.Error.WriteLine(
                    "Pass the repository root as the first argument or use a dedicated dev mode such as seed-transactions or report-pressure-test.");

                return 1;
            }

            await DatabaseSetup.CheckDockerAsync(root);
            await DatabaseSetup.RestoreToolsAsync(root);

            var envPath = Path.Combine(root, "infra", "v2", ".env");

            if (!File.Exists(envPath))
                PasswordGenerator.CreateEnv(root);

            var passwords = DatabaseSetup.ReadEnv(root);

            await DatabaseSetup.StartDatabaseAsync(root, passwords);
            await DatabaseSetup.ApplyMigrationsAsync(root, passwords);
            await DatabaseSetup.ApplyPermissionsAsync(root, passwords);
            await DatabaseSetup.ConfigureApiConnectionsAsync(root, passwords);
            await DatabaseSetup.TestDatabaseAsync(passwords);
            Console.WriteLine("Local database setup completed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task SeedTransactionsUntilStoppedAsync(string root, long customerId, int batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");

        var envPath = Path.Combine(root, "infra", "v2", ".env");
        if (!File.Exists(envPath))
            throw new InvalidOperationException($"Environment file not found at {envPath}. Run the normal DB setup first.");

        var passwords = DatabaseSetup.ReadEnv(root);
        var connectionString = DatabaseSetup.CreateConnectionString(
            "nordiska_api",
            passwords["NORDISKA_API_PASSWORD"]);

        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new BankingDbContext(options);

        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Could not connect to the local Nordiska database.");

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer is null)
            throw new InvalidOperationException($"Customer with id {customerId} was not found.");

        var account = await db.SavingsAccounts
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync();

        if (account is null)
        {
            account = new SavingsAccount
            {
                CustomerId = customerId,
                AccountNumber = $"SEED-{customerId}-{DateTime.UtcNow:yyMMddHHmmss}",
                AccountType = "flex",
                Balance = 0m,
                InterestRate = 0.0350m,
                CreatedAt = DateTime.UtcNow,
                Status = "active"
            };

            db.SavingsAccounts.Add(account);
            await db.SaveChangesAsync();
        }

        var totalInserted = 0;
        var loop = 0;
        Console.WriteLine($"Seeding transactions for customer {customerId} on account {account.Id} ({account.AccountNumber}). Press Enter or Ctrl+C to stop.");

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                var batchInserted = 0;

                while (batchInserted < batchSize && !shutdown.IsCancellationRequested)
                {
                    var isDeposit = (loop + batchInserted) % 2 == 0;
                    var amount = 25m + ((loop + batchInserted) % 12) * 50m;
                    var ledgerEntry = new LedgerEntry
                    {
                        AccountId = account.Id,
                        Type = isDeposit ? "deposit" : "withdrawal",
                        Amount = isDeposit ? amount : -amount,
                        Label = $"Seed {totalInserted + 1}",
                        CreatedAt = DateTime.UtcNow
                    };

                    db.LedgerEntries.Add(ledgerEntry);
                    account.Balance += isDeposit ? amount : -amount;
                    account.UpdatedAt = DateTime.UtcNow;

                    batchInserted++;
                    totalInserted++;
                    loop++;
                }

                await db.SaveChangesAsync();
                Console.WriteLine($"Inserted {batchInserted} transactions. Running total: {totalInserted}.");

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Enter || key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }

                if (!shutdown.IsCancellationRequested)
                {
                    await Task.Delay(500, shutdown.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the user presses Ctrl+C.
        }

        Console.WriteLine($"Stopped. Total seeded transactions: {totalInserted}.");
    }

    private static async Task PressureTestReportsUntilStoppedAsync(
        string root,
        long customerId,
        long accountId,
        int year,
        int batchSize,
        int delayMs)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");

        if (delayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be zero or greater.");

        var envPath = Path.Combine(root, "infra", "v2", ".env");
        if (!File.Exists(envPath))
            throw new InvalidOperationException($"Environment file not found at {envPath}. Run the normal DB setup first.");

        var passwords = DatabaseSetup.ReadEnv(root);
        var connectionString = DatabaseSetup.CreateConnectionString(
            "nordiska_api",
            passwords["NORDISKA_API_PASSWORD"]);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        if (accountId == 0)
        {
            await using var banking = new BankingDbContext(
                new DbContextOptionsBuilder<BankingDbContext>()
                    .UseNpgsql(connectionString)
                    .Options);

            accountId = await banking.SavingsAccounts
                .Where(a => a.CustomerId == customerId)
                .OrderBy(a => a.Id)
                .Select(a => a.Id)
                .FirstOrDefaultAsync();
        }

        if (accountId == 0)
            throw new InvalidOperationException($"No savings account was found for customer {customerId}.");

        var totalSubmitted = 0;
        Console.WriteLine(
            $"Pressure testing report generation for customer {customerId}, account {accountId}, year {year}. " +
            $"Batch size: {batchSize}. Press Enter or Ctrl+C to stop.");

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                var queuedThisLoop = 0;
                while (queuedThisLoop < batchSize && !shutdown.IsCancellationRequested)
                {
                    var jobId = Guid.NewGuid();
                    var createdAt = DateTime.UtcNow;
                    var commandText = @"
                        INSERT INTO reporting.tax_report_jobs (
                            ""Id"",
                            ""CustomerId"",
                            ""AccountId"",
                            ""Year"",
                            ""Status"",
                            ""DownloadUrl"",
                            ""CreatedAt"",
                            ""UpdatedAt""
                        )
                        VALUES (
                            @Id,
                            @CustomerId,
                            @AccountId,
                            @Year,
                            @Status,
                            @DownloadUrl,
                            @CreatedAt,
                            @UpdatedAt
                        );";

                    await using (var command = new NpgsqlCommand(commandText, connection))
                    {
                        command.Parameters.AddWithValue("@Id", jobId);
                        command.Parameters.AddWithValue("@CustomerId", customerId);
                        command.Parameters.AddWithValue("@AccountId", accountId);
                        command.Parameters.AddWithValue("@Year", year);
                        command.Parameters.AddWithValue("@Status", "Pending");
                        command.Parameters.AddWithValue("@DownloadUrl", $"/api/reports/jobs/{jobId}/download");
                        command.Parameters.AddWithValue("@CreatedAt", createdAt);
                        command.Parameters.AddWithValue("@UpdatedAt", createdAt);
                        await command.ExecuteNonQueryAsync(shutdown.Token);
                    }

                    totalSubmitted++;
                    queuedThisLoop++;
                }

                Console.WriteLine($"Queued {queuedThisLoop} report jobs. Total submitted: {totalSubmitted}.");

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Enter || key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }

                if (!shutdown.IsCancellationRequested && delayMs > 0)
                {
                    await Task.Delay(delayMs, shutdown.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the user presses Ctrl+C.
        }

        Console.WriteLine($"Stopped. Total report jobs queued: {totalSubmitted}.");
    }

    private static async Task PressureTestDirectReportsUntilStoppedAsync(
        string root,
        long customerId,
        long accountId,
        int year,
        int batchSize,
        int delayMs,
        string baseUrl,
        string? bearerToken)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");

        if (delayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be zero or greater.");

        if (accountId == 0)
        {
            var envPath = Path.Combine(root, "infra", "v2", ".env");
            if (!File.Exists(envPath))
                throw new InvalidOperationException($"Environment file not found at {envPath}. Run the normal DB setup first.");

            var passwords = DatabaseSetup.ReadEnv(root);
            var connectionString = DatabaseSetup.CreateConnectionString(
                "nordiska_api",
                passwords["NORDISKA_API_PASSWORD"]);

            await using var banking = new BankingDbContext(
                new DbContextOptionsBuilder<BankingDbContext>()
                    .UseNpgsql(connectionString)
                    .Options);

            accountId = await banking.SavingsAccounts
                .Where(a => a.CustomerId == customerId)
                .OrderBy(a => a.Id)
                .Select(a => a.Id)
                .FirstOrDefaultAsync();
        }

        if (accountId == 0)
            throw new InvalidOperationException($"No savings account was found for customer {customerId}.");

        var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var totalRequests = 0;
        Console.WriteLine(
            $"Hammering direct tax report generation for customer {customerId}, account {accountId}, year {year}. " +
            $"Batch size: {batchSize}. Base URL: {baseUrl}. Press Enter or Ctrl+C to stop.");

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                var loopCount = 0;
                while (loopCount < batchSize && !shutdown.IsCancellationRequested)
                {
                    var requestUri = $"/api/reports/tax-report?accountId={accountId}&year={year}";
                    using var response = await client.GetAsync(requestUri, shutdown.Token);
                    totalRequests++;

                    if (response.IsSuccessStatusCode)
                    {
                        var contentLength = response.Content.Headers.ContentLength ?? 0;
                        Console.WriteLine($"Request {totalRequests}: HTTP {(int)response.StatusCode} ({contentLength} bytes)");
                    }
                    else
                    {
                        var body = await response.Content.ReadAsStringAsync(shutdown.Token);
                        var preview = body.Length > 200 ? body[..200] : body;
                        Console.WriteLine($"Request {totalRequests}: HTTP {(int)response.StatusCode} - {preview}");
                    }

                    loopCount++;
                }

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Enter || key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }

                if (!shutdown.IsCancellationRequested && delayMs > 0)
                {
                    await Task.Delay(delayMs, shutdown.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the user presses Ctrl+C.
        }

        Console.WriteLine($"Stopped. Total requests sent: {totalRequests}.");
    }
}

internal static class PasswordGenerator
{
    static readonly string[] passwordVariables =
    [
        "POSTGRES_BOOTSTRAP_PASSWORD",
        "NORDISKA_MIGRATOR_PASSWORD",
        "NORDISKA_API_PASSWORD",
        "NORDISKA_REPORTING_WORKER_PASSWORD"
    ];
    public static string Generate()
    {
        var bytes = new byte[32];

        RandomNumberGenerator.Fill(bytes);

        return Convert.ToHexString(bytes);
    }

    public static void CreateEnv(string root)
    {
        var path = Path.Combine(root,"infra","v2",".env");
        using var file = new FileStream
        (
            path,
            FileMode.CreateNew,
            FileAccess.Write
        );
        using var writer = new StreamWriter(file);
        foreach(var password in passwordVariables)
        {
            writer.WriteLine($"{password}={Generate()}");
        }
    }
}



internal static class DatabaseSetup
{
    private static readonly string[] Modules =
    [
        "Banking",
        "Faq",
        "Reporting"
    ];


    public static Dictionary<string, string> ReadEnv(string root)
    {
        var path = Path.Combine(root, "infra", "v2", ".env");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(path))
        {
            var text = line.Trim();

            if (text.Length == 0 || text.StartsWith('#'))
                continue;

            var parts = text.Split('=', 2);

            if (parts.Length != 2)
                throw new InvalidOperationException("Invalid .env entry.");

            values.Add(parts[0].Trim(), parts[1].Trim());
        }

        return values;
    }

    public static async Task CheckDockerAsync(string root)
    {
        await RunAsync(
            "Checking Docker",
            "docker",
            root,
            ["version"]);

        await RunAsync(
            "Checking Docker Compose",
            "docker",
            root,
            ["compose", "version"]);
    }

    public static async Task RestoreToolsAsync(string root)
    {
        var backend = Path.Combine(root, "backend");
        var manifest = Path.Combine(backend, "dotnet-tools.json");

        if (!File.Exists(manifest))
        {
            throw new InvalidOperationException(
                "The backend/dotnet-tools.json manifest is missing.");
        }

        await RunAsync(
            "Restoring local .NET tools",
            "dotnet",
            backend,
            ["tool", "restore", "--tool-manifest", manifest]);
    }

    public static Task StartDatabaseAsync(
        string root,
        Dictionary<string, string> passwords)
    {
        return RunComposeAsync(
            root,
            passwords,
            "Starting PostgreSQL",
            [
                "up", "-d",
                "--wait",
                "--wait-timeout", "120",
                "db"
            ]);
    }

    public static string CreateConnectionString(
        string username,
        string password)
    {
        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = "127.0.0.1",
            ["Port"] = 5433,
            ["Database"] = "nordiska_v2",
            ["Username"] = username,
            ["Password"] = password
        };

        return builder.ConnectionString;
    }

    public static async Task ApplyMigrationsAsync(
        string root,
        Dictionary<string, string> passwords)
    {
        var backend = Path.Combine(root, "backend");

        var startupProject = Path.Combine(
            backend,
            "src",
            "Nordiska.FrontendApi",
            "Nordiska.FrontendApi.csproj");

        var connectionString = CreateConnectionString(
            "nordiska_migrator",
            passwords["NORDISKA_MIGRATOR_PASSWORD"]);

        foreach (var module in Modules)
        {
            var project = Path.Combine(
                backend,
                "src",
                "Modules",
                module,
                $"Nordiska.Modules.{module}.csproj");

            // Only this child process receives this migration connection.
            var environment = new Dictionary<string, string>
            {
                [$"ConnectionStrings__{module}MigrationDatabase"]
                    = connectionString
            };

            await RunAsync(
                $"Applying {module} migrations",
                "dotnet",
                backend,
                [
                    "tool", "run", "dotnet-ef", "--",
                    "database", "update",
                    "--context", $"{module}DbContext",
                    "--project", project,
                    "--startup-project", startupProject
                ],
                environment);
        }
    }

    public static Task ApplyPermissionsAsync(
        string root,
        Dictionary<string, string> passwords)
    {
        return RunComposeAsync(
            root,
            passwords,
            "Applying database permissions",
            [
                "exec", "-T", "db",
                "psql",
                "-U", "nordiska_migrator",
                "-d", "nordiska_v2",
                "-v", "ON_ERROR_STOP=1",
                "-f", "/opt/nordiska/post-migration-permissions.sql"
            ]);
    }


    public static async Task ConfigureApiConnectionsAsync(
        string root,
        Dictionary<string, string> passwords)
    {
        var project = Path.Combine(
            root,
            "backend",
            "src",
            "Nordiska.FrontendApi",
            "Nordiska.FrontendApi.csproj");

        var connectionString = CreateConnectionString(
            "nordiska_api",
            passwords["NORDISKA_API_PASSWORD"]);

        var secrets = Modules.ToDictionary(
            module => $"ConnectionStrings:{module}Database",
            _ => connectionString);

        await RunAsync(
            "Configuring local API connection strings",
            "dotnet",
            root,
            ["user-secrets", "set", "--project", project],
            input: JsonSerializer.Serialize(secrets));
    }

    private static Task RunComposeAsync(
        string root,
        Dictionary<string, string> passwords,
        string description,
        string[] arguments)
    {
        var directory = Path.Combine(root, "infra", "v2");

        return RunAsync(
            description,
            "docker",
            directory,
            [
                "compose",
                "--project-name", "nordiska-v2",
                "--env-file", ".env",
                "-f", "docker-compose.yml",
                "-f", "docker-compose.override.yml",
                .. arguments
            ],
            passwords);
    }

    private static async Task RunAsync(
        string description,
        string executable,
        string directory,
        string[] arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        string? input = null)
    {
        Console.WriteLine($"{description}...");

        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (environment is not null)
        {
            foreach (var entry in environment)
                startInfo.Environment[entry.Key] = entry.Value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Could not start {executable}.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync();
        await Task.WhenAll(outputTask, errorTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{description} failed with exit code {process.ExitCode}.");
        }

        Console.WriteLine($"{description}: completed.");
    }


    public static async Task TestDatabaseAsync(
    Dictionary<string, string> passwords)
    {
        var connectionString = CreateConnectionString(
            "nordiska_api",
            passwords["NORDISKA_API_PASSWORD"]);

        await using var dataSource =
            NpgsqlDataSource.Create(connectionString);

        await using var command =
            dataSource.CreateCommand("SELECT 1;");

        // Opens a connection, authenticates, and executes the query.
        var result = await command.ExecuteScalarAsync();

        if (result is not int value || value != 1)
        {
            throw new InvalidOperationException(
                "Database test returned an unexpected result.");
        }

        Console.WriteLine(
            "Database test passed: API role connected and executed SQL.");
    }
}