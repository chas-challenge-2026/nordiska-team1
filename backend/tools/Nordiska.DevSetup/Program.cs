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


internal static class ConsoleUi
{
    private static readonly object Sync = new();

    public static void WriteLine() => Console.WriteLine();

    public static void WriteLine(string text)
    {
        WriteMultiline(Console.Out, text, Console.IsOutputRedirected);
    }

    public static void WriteErrorLine() => Console.Error.WriteLine();

    public static void WriteErrorLine(string text)
    {
        WriteMultiline(Console.Error, text, Console.IsErrorRedirected);
    }

    private static void WriteMultiline(
        TextWriter writer,
        string text,
        bool outputIsRedirected)
    {
        // Color is supplemental only. Tags such as [OK], [WARNING], and [ADVICE]
        // remain in the text so redirected logs and non-color terminals stay readable.
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        lock (Sync)
        {
            foreach (var line in lines)
                WriteSingleLine(writer, line, outputIsRedirected);
        }
    }

    private static void WriteSingleLine(
        TextWriter writer,
        string text,
        bool outputIsRedirected)
    {
        var color = GetColor(text);

        if (color is null || outputIsRedirected)
        {
            writer.WriteLine(text);
            return;
        }

        var previousColor = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = color.Value;
            writer.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }
    }

    private static ConsoleColor? GetColor(string text)
    {
        var trimmed = text.TrimStart();

        // Failures first so they are impossible to miss.
        if (trimmed.StartsWith("[FATAL]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[ERROR]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[DATABASE AUTH]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[DATABASE PERMISSION]", StringComparison.Ordinal))
        {
            return ConsoleColor.Red;
        }

        if (trimmed.StartsWith("[WARNING]", StringComparison.Ordinal) ||
            trimmed.StartsWith("WARNING:", StringComparison.Ordinal) ||
            trimmed.StartsWith("[CAUSE]", StringComparison.Ordinal))
        {
            return ConsoleColor.Yellow;
        }

        // Actions the developer can safely take are deliberately green.
        if (trimmed.StartsWith("[ADVICE]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[COMMAND]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[OK]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[SUCCESS]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[REUSE]", StringComparison.Ordinal) ||
            trimmed.Contains("[REUSE]", StringComparison.Ordinal) ||
            trimmed.Contains("Passed:", StringComparison.Ordinal))
        {
            return ConsoleColor.Green;
        }

        if (trimmed.StartsWith("[START]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[GENERATE]", StringComparison.Ordinal) ||
            trimmed.Contains("[GENERATE]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[SECRETS]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[ENV]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[CONNECTION STRINGS]", StringComparison.Ordinal) ||
            trimmed.StartsWith("[DATABASE TEST]", StringComparison.Ordinal) ||
            trimmed.Equals("Nordiska local database setup", StringComparison.Ordinal) ||
            trimmed.Contains("DIAGNOSIS", StringComparison.Ordinal))
        {
            return ConsoleColor.Cyan;
        }

        if (trimmed.StartsWith("Executable", StringComparison.Ordinal) ||
            trimmed.StartsWith("Working directory", StringComparison.Ordinal) ||
            trimmed.StartsWith("Command", StringComparison.Ordinal) ||
            trimmed.StartsWith("Environment keys", StringComparison.Ordinal) ||
            trimmed.StartsWith("Environment values", StringComparison.Ordinal) ||
            trimmed.StartsWith("Standard input", StringComparison.Ordinal) ||
            trimmed.StartsWith("Repository root:", StringComparison.Ordinal) ||
            trimmed.StartsWith(".env path:", StringComparison.Ordinal) ||
            trimmed.StartsWith("---", StringComparison.Ordinal) ||
            trimmed.StartsWith("===", StringComparison.Ordinal))
        {
            return ConsoleColor.DarkGray;
        }

        return null;
    }
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleUi.WriteErrorLine("[ERROR] Invalid arguments.");
            ConsoleUi.WriteErrorLine("Expected exactly one argument: the repository root.");
            ConsoleUi.WriteErrorLine("Example: dotnet run -- C:\\src\\nordiska-team1");
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

            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(
                    $"Repository root does not exist: '{root}'.");
            }

            ConsoleUi.WriteLine("============================================================");
            ConsoleUi.WriteLine("Nordiska local database setup");
            ConsoleUi.WriteLine($"Repository root: {root}");
            ConsoleUi.WriteLine("============================================================");
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

            // Keep existing generated secrets stable between setup runs.
            // Only missing, empty, or placeholder values are generated.
            SecretGenerator.EnsureEnv(root);

            var passwords = DatabaseSetup.ReadEnv(root);

            await DatabaseSetup.StartDatabaseAsync(root, passwords);
            await DatabaseSetup.EnsureModuleSchemasAsync(root, passwords);
            await DatabaseSetup.ApplyMigrationsAsync(root, passwords);
            await DatabaseSetup.ApplyPermissionsAsync(root, passwords);
            await DatabaseSetup.ConfigureApiConnectionsAsync(root, passwords);
            await DatabaseSetup.TestDatabaseAsync(passwords);

            ConsoleUi.WriteLine();
            ConsoleUi.WriteLine("============================================================");
            ConsoleUi.WriteLine("[SUCCESS] Local database setup completed.");
            ConsoleUi.WriteLine("============================================================");
            return 0;
        }
        catch (Exception exception)
        {
            ConsoleUi.WriteErrorLine();
            ConsoleUi.WriteErrorLine("============================================================");
            ConsoleUi.WriteErrorLine("[FATAL] Local database setup failed.");
            ConsoleUi.WriteErrorLine($"Exception type : {exception.GetType().FullName}");
            ConsoleUi.WriteErrorLine($"Message        : {exception.Message}");

            if (exception.InnerException is not null)
            {
                ConsoleUi.WriteErrorLine(
                    $"Inner exception: {exception.InnerException.GetType().FullName}: " +
                    exception.InnerException.Message);
            }

            ConsoleUi.WriteErrorLine();
            ConsoleUi.WriteErrorLine("Stack trace:");
            ConsoleUi.WriteErrorLine(exception.StackTrace ?? "<no stack trace available>");
            ConsoleUi.WriteErrorLine("============================================================");
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

internal static class SecretGenerator
{
    private static readonly string[] SecretVariables =
    [
        "POSTGRES_BOOTSTRAP_PASSWORD",
        "NORDISKA_MIGRATOR_PASSWORD",
        "NORDISKA_API_PASSWORD",
        "NORDISKA_REPORTING_WORKER_PASSWORD",
        "NORDISKA_JWT_SECRET",
        "NORDISKA_AUDIT_SIGNING_KEY"
    ];

    private static readonly HashSet<string> SecretVariableSet =
        new(SecretVariables, StringComparer.Ordinal);

    public static IReadOnlyList<string> RequiredVariables => SecretVariables;

    public static string Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    public static void EnsureEnv(string root)
    {
        var directory = Path.Combine(root, "infra", "v2");
        var path = Path.Combine(directory, ".env");

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Cannot create or update .env because the directory is missing: '{directory}'.");
        }

        try
        {
            ConsoleUi.WriteLine();
            ConsoleUi.WriteLine("[SECRETS] Checking required environment secrets...");
            ConsoleUi.WriteLine($".env path: {path}");

            var existingLines = File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string>();

            if (File.Exists(path))
                ConsoleUi.WriteLine("Existing .env file found.");
            else
                ConsoleUi.WriteLine("No .env file found. A new one will be created.");

            var existingSecrets = ReadExistingGeneratedSecrets(existingLines);
            var finalSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
            var generatedCount = 0;
            var reusedCount = 0;

            foreach (var variable in SecretVariables)
            {
                if (existingSecrets.TryGetValue(variable, out var existingValue) &&
                    IsUsableSecret(existingValue))
                {
                    finalSecrets[variable] = existingValue;
                    reusedCount++;
                    ConsoleUi.WriteLine($"  [REUSE] {variable}");
                }
                else
                {
                    finalSecrets[variable] = Generate();
                    generatedCount++;

                    var reason = !existingSecrets.TryGetValue(variable, out var invalidValue)
                        ? "variable was missing"
                        : string.IsNullOrWhiteSpace(invalidValue)
                            ? "existing value was empty"
                            : "existing value was a template placeholder";

                    ConsoleUi.WriteLine($"  [GENERATE] {variable} ({reason})");
                }
            }

            var updatedLines = MergeGeneratedSecrets(existingLines, finalSecrets);

            // Write to a temporary file first, then replace the destination.
            // This avoids leaving a half-written .env if writing fails.
            var temporaryPath = path + ".tmp";

            try
            {
                File.WriteAllLines(temporaryPath, updatedLines);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            ConsoleUi.WriteLine(
                $"[SECRETS] Complete. Reused {reusedCount} secret(s); " +
                $"generated {generatedCount} secret(s).");
            ConsoleUi.WriteLine("[SECRETS] Secret values are intentionally not printed.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed while checking or updating generated secrets in '{path}'. " +
                $"{exception.GetType().Name}: {exception.Message}",
                exception);
        }
    }

    private static Dictionary<string, string> ReadExistingGeneratedSecrets(
        IEnumerable<string> lines)
    {
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            if (!TryReadVariable(line, out var variable, out var value) ||
                !SecretVariableSet.Contains(variable))
            {
                continue;
            }

            // Prefer the first usable value. This lets us clean up accidental
            // duplicates without rotating an existing secret.
            if (!secrets.TryGetValue(variable, out var current) ||
                !IsUsableSecret(current))
            {
                secrets[variable] = value;
            }
        }

        return secrets;
    }

    private static List<string> MergeGeneratedSecrets(
        IEnumerable<string> existingLines,
        IReadOnlyDictionary<string, string> secrets)
    {
        var result = new List<string>();
        var written = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in existingLines)
        {
            if (!TryReadVariable(line, out var variable, out _) ||
                !SecretVariableSet.Contains(variable))
            {
                result.Add(line);
                continue;
            }

            // Normalize the first occurrence to the chosen value and remove
            // accidental duplicates. Existing usable secrets are reused.
            if (written.Add(variable))
                result.Add($"{variable}={secrets[variable]}");
        }

        foreach (var variable in SecretVariables)
        {
            if (written.Add(variable))
                result.Add($"{variable}={secrets[variable]}");
        }

        return result;
    }

    private static bool IsUsableSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return !string.Equals(
                   value,
                   "placeholder",
                   StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith(
                   "replace-with-",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadVariable(
        string line,
        out string variable,
        out string value)
    {
        variable = string.Empty;
        value = string.Empty;

        var trimmed = line.Trim();

        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            return false;

        var separatorIndex = trimmed.IndexOf('=');

        if (separatorIndex <= 0)
            return false;

        variable = trimmed[..separatorIndex].Trim();
        value = trimmed[(separatorIndex + 1)..].Trim();
        return variable.Length > 0;
    }
}

internal static class DatabaseSetup
{
    private static readonly string[] Modules =
    [
        "Banking",
        "Faq",
        "Reporting",
        "Inbox"
    ];

    public static Dictionary<string, string> ReadEnv(string root)
    {
        var path = Path.Combine(root, "infra", "v2", ".env");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The required .env file does not exist: '{path}'.",
                path);
        }

        try
        {
            ConsoleUi.WriteLine();
            ConsoleUi.WriteLine("[ENV] Reading generated passwords from .env...");

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var lineNumber = 0;

            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                var text = line.Trim();

                if (text.Length == 0 || text.StartsWith('#'))
                    continue;

                var parts = text.Split('=', 2);

                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    throw new InvalidOperationException(
                        $"Invalid .env entry at line {lineNumber}. " +
                        "Expected KEY=VALUE.");
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (!values.TryAdd(key, value))
                {
                    throw new InvalidOperationException(
                        $"Duplicate .env variable '{key}' at line {lineNumber}.");
                }
            }

            foreach (var requiredVariable in SecretGenerator.RequiredVariables)
            {
                if (!values.TryGetValue(requiredVariable, out var value))
                {
                    throw new InvalidOperationException(
                        $"Required generated secret '{requiredVariable}' is missing from '{path}'.");
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        $"Required generated secret '{requiredVariable}' is empty in '{path}'.");
                }
            }

            ConsoleUi.WriteLine(
                $"[ENV] .env loaded successfully. {values.Count} variable(s) found.");
            ConsoleUi.WriteLine("[ENV] Generated password values are present and non-empty.");
            return values;
        }
        catch (Exception exception) when (exception is not FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Failed to read or validate .env file '{path}'. " +
                $"{exception.GetType().Name}: {exception.Message}",
                exception);
        }
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

        if (!Directory.Exists(backend))
        {
            throw new DirectoryNotFoundException(
                $"Backend directory is missing: '{backend}'.");
        }

        if (!File.Exists(manifest))
        {
            throw new FileNotFoundException(
                $"The backend/dotnet-tools.json manifest is missing: '{manifest}'.",
                manifest);
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

    public static Task EnsureModuleSchemasAsync(
        string root,
        Dictionary<string, string> passwords)
    {
        // Schema creation is a bootstrap responsibility, not a runtime/API responsibility.
        // The bootstrap role is the PostgreSQL superuser created by docker-compose via
        // POSTGRES_USER=nordiska_bootstrap. It creates only the four approved module
        // schemas, then the migrator operates inside those schemas.
        const string sql =
            "CREATE SCHEMA IF NOT EXISTS banking AUTHORIZATION nordiska_migrator; " +
            "CREATE SCHEMA IF NOT EXISTS faq AUTHORIZATION nordiska_migrator; " +
            "CREATE SCHEMA IF NOT EXISTS reporting AUTHORIZATION nordiska_migrator; " +
            "CREATE SCHEMA IF NOT EXISTS inbox AUTHORIZATION nordiska_migrator; " +
            "REVOKE ALL ON SCHEMA banking, faq, reporting, inbox FROM PUBLIC; " +
            "GRANT USAGE, CREATE ON SCHEMA banking, faq, reporting, inbox TO nordiska_migrator;";

        return RunComposeAsync(
            root,
            passwords,
            "Ensuring module schemas and migration permissions",
            [
                "exec", "-T", "db",
                "psql",
                "-U", "nordiska_bootstrap",
                "-d", "nordiska_v2",
                "-v", "ON_ERROR_STOP=1",
                "-c", sql
            ]);
    }

    public static string CreateConnectionString(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Database username cannot be empty.", nameof(username));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Database password cannot be empty.", nameof(password));

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

        if (!File.Exists(startupProject))
        {
            throw new FileNotFoundException(
                $"Frontend API startup project is missing: '{startupProject}'.",
                startupProject);
        }

        var connectionString = CreateConnectionString(
            "nordiska_migrator",
            GetRequiredPassword(passwords, "NORDISKA_MIGRATOR_PASSWORD"));

        foreach (var module in Modules)
        {
            var project = Path.Combine(
                backend,
                "src",
                "Modules",
                module,
                $"Nordiska.Modules.{module}.csproj");

            if (!File.Exists(project))
            {
                throw new FileNotFoundException(
                    $"Cannot apply {module} migrations because the module project is missing: " +
                    $"'{project}'.",
                    project);
            }

            // Only this child process receives this migration connection.
            var environment = new Dictionary<string, string>
            {
                [$"ConnectionStrings__{module}MigrationDatabase"] = connectionString
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

        if (!File.Exists(project))
        {
            throw new FileNotFoundException(
                $"Cannot configure API connection strings because the project is missing: '{project}'.",
                project);
        }

        var secretKeys = Modules
            .Select(module => $"ConnectionStrings:{module}Database")
            .ToArray();

        ConsoleUi.WriteLine();
        ConsoleUi.WriteLine("[CONNECTION STRINGS] Checking existing API user-secrets...");

        var existingSecretKeys = await GetExistingUserSecretKeysAsync(
            root,
            project,
            secretKeys);

        if (existingSecretKeys.Count > 0)
        {
            ConsoleUi.WriteLine(
                "Existing generated API connection strings found and will be overwritten:");

            foreach (var key in existingSecretKeys)
                ConsoleUi.WriteLine($"  - {key}");
        }
        else
        {
            ConsoleUi.WriteLine(
                "No existing generated API connection strings were found. " +
                "They will be created.");
        }

        var connectionString = CreateConnectionString(
            "nordiska_api",
            GetRequiredPassword(passwords, "NORDISKA_API_PASSWORD"));

        var secrets = secretKeys.ToDictionary(
            key => key,
            _ => connectionString,
            StringComparer.Ordinal);

        await RunAsync(
            "Writing local API connection strings",
            "dotnet",
            root,
            ["user-secrets", "set", "--project", project],
            input: JsonSerializer.Serialize(secrets),
            showOutputOnSuccess: false);

        ConsoleUi.WriteLine(
            $"[CONNECTION STRINGS] Wrote {secrets.Count} API connection string secret(s). " +
            "Existing values were replaced.");
        ConsoleUi.WriteLine("[CONNECTION STRINGS] Secret values are intentionally not printed.");
    }

    private static async Task<List<string>> GetExistingUserSecretKeysAsync(
        string root,
        string project,
        IReadOnlyCollection<string> targetKeys)
    {
        var result = await RunProcessAsync(
            "Reading existing API user-secrets",
            "dotnet",
            root,
            ["user-secrets", "list", "--project", project],
            showOutputOnSuccess: false,
            outputMayContainSecrets: true);

        var found = new List<string>();

        foreach (var targetKey in targetKeys)
        {
            var exists = result.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.StartsWith(targetKey + " =", StringComparison.Ordinal));

            if (exists)
                found.Add(targetKey);
        }

        return found;
    }

    private static Task RunComposeAsync(
        string root,
        Dictionary<string, string> passwords,
        string description,
        string[] arguments)
    {
        var directory = Path.Combine(root, "infra", "v2");

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Docker Compose directory is missing: '{directory}'.");
        }

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
        string? input = null,
        bool showOutputOnSuccess = true)
    {
        await RunProcessAsync(
            description,
            executable,
            directory,
            arguments,
            environment,
            input,
            showOutputOnSuccess);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string description,
        string executable,
        string directory,
        string[] arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        string? input = null,
        bool showOutputOnSuccess = true,
        bool outputMayContainSecrets = false)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Cannot run '{description}' because the working directory does not exist: " +
                $"'{directory}'.");
        }

        ConsoleUi.WriteLine();
        ConsoleUi.WriteLine($"[START] {description}");
        ConsoleUi.WriteLine($"Executable       : {executable}");
        ConsoleUi.WriteLine($"Working directory: {directory}");
        ConsoleUi.WriteLine($"Command          : {FormatCommand(executable, arguments)}");

        if (environment is { Count: > 0 })
        {
            ConsoleUi.WriteLine(
                "Environment keys : " + string.Join(", ", environment.Keys));
            ConsoleUi.WriteLine("Environment values are hidden because they may contain secrets.");
        }

        if (input is not null)
            ConsoleUi.WriteLine("Standard input   : supplied (content hidden)");

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

        Process process;

        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"Process.Start returned null for executable '{executable}'.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Could not start '{description}'.{Environment.NewLine}" +
                $"Executable: {executable}{Environment.NewLine}" +
                $"Working directory: {directory}{Environment.NewLine}" +
                $"Command: {FormatCommand(executable, arguments)}{Environment.NewLine}" +
                $"Original error: {exception.GetType().Name}: {exception.Message}",
                exception);
        }

        using (process)
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (input is not null)
            {
                try
                {
                    await process.StandardInput.WriteAsync(input);
                    process.StandardInput.Close();
                }
                catch (Exception exception)
                {
                    TryKill(process);

                    throw new InvalidOperationException(
                        $"Failed while writing standard input for '{description}'. " +
                        $"{exception.GetType().Name}: {exception.Message}",
                        exception);
                }
            }

            try
            {
                await process.WaitForExitAsync();
                await Task.WhenAll(outputTask, errorTask);
            }
            catch (Exception exception)
            {
                TryKill(process);

                throw new InvalidOperationException(
                    $"Failed while waiting for '{description}' to finish. " +
                    $"{exception.GetType().Name}: {exception.Message}",
                    exception);
            }

            var output = RedactSensitiveText(outputTask.Result, environment, input);
            var error = RedactSensitiveText(errorTask.Result, environment, input);
            var result = new ProcessResult(process.ExitCode, output, error);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    BuildProcessFailureMessage(
                        description,
                        executable,
                        directory,
                        arguments,
                        result,
                        environment,
                        input is not null,
                        outputMayContainSecrets));
            }

            if (showOutputOnSuccess)
            {
                PrintProcessOutput("stdout", output);
                PrintProcessOutput("stderr", error);
            }

            ConsoleUi.WriteLine($"[OK] {description} completed with exit code 0.");
            return result;
        }
    }

    private static string BuildProcessFailureMessage(
        string description,
        string executable,
        string directory,
        string[] arguments,
        ProcessResult result,
        IReadOnlyDictionary<string, string>? environment,
        bool hadInput,
        bool outputMayContainSecrets)
    {
        var environmentKeys = environment is { Count: > 0 }
            ? string.Join(", ", environment.Keys)
            : "<none>";

        var stdout = outputMayContainSecrets
            ? "<hidden because this command may print secret values>"
            : FormatOutput(result.StandardOutput);

        var stderr = outputMayContainSecrets
            ? "<hidden because this command may print secret values>"
            : FormatOutput(result.StandardError);

        var diagnosis = BuildKnownFailureDiagnosis(
            result.StandardOutput,
            result.StandardError);

        return
            $"{description} failed.{Environment.NewLine}" +
            $"Executable        : {executable}{Environment.NewLine}" +
            $"Working directory : {directory}{Environment.NewLine}" +
            $"Command           : {FormatCommand(executable, arguments)}{Environment.NewLine}" +
            $"Exit code         : {result.ExitCode}{Environment.NewLine}" +
            $"Environment keys  : {environmentKeys}{Environment.NewLine}" +
            $"Standard input    : {(hadInput ? "supplied (content hidden)" : "<none>")}{Environment.NewLine}" +
            $"-------------------- stdout --------------------{Environment.NewLine}" +
            $"{stdout}{Environment.NewLine}" +
            $"-------------------- stderr --------------------{Environment.NewLine}" +
            $"{stderr}{Environment.NewLine}" +
            $"------------------------------------------------" +
            diagnosis;
    }

    private static string BuildKnownFailureDiagnosis(
        string standardOutput,
        string standardError)
    {
        var combinedOutput = standardOutput + Environment.NewLine + standardError;

        if (combinedOutput.Contains("42501", StringComparison.OrdinalIgnoreCase) ||
            combinedOutput.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            var schemaCreationFailure =
                combinedOutput.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase) ||
                combinedOutput.Contains("permission denied for database", StringComparison.OrdinalIgnoreCase);

            if (schemaCreationFailure)
            {
                return
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    $"==================== DIAGNOSIS ===================={Environment.NewLine}" +
                    $"[DATABASE PERMISSION] PostgreSQL rejected schema creation.{Environment.NewLine}" +
                    $"[CAUSE] PostgreSQL SQLSTATE 42501 means insufficient privilege.{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"[CAUSE] A required module schema may be missing, while nordiska_migrator " +
                    $"does not have database-wide CREATE permission.{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"[ADVICE] Nordiska.DevSetup normally fixes this before migrations by asking " +
                    $"nordiska_bootstrap to ensure the approved schemas exist: banking, faq, " +
                    $"reporting, and inbox.{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"[ADVICE] If this message still appears, verify that the database was " +
                    $"initialized with POSTGRES_USER=nordiska_bootstrap and that the bootstrap " +
                    $"role still owns/controls nordiska_v2.{Environment.NewLine}" +
                    $"===================================================";
            }

            return
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"==================== DIAGNOSIS ===================={Environment.NewLine}" +
                $"[DATABASE PERMISSION] PostgreSQL rejected an operation because the active " +
                $"role lacks the required privilege.{Environment.NewLine}" +
                $"[CAUSE] PostgreSQL SQLSTATE 42501 means insufficient privilege.{Environment.NewLine}" +
                $"[ADVICE] Check the role, target schema/table, and GRANT/REVOKE rules shown " +
                $"immediately above this diagnosis.{Environment.NewLine}" +
                $"===================================================";
        }

        if (combinedOutput.Contains("28P01", StringComparison.OrdinalIgnoreCase) ||
            combinedOutput.Contains(
                "password authentication failed",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"==================== DIAGNOSIS ===================={Environment.NewLine}" +
                $"[DATABASE AUTH] PostgreSQL rejected the supplied username/password.{Environment.NewLine}" +
                $"[CAUSE] PostgreSQL SQLSTATE 28P01 means password authentication failed.{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"[CAUSE] A common cause in this local setup is that the passwords in infra/v2/.env " +
                $"do not match the passwords stored inside an already initialized Docker " +
                $"PostgreSQL volume.{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"Why this can happen:{Environment.NewLine}" +
                $"  - The .env file was recreated, restored, or previously regenerated.{Environment.NewLine}" +
                $"  - The PostgreSQL Docker volume already existed with older role passwords.{Environment.NewLine}" +
                $"  - Restarting the container does NOT update passwords of existing PostgreSQL roles.{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"[ADVICE] If this is disposable LOCAL development data, reset the database volume " +
                $"from the repository root:{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"[COMMAND] docker compose --project-name nordiska-v2 --env-file infra/v2/.env " +
                $"-f infra/v2/docker-compose.yml -f infra/v2/docker-compose.override.yml down -v{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"[ADVICE] Then run the Nordiska.DevSetup command again. The existing .env passwords " +
                $"will be reused when PostgreSQL is initialized.{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"[WARNING] 'down -v' deletes the local PostgreSQL volume and its data.{Environment.NewLine}" +
                $"[ADVICE] If the data must be kept, do NOT delete the volume. Instead, update the " +
                $"PostgreSQL role password to match .env, or restore the .env credentials " +
                $"that match the existing database.{Environment.NewLine}" +
                $"===================================================";
        }

        return string.Empty;
    }

    private static string GetRequiredPassword(
        IReadOnlyDictionary<string, string> passwords,
        string key)
    {
        if (!passwords.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException(
                $"Required generated password '{key}' was not found in the loaded .env values.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required generated password '{key}' is empty.");
        }

        return value;
    }

    private static string RedactSensitiveText(
        string text,
        IReadOnlyDictionary<string, string>? environment,
        string? input)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var valuesToRedact = new HashSet<string>(StringComparer.Ordinal);

        if (environment is not null)
        {
            foreach (var entry in environment)
            {
                if (LooksSensitive(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
                    valuesToRedact.Add(entry.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(input))
        {
            try
            {
                var inputSecrets = JsonSerializer.Deserialize<Dictionary<string, string>>(input);

                if (inputSecrets is not null)
                {
                    foreach (var value in inputSecrets.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                            valuesToRedact.Add(value);
                    }
                }
            }
            catch (JsonException)
            {
                // The input is never printed. Failure to parse it only means
                // there are no additional values available for output redaction.
            }
        }

        var redacted = text;

        foreach (var value in valuesToRedact.OrderByDescending(value => value.Length))
            redacted = redacted.Replace(value, "***REDACTED***", StringComparison.Ordinal);

        return redacted;
    }

    private static bool LooksSensitive(string key)
    {
        return key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("CONNECTIONSTRING", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatCommand(string executable, IEnumerable<string> arguments)
    {
        return string.Join(
            " ",
            new[] { executable }.Concat(arguments.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
            return "\"\"";

        if (!argument.Any(char.IsWhiteSpace) && !argument.Contains('"'))
            return argument;

        return "\"" + argument.Replace("\"", "\\\"") + "\"";
    }

    private static void PrintProcessOutput(string name, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        ConsoleUi.WriteLine($"--- {name} ---");
        ConsoleUi.WriteLine(output.TrimEnd());
        ConsoleUi.WriteLine($"--- end {name} ---");
    }

    private static string FormatOutput(string output)
    {
        return string.IsNullOrWhiteSpace(output)
            ? "<empty>"
            : output.TrimEnd();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort only. Preserve the original exception.
        }
    }

    public static async Task TestDatabaseAsync(
        Dictionary<string, string> passwords)
    {
        ConsoleUi.WriteLine();
        ConsoleUi.WriteLine("[DATABASE TEST] Testing API database connection...");

        var connectionString = CreateConnectionString(
            "nordiska_api",
            GetRequiredPassword(passwords, "NORDISKA_API_PASSWORD"));

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var command = dataSource.CreateCommand("SELECT 1;");

            // Opens a connection, authenticates, and executes the query.
            var result = await command.ExecuteScalarAsync();

            if (result is not int value || value != 1)
            {
                throw new InvalidOperationException(
                    $"Database test returned an unexpected result. " +
                    $"Expected Int32 value 1, got '{result ?? "<null>"}' " +
                    $"({result?.GetType().FullName ?? "no type"}).");
            }

            ConsoleUi.WriteLine(
                "[DATABASE TEST] Passed: API role connected and executed SELECT 1.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Database connectivity test failed for the local API role. " +
                "Target: Host=127.0.0.1;Port=5433;Database=nordiska_v2;" +
                "Username=nordiska_api. The password is intentionally hidden. " +
                $"{exception.GetType().Name}: {exception.Message}",
                exception);
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
