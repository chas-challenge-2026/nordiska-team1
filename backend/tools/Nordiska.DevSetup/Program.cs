using System.Security.Cryptography;

namespace Nordiska.DevSetup;

/// <summary>
/// WORK IN PROGRESS 
/// 
/// </summary>
 
internal static class Program
{
    private static readonly string[] PasswordVariables =
    [
        "POSTGRES_BOOTSTRAP_PASSWORD",
        "NORDISKA_MIGRATOR_PASSWORD",
        "NORDISKA_API_PASSWORD",
        "NORDISKA_REPORTING_WORKER_PASSWORD"
    ];


     private static int Main(string[] args)
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

            CreateEnv(root);

            Console.WriteLine(
                "Local database ENV created!");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"ENV setup failed: {exception.Message}");

            return 1;
        }
    }


     private static string GeneratePassword()
    {
        var bytes = new byte[32];

        RandomNumberGenerator.Fill(bytes);

        return Convert.ToHexString(bytes);
    }


     private static string GetEnvPath(string root)
    {
        return Path.Combine(
            root,
            "infra",
            "v2",
            ".env");
    }


     private static void CreateEnv(string root)
    {
        var path = GetEnvPath(root);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "Could not find ENV directory.");

        Directory.CreateDirectory(directory);

        using var file = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write);

        using var writer = new StreamWriter(file);

        foreach (var variable in PasswordVariables)
        {
            writer.WriteLine(
                $"{variable}={GeneratePassword()}");
        }
    }
}