using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
namespace Nordiska.DevSetup;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine(
                "Pass the repository root as the first argument.");

            return 1;
        }

        try
        {
            var root = Path.GetFullPath(args[0]);

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