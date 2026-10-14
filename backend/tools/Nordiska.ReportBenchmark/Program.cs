using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Npgsql;
using NpgsqlTypes;

namespace Nordiska.ReportBenchmark;

internal static class Program
{
    private sealed record BenchmarkOptions(
        string? RepoRoot,
        string? SigningMode,
        int? WorkerCount,
        int Limit,
        int Year,
        string DocumentType,
        string? Database,
        int Concurrency,
        bool CleanBeforeRun,
        bool NoDeploy,
        int PollIntervalMs,
        int TimeoutMinutes,
        bool ExportProfile);

    private sealed record WorkerContainerConfig(
        string ContainerId,
        string? Database,
        string SigningMode);

    private record struct JobCounts(int Pending, int Processing, int Completed, int Failed)
    {
        public readonly int Incomplete => Pending + Processing;
    }

    private sealed record JobLatencyStats(
        double AvgProcSec,
        double MinProcSec,
        double MaxProcSec,
        double P50ProcSec,
        double P95ProcSec,
        double P99ProcSec,
        double AvgQueueWaitSec,
        DateTimeOffset? MinStartedAt,
        DateTimeOffset? MaxCompletedAt);

    private sealed record AggregatePhaseMetrics(
        int TotalJobs,
        double TotalWallSec,
        double SubmissionWallSec,
        double SubmissionAvgMs,
        double SubmissionP50Ms,
        double SubmissionP95Ms,
        double QueueWaitAvgMs,
        double QueueWaitP50Ms,
        double QueueWaitP95Ms,
        double QueueWaitSumSec,
        double ClaimSumSec,
        double ClaimAvgMs,
        double ClaimP50Ms,
        double ClaimP95Ms,
        double LoadSnapshotSumSec,
        double LoadSnapshotAvgMs,
        double LoadSnapshotP50Ms,
        double LoadSnapshotP95Ms,
        double JsonMappingSumSec,
        double JsonMappingAvgMs,
        double JsonMappingP50Ms,
        double JsonMappingP95Ms,
        double NativeTotalSumSec,
        double NativeTotalAvgMs,
        double NativeTotalP50Ms,
        double NativeTotalP95Ms,
        double NativeIngestSumSec,
        double NativeIngestAvgMs,
        double NativeIngestP50Ms,
        double NativeIngestP95Ms,
        double NativeLayoutSumSec,
        double NativeLayoutAvgMs,
        double NativeLayoutP50Ms,
        double NativeLayoutP95Ms,
        double NativeRenderSumSec,
        double NativeRenderAvgMs,
        double NativeRenderP50Ms,
        double NativeRenderP95Ms,
        double NativeSignSumSec,
        double NativeSignAvgMs,
        double NativeSignP50Ms,
        double NativeSignP95Ms,
        double NativeSignerCallSumSec,
        double NativeSignerCallAvgMs,
        double NativeSignerCallP50Ms,
        double NativeSignerCallP95Ms,
        double ManagedHashSumSec,
        double ManagedHashAvgMs,
        double ManagedHashP50Ms,
        double ManagedHashP95Ms,
        double StorageSaveSumSec,
        double StorageSaveAvgMs,
        double StorageSaveP50Ms,
        double StorageSaveP95Ms,
        double DbCompleteSumSec,
        double DbCompleteAvgMs,
        double DbCompleteP50Ms,
        double DbCompleteP95Ms,
        double WorkerTotalSumSec,
        double WorkerTotalAvgMs,
        double WorkerTotalP50Ms,
        double WorkerTotalP95Ms);

    public static async Task<int> Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(a, "help", StringComparison.OrdinalIgnoreCase)))
        {
            PrintHelp();
            return 0;
        }

        BenchmarkOptions options;
        try
        {
            options = ParseArgs(args);
        }
        catch (ArgumentException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("Use --help to view available options.");
            return 1;
        }

        var repoRoot = options.RepoRoot ?? FindRepoRoot();
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[ERROR] Could not locate the repository root. Pass --root <path>.");
            Console.ResetColor();
            return 1;
        }

        var envPath = Path.Combine(repoRoot, "infra", "v2", ".env");
        if (!File.Exists(envPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Environment file not found at: {envPath}");
            Console.Error.WriteLine("Make sure the infra/v2 environment is configured.");
            Console.ResetColor();
            return 1;
        }

        var envVars = ReadEnv(envPath);
        var targetDatabase = !string.IsNullOrWhiteSpace(options.Database)
            ? options.Database
            : envVars.GetValueOrDefault("NORDISKA_DATABASE", "nordiska_synthetic");

        string? requestedSigning = !string.IsNullOrWhiteSpace(options.SigningMode)
            ? NormalizeSigningMode(options.SigningMode)
            : null;

        // 1. Worker Deployment & Validation
        if (!options.NoDeploy)
        {
            int initialWorkers = await GetActiveWorkerCountAsync(repoRoot);
            int targetScale = options.WorkerCount ?? (initialWorkers > 0 ? initialWorkers : 1);

            // Clean database & storage volume before run (always enabled by default)
            if (options.CleanBeforeRun)
            {
                Console.Write("[SETUP] Cleaning database & storage volume... ");
                if (initialWorkers > 0)
                {
                    await StopReportingWorkersAsync(repoRoot);
                }

                var bootstrapPassword = envVars.GetValueOrDefault("POSTGRES_BOOTSTRAP_PASSWORD");
                await CleanPreviousReportsAsync("127.0.0.1", 5433, targetDatabase, bootstrapPassword);
                await CleanDocumentVolumeAsync(repoRoot);
                Console.WriteLine("done.");
            }

            // Determine signing mode to deploy
            string signingToDeploy = requestedSigning ?? GetCurrentSigningMode(envVars);
            var (enableSigning, signerMode) = ResolveSigningConfig(signingToDeploy);
            var curEnable = envVars.GetValueOrDefault("NORDISKA_PDF_ENABLE_SIGNING", "0");
            var curMode = envVars.GetValueOrDefault("PDF_SIGNER_MODE", "plain");
            var curDb = envVars.GetValueOrDefault("NORDISKA_DATABASE", "nordiska_synthetic");

            bool envChanged = false;
            if (curEnable != enableSigning || curMode != signerMode)
            {
                UpdateEnvFile(envPath, "NORDISKA_PDF_ENABLE_SIGNING", enableSigning);
                UpdateEnvFile(envPath, "PDF_SIGNER_MODE", signerMode);
                envVars["NORDISKA_PDF_ENABLE_SIGNING"] = enableSigning;
                envVars["PDF_SIGNER_MODE"] = signerMode;
                envChanged = true;
            }

            if (curDb != targetDatabase)
            {
                UpdateEnvFile(envPath, "NORDISKA_DATABASE", targetDatabase);
                envVars["NORDISKA_DATABASE"] = targetDatabase;
                envChanged = true;
            }

            int currentWorkers = await GetActiveWorkerCountAsync(repoRoot);
            if (envChanged || options.WorkerCount.HasValue || options.CleanBeforeRun || currentWorkers != targetScale)
            {
                Console.Write($"[SETUP] Deploying {targetScale} workers ({signingToDeploy.ToUpperInvariant()})... ");
                await DeployAndScaleWorkersAsync(repoRoot, targetScale);
                Console.WriteLine("done.");
            }
        }
        else
        {
            if (options.CleanBeforeRun)
            {
                Console.Write("[SETUP] Cleaning database & storage volume... ");
                var bootstrapPassword = envVars.GetValueOrDefault("POSTGRES_BOOTSTRAP_PASSWORD");
                await CleanPreviousReportsAsync("127.0.0.1", 5433, targetDatabase, bootstrapPassword);
                await CleanDocumentVolumeAsync(repoRoot);
                Console.WriteLine("done.");
            }
        }

        // Validate running container configuration
        var workerConfigs = await InspectRunningWorkersAsync(repoRoot);
        if (workerConfigs.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[ERROR] No running 'reporting-worker' containers discovered.");
            Console.Error.WriteLine("Start the worker using docker compose or omit --no-deploy to auto-deploy.");
            Console.ResetColor();
            return 1;
        }

        var sampleWorker = workerConfigs[0];
        if (!string.Equals(sampleWorker.Database, targetDatabase, StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Configuration mismatch: runner database is '{targetDatabase}', but running worker containers are connected to '{sampleWorker.Database}'.");
            if (options.NoDeploy)
            {
                Console.Error.WriteLine("Run without --no-deploy to align worker environment, or specify --database matching the running worker.");
            }
            Console.ResetColor();
            return 1;
        }

        string effectiveSigning = sampleWorker.SigningMode;
        if (!string.IsNullOrWhiteSpace(requestedSigning) &&
            !string.Equals(requestedSigning, effectiveSigning, StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Signing mismatch: user requested '{requestedSigning.ToUpperInvariant()}', but running worker containers are running '{effectiveSigning.ToUpperInvariant()}'.");
            if (options.NoDeploy)
            {
                Console.Error.WriteLine("Re-run without --no-deploy to redeploy workers with requested signing mode.");
            }
            Console.ResetColor();
            return 1;
        }

        int activeWorkers = workerConfigs.Count;

        var apiPassword = envVars.GetValueOrDefault("NORDISKA_API_PASSWORD");
        if (string.IsNullOrWhiteSpace(apiPassword))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[ERROR] NORDISKA_API_PASSWORD is not set in infra/v2/.env.");
            Console.ResetColor();
            return 1;
        }

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 5433,
            Database = targetDatabase,
            Username = "nordiska_api",
            Password = apiPassword
        }.ConnectionString;

        try
        {
            await using var testConn = new NpgsqlConnection(connectionString);
            await testConn.OpenAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Failed to connect to PostgreSQL at 127.0.0.1:5433 ({targetDatabase}): {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        // Setup real DI services for Domain Modules
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReportingDatabase"] = connectionString,
                ["ConnectionStrings:BankingDatabase"] = connectionString,
                ["ReportDocumentStorage:RootPath"] = "/var/lib/nordiska/report-documents"
            })
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        serviceCollection.AddReportingModuleInfrastructure(config);
        serviceCollection.AddDbContext<BankingDbContext>(opt => opt.UseNpgsql(connectionString));

        using var serviceProvider = serviceCollection.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var bankingDb = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var query = bankingDb.SavingsAccounts
            .AsNoTracking()
            .Where(a => a.Status == "active")
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.CustomerId, a.AccountNumber, a.InterestRate });

        var targetAccounts = options.Limit > 0
            ? await query.Take(options.Limit).ToListAsync()
            : await query.ToListAsync();

        if (targetAccounts.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] No active accounts found in database '{targetDatabase}'.");
            Console.ResetColor();
            return 1;
        }

        bool runStatements = options.DocumentType is "all" or "statements" or "statement";
        bool runTax = options.DocumentType is "all" or "tax";

        int targetStatements = runStatements ? targetAccounts.Count : 0;
        int targetTaxReports = runTax ? targetAccounts.Count(a => a.InterestRate > 0) : 0;
        int totalTargetPdfs = targetStatements + targetTaxReports;

        Console.WriteLine($"[BATCH] Target: {totalTargetPdfs:N0} PDFs ({targetAccounts.Count:N0} accounts) | Database: {targetDatabase} | Signing: {effectiveSigning.ToUpperInvariant()} | Workers: {activeWorkers}");

        var fromDate = new DateOnly(options.Year, 1, 1);
        var toDate = new DateOnly(options.Year, 12, 31);

        var statementJobIds = new ConcurrentBag<long>();
        var taxReportJobIds = new ConcurrentBag<long>();
        var submissionErrors = new ConcurrentBag<string>();
        var submissionLatencies = new ConcurrentBag<double>();
        int alreadyCompletedAtSubmit = 0;

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        var runStartedAt = DateTimeOffset.UtcNow;
        var submitSw = Stopwatch.StartNew();
        int submittedAccounts = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.Concurrency),
            CancellationToken = cts.Token
        };

        bool cancelledDuringSubmission = false;
        try
        {
            await Parallel.ForEachAsync(targetAccounts, parallelOptions, async (account, ct) =>
            {
                long tAccount = Stopwatch.GetTimestamp();
                try
                {
                    using var innerScope = serviceProvider.CreateScope();

                    if (runStatements)
                    {
                        var statementService = innerScope.ServiceProvider.GetRequiredService<IAccountStatementService>();
                        var stmtResult = await statementService.RequestAsync(
                            account.CustomerId,
                            account.Id,
                            fromDate,
                            toDate,
                            ct);

                        statementJobIds.Add(stmtResult.JobId);
                        if (stmtResult.Status == "Completed")
                        {
                            Interlocked.Increment(ref alreadyCompletedAtSubmit);
                        }
                    }

                    if (runTax && account.InterestRate > 0)
                    {
                        var taxService = innerScope.ServiceProvider.GetRequiredService<IAnnualTaxReportService>();
                        var taxResult = await taxService.RequestAsync(
                            account.CustomerId,
                            account.Id,
                            options.Year,
                            ct);

                        taxReportJobIds.Add(taxResult.JobId);
                        if (taxResult.Status == "Completed")
                        {
                            Interlocked.Increment(ref alreadyCompletedAtSubmit);
                        }
                    }

                    submissionLatencies.Add(Stopwatch.GetElapsedTime(tAccount).TotalMilliseconds);

                    var currentDone = Interlocked.Increment(ref submittedAccounts);
                    if (currentDone % 100 == 0 || currentDone == targetAccounts.Count)
                    {
                        Console.Write($"\r[BATCH] Queuing requests: {currentDone:N0}/{targetAccounts.Count:N0} accounts... \u001b[K");
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    submissionErrors.Add($"Account {account.Id}: {ex.Message}");
                }
            });
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            cancelledDuringSubmission = true;
        }

        submitSw.Stop();

        var distinctStmtIds = statementJobIds.Distinct().ToArray();
        var distinctTaxIds = taxReportJobIds.Distinct().ToArray();
        var totalSubmittedJobs = distinctStmtIds.Length + distinctTaxIds.Length;

        if (cancelledDuringSubmission)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[CANCELLED] Submission aborted by user. Queued {totalSubmittedJobs} jobs before cancel.");
            Console.ResetColor();
            return 130;
        }

        await using var statusConnection = new NpgsqlConnection(connectionString);
        await statusConnection.OpenAsync(cts.Token);

        var initialCounts = await FetchJobCountsAsync(statusConnection, distinctStmtIds, distinctTaxIds, cts.Token);
        var drainSw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMinutes(options.TimeoutMinutes);

        JobCounts latestCounts = initialCounts;
        bool timedOut = false;
        bool cancelledDuringDrain = false;



        while (drainSw.Elapsed < timeout && !cts.IsCancellationRequested)
        {
            latestCounts = await FetchJobCountsAsync(statusConnection, distinctStmtIds, distinctTaxIds, cts.Token);
            var completed = latestCounts.Completed;
            var processing = latestCounts.Processing;
            var pending = latestCounts.Pending;
            var failed = latestCounts.Failed;

            var totalElapsed = submitSw.Elapsed + drainSw.Elapsed;
            var newlyCompleted = Math.Max(0, completed - alreadyCompletedAtSubmit);
            var rate = totalElapsed.TotalSeconds > 0.5 ? newlyCompleted / totalElapsed.TotalSeconds : 0.0;
            var remaining = Math.Max(0, totalSubmittedJobs - completed - failed);
            var etaSec = rate > 0.0 ? remaining / rate : 0.0;

            int barWidth = 15;
            double fraction = totalSubmittedJobs > 0 ? (double)completed / totalSubmittedJobs : 0.0;
            int filled = (int)Math.Clamp(fraction * barWidth, 0, barWidth);
            string bar = new string('█', filled) + new string('░', barWidth - filled);

            Console.Write($"\r[BATCH] Rendering: {completed:N0}/{totalSubmittedJobs:N0} [{bar}] {fraction * 100.0:F1}% | {rate:F1} pdf/s | {totalElapsed:mm\\:ss} \u001b[K");

            if (pending == 0 && processing == 0)
            {
                break;
            }

            try
            {
                await Task.Delay(options.PollIntervalMs, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelledDuringDrain = true;
                break;
            }
        }

        drainSw.Stop();
        Console.CancelKeyPress -= cancelHandler;
        Console.WriteLine();

        if (drainSw.Elapsed >= timeout && latestCounts.Incomplete > 0)
        {
            timedOut = true;
        }

        var lastCompleted = latestCounts.Completed;
        var failedCount = latestCounts.Failed;

        // Overall batch timer measures total wall clock time from START of run to END
        var totalWallClockSec = submitSw.Elapsed.TotalSeconds + drainSw.Elapsed.TotalSeconds;
        var newlyGeneratedTotal = Math.Max(0, lastCompleted - alreadyCompletedAtSubmit);
        var overallThroughput = totalWallClockSec > 0 ? newlyGeneratedTotal / totalWallClockSec : 0.0;

        // Per-job latency metrics from database timestamps
        JobLatencyStats? latencyStats = null;
        try
        {
            latencyStats = await FetchJobLatencyStatsAsync(statusConnection, distinctStmtIds, distinctTaxIds, runStartedAt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Failed to compute detailed per-job latency statistics: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("======================= BENCHMARK RESULTS =======================");
        Console.WriteLine($"Status                  : {(timedOut ? "TIMED OUT" : cancelledDuringDrain ? "CANCELLED" : "SUCCESS")}");
        Console.WriteLine($"Total Documents         : {totalTargetPdfs:N0} PDFs");
        Console.WriteLine($"Successful Documents    : {lastCompleted:N0} PDFs ({(failedCount == 0 ? "0 failed" : $"{failedCount} failed")})");
        if (alreadyCompletedAtSubmit > 0)
        {
            Console.WriteLine($"  - Freshly Rendered    : {newlyGeneratedTotal:N0} PDFs");
            Console.WriteLine($"  - Reused From Cache   : {alreadyCompletedAtSubmit:N0} PDFs");
        }
        Console.WriteLine("-----------------------------------------------------------------");
        Console.WriteLine($"DURATION (START to END) : {TimeSpan.FromSeconds(totalWallClockSec):mm\\:ss\\.ff} ({totalWallClockSec:F2}s)");
        Console.WriteLine($"OVERALL THROUGHPUT      : {overallThroughput:F2} PDFs / sec");
        Console.WriteLine("-----------------------------------------------------------------");
        if (latencyStats is not null && latencyStats.MaxCompletedAt.HasValue && latencyStats.MinStartedAt.HasValue)
        {
            var dbSpan = latencyStats.MaxCompletedAt.Value - latencyStats.MinStartedAt.Value;
            var dbSpanSec = Math.Max(0.001, dbSpan.TotalSeconds);
            var dbThroughput = newlyGeneratedTotal / dbSpanSec;

            Console.WriteLine($"Per-Doc Render Time (p50): {latencyStats.P50ProcSec * 1000.0:F1} ms");
            Console.WriteLine($"Per-Doc Render Time (p95): {latencyStats.P95ProcSec * 1000.0:F1} ms");
            Console.WriteLine($"Per-Doc Render Time (Avg): {latencyStats.AvgProcSec * 1000.0:F1} ms");
            Console.WriteLine($"Active Worker Span       : {dbSpanSec:F2}s ({dbThroughput:F2} PDFs/s clock-measured)");
            Console.WriteLine("-----------------------------------------------------------------");
        }
        Console.WriteLine($"Configuration           : {activeWorkers} workers, {effectiveSigning.ToUpperInvariant()} signing (Libharu)");
        Console.WriteLine("=================================================================");

        AggregatePhaseMetrics? aggregateMetrics = null;
        try
        {
            aggregateMetrics = await FetchAggregateTelemetryAsync(
                statusConnection,
                distinctStmtIds,
                distinctTaxIds,
                runStartedAt,
                submissionLatencies,
                submitSw.Elapsed.TotalSeconds,
                totalWallClockSec);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Failed to fetch pipeline phase telemetry: {ex.Message}");
        }

        if (aggregateMetrics is not null && aggregateMetrics.TotalJobs > 0)
        {
            PrintPhaseBreakdown(aggregateMetrics);

            if (options.ExportProfile)
            {
                await ExportProfilesAsync(repoRoot, aggregateMetrics);
            }
        }

        if (aggregateMetrics is null || aggregateMetrics.TotalJobs == 0)
        {
            Console.WriteLine("[INFO] Worker phase telemetry unavailable; throughput and job latency results remain available. Phase profiles were not exported.");
        }

        if (failedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("[FAILURES DETECTED] Sample failed job errors:");
            var failures = await FetchSampleFailuresAsync(statusConnection, distinctStmtIds, distinctTaxIds);
            foreach (var f in failures)
            {
                Console.WriteLine($"  - [{f.JobType} #{f.Id}] {f.Error}");
            }
            Console.ResetColor();
            return 2;
        }

        if (timedOut)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("\n[FAILURE] Benchmark timed out before all jobs completed.");
            Console.ResetColor();
            return 1;
        }

        if (cancelledDuringDrain)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine("\n[CANCELLED] Benchmark drain aborted by user before completion.");
            Console.ResetColor();
            return 130;
        }

        if (!submissionErrors.IsEmpty)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("\n[FAILURE] Submission errors occurred during benchmark.");
            Console.ResetColor();
            return 1;
        }

        if (latestCounts.Incomplete > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\n[FAILURE] Benchmark ended with {latestCounts.Incomplete} incomplete jobs.");
            Console.ResetColor();
            return 1;
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Nordiska PDF Reporting Benchmark
Benchmarking tool measuring the real application path from DB to PDF.

USAGE:
  dotnet run --project backend/tools/Nordiska.ReportBenchmark -- [OPTIONS]
  ./report-benchmark [OPTIONS]

SIGNING OPTIONS:
  --signing <mode>, -s <mode>
      Configure cryptographic signing mode for reporting workers:
        unsigned, none, off, 0   : Pure C++ Libharu rendering without signing.
        plain, cms, signed       : CMS/PKCS#7 cryptographic signing via SoftHSM.
        timestamp, tsa           : CMS/PKCS#7 signing + RFC 3161 TSA timestamp.
      (Default: current mode in infra/v2/.env)

  --no-signing
      Shorthand for --signing unsigned.

WORKER DEPLOYMENT OPTIONS:
  --workers <N>, -w <N>
      Deploys and scales 'reporting-worker' docker containers to N instances.
      Waits for readiness before starting benchmark.
      Example: --workers 1, --workers 2, --workers 4, --workers 8

  --no-deploy
      Skips Docker Compose container scaling/restart. Validates that running
      worker containers match requested database and signing configuration.

WORKLOAD OPTIONS:
  --limit <N>, -l <N>
      Limit benchmark to first N active accounts (e.g. --limit 100).
      Default: all active accounts.

  --type <all|statements|tax>, -t <type>
      Document type to benchmark:
        all        : Account statements AND Annual tax reports (default).
        statements : Account statements only.
        tax        : Annual tax reports only.

  --clean, --clear-jobs, --fresh
      Coordinated cleanup: stops workers, wipes database reporting tables,
      purges stored PDF files from the Docker volume, and restarts workers (default: enabled).

  --no-clean, --preserve
      Preserves existing reports in DB and skips wiping.

  --year <YYYY>, -y <YYYY>
      Reporting year for statement period and tax calculations (default: 2025).

  --database <name>, --db <name>
      Postgres database name (default: NORDISKA_DATABASE in .env or nordiska_synthetic).

  --concurrency <N>, -c <N>
      Parallel streams for submitting requests via .NET DI (default: 16).

  --timeout-minutes <N>
      Maximum minutes to wait for queue drain (default: 60).

  --poll-ms <N>
      Polling interval in milliseconds to check DB progress (default: 1000).

  --flamegraph, --profile
      Emits benchmark-flamegraph.txt (collapsed stack) and benchmark.speedscope.json
      for interactive flamegraph visualization when worker telemetry is available (default: enabled).

  --no-flamegraph, --no-profile
      Disables flamegraph profile export.

  --root <path>
      Path to repo root. Auto-detected if omitted.

EXAMPLES:
  # Quick 100-account test without signing using 2 workers (cleans old reports automatically)
  ./report-benchmark --limit 100 --signing none --workers 2

  # Compare 1 vs 4 vs 8 workers with timestamp signing
  ./report-benchmark --signing timestamp --workers 4 --limit 200

  # Run against existing running deployment without scaling
  ./report-benchmark --no-deploy --limit 50
");
    }

    private static BenchmarkOptions ParseArgs(string[] args)
    {
        string? root = null;
        string? signing = null;
        int? workers = null;
        int limit = 0;
        int year = 2025;
        string docType = "all";
        string? database = null;
        int concurrency = 16;
        bool clean = true;
        bool noDeploy = false;
        int pollMs = 1000;
        int timeoutMinutes = 60;
        bool exportProfile = true;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (MatchOpt(arg, "--root"))
            {
                root = RequireValue(args, ref i, "--root");
            }
            else if (MatchOpt(arg, "--signing", "-s"))
            {
                signing = RequireValue(args, ref i, "--signing");
            }
            else if (string.Equals(arg, "--no-signing", StringComparison.OrdinalIgnoreCase))
            {
                signing = "unsigned";
            }
            else if (MatchOpt(arg, "--workers", "-w"))
            {
                workers = int.Parse(RequireValue(args, ref i, "--workers"));
            }
            else if (MatchOpt(arg, "--limit", "-l"))
            {
                limit = int.Parse(RequireValue(args, ref i, "--limit"));
            }
            else if (MatchOpt(arg, "--type", "-t"))
            {
                docType = RequireValue(args, ref i, "--type").ToLowerInvariant();
            }
            else if (MatchOpt(arg, "--year", "-y"))
            {
                year = int.Parse(RequireValue(args, ref i, "--year"));
            }
            else if (MatchOpt(arg, "--database", "--db"))
            {
                database = RequireValue(args, ref i, "--database");
            }
            else if (MatchOpt(arg, "--concurrency", "-c"))
            {
                concurrency = int.Parse(RequireValue(args, ref i, "--concurrency"));
            }
            else if (MatchOpt(arg, "--clean", "--clear-jobs", "--fresh"))
            {
                clean = true;
            }
            else if (MatchOpt(arg, "--no-clean", "--preserve"))
            {
                clean = false;
            }
            else if (MatchOpt(arg, "--no-deploy"))
            {
                noDeploy = true;
            }
            else if (MatchOpt(arg, "--poll-ms"))
            {
                pollMs = int.Parse(RequireValue(args, ref i, "--poll-ms"));
            }
            else if (MatchOpt(arg, "--timeout-minutes"))
            {
                timeoutMinutes = int.Parse(RequireValue(args, ref i, "--timeout-minutes"));
            }
            else if (MatchOpt(arg, "--flamegraph", "--profile"))
            {
                exportProfile = true;
            }
            else if (MatchOpt(arg, "--no-flamegraph", "--no-profile"))
            {
                exportProfile = false;
            }
            else if (!arg.StartsWith('-') && root is null)
            {
                if (Directory.Exists(arg) && (Directory.Exists(Path.Combine(arg, "infra")) || File.Exists(Path.Combine(arg, "README.md"))))
                {
                    root = arg;
                }
                else
                {
                    throw new ArgumentException($"Unknown argument '{arg}'.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        return new BenchmarkOptions(
            root,
            signing,
            workers,
            limit,
            year,
            docType,
            database,
            concurrency,
            clean,
            noDeploy,
            pollMs,
            timeoutMinutes,
            exportProfile);
    }

    private static bool MatchOpt(string arg, params string[] options) =>
        options.Any(opt => string.Equals(arg, opt, StringComparison.OrdinalIgnoreCase));

    private static string RequireValue(string[] args, ref int i, string optionName)
    {
        if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
        {
            throw new ArgumentException($"Option '{optionName}' requires a value.");
        }
        return args[++i];
    }

    private static string NormalizeSigningMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "unsigned" or "none" or "off" or "no-signing" or "0" => "unsigned",
            "plain" or "signed" or "cms" or "1" => "plain",
            "timestamp" or "tsa" or "2" => "timestamp",
            _ => throw new ArgumentException($"Unknown signing mode '{mode}'. Choose: unsigned, plain, timestamp.")
        };
    }

    private static (string EnableSigning, string SignerMode) ResolveSigningConfig(string mode)
    {
        return NormalizeSigningMode(mode) switch
        {
            "unsigned" => ("0", "plain"),
            "plain" => ("1", "plain"),
            "timestamp" => ("1", "timestamp"),
            _ => ("0", "plain")
        };
    }

    private static string GetCurrentSigningMode(Dictionary<string, string> env)
    {
        var enable = env.GetValueOrDefault("NORDISKA_PDF_ENABLE_SIGNING", "0");
        var mode = env.GetValueOrDefault("PDF_SIGNER_MODE", "plain");
        return enable == "1" ? (mode == "timestamp" ? "timestamp" : "plain") : "unsigned";
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "infra", "v2", ".env")) ||
                File.Exists(Path.Combine(dir.FullName, "backend", "np.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static Dictionary<string, string> ReadEnv(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return dict;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var idx = trimmed.IndexOf('=');
            if (idx > 0)
            {
                var key = trimmed[..idx].Trim();
                var val = trimmed[(idx + 1)..].Trim();
                dict[key] = val;
            }
        }
        return dict;
    }

    private static void UpdateEnvFile(string envPath, string key, string value)
    {
        var lines = File.Exists(envPath) ? File.ReadAllLines(envPath).ToList() : new List<string>();
        bool replaced = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                replaced = true;
                break;
            }
        }
        if (!replaced)
        {
            lines.Add($"{key}={value}");
        }
        File.WriteAllLines(envPath, lines);
    }

    private static async Task<int> GetActiveWorkerCountAsync(string repoRoot)
    {
        try
        {
            var result = await RunProcessAsync(
                "docker",
                ["compose", "-f", "infra/v2/docker-compose.yml", "-f", "infra/v2/docker-compose.override.yml", "ps", "--status", "running", "-q", "reporting-worker"],
                repoRoot);

            if (result.ExitCode != 0) return 0;
            var lines = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return lines.Length;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task StopReportingWorkersAsync(string repoRoot)
    {
        await RunProcessAsync(
            "docker",
            ["compose", "-f", "infra/v2/docker-compose.yml", "-f", "infra/v2/docker-compose.override.yml", "stop", "reporting-worker"],
            repoRoot);
    }

    private static async Task DeployAndScaleWorkersAsync(string repoRoot, int scale)
    {
        var result = await RunProcessAsync(
            "docker",
            ["compose", "-f", "infra/v2/docker-compose.yml", "-f", "infra/v2/docker-compose.override.yml", "up", "-d", "--scale", $"reporting-worker={scale}", "--no-deps", "reporting-worker"],
            repoRoot);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to scale workers: {result.Error}");
        }

        // Readiness check: poll until all expected replicas are running
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(30);
        while (sw.Elapsed < timeout)
        {
            int running = await GetActiveWorkerCountAsync(repoRoot);
            if (running == scale)
            {
                return;
            }
            await Task.Delay(250);
        }

        int finalRunning = await GetActiveWorkerCountAsync(repoRoot);
        if (finalRunning != scale)
        {
            throw new InvalidOperationException($"Worker readiness timeout: expected {scale} running replicas, but {finalRunning} are running after 30s.");
        }
    }

    private static async Task<List<WorkerContainerConfig>> InspectRunningWorkersAsync(string repoRoot)
    {
        var configs = new List<WorkerContainerConfig>();
        var psResult = await RunProcessAsync(
            "docker",
            ["compose", "-f", "infra/v2/docker-compose.yml", "-f", "infra/v2/docker-compose.override.yml", "ps", "--status", "running", "-q", "reporting-worker"],
            repoRoot);

        if (psResult.ExitCode != 0) return configs;
        var containerIds = psResult.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var id in containerIds)
        {
            var inspectResult = await RunProcessAsync(
                "docker",
                ["inspect", id, "--format", "{{json .Config.Env}}"],
                repoRoot);

            if (inspectResult.ExitCode != 0) continue;

            string[]? envArray = null;
            try
            {
                envArray = JsonSerializer.Deserialize<string[]>(inspectResult.Output);
            }
            catch { }

            if (envArray == null) continue;

            string? dbName = null;
            string enableSign = "0";
            string signerMode = "plain";

            foreach (var envEntry in envArray)
            {
                if (envEntry.StartsWith("ConnectionStrings__ReportingDatabase=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = envEntry["ConnectionStrings__ReportingDatabase=".Length..];
                    var m = Regex.Match(val, @"Database=([^;]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        dbName = m.Groups[1].Value.Trim();
                    }
                }
                else if (envEntry.StartsWith("NORDISKA_PDF_ENABLE_SIGNING=", StringComparison.OrdinalIgnoreCase))
                {
                    enableSign = envEntry["NORDISKA_PDF_ENABLE_SIGNING=".Length..].Trim();
                }
                else if (envEntry.StartsWith("PDF_SIGNER_MODE=", StringComparison.OrdinalIgnoreCase))
                {
                    signerMode = envEntry["PDF_SIGNER_MODE=".Length..].Trim();
                }
            }

            string mode = enableSign == "1" ? (signerMode.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ? "timestamp" : "plain") : "unsigned";
            configs.Add(new WorkerContainerConfig(id, dbName, mode));
        }

        return configs;
    }

    private static async Task CleanPreviousReportsAsync(string host, int port, string database, string? bootstrapPassword)
    {
        if (string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            throw new InvalidOperationException("POSTGRES_BOOTSTRAP_PASSWORD is required in infra/v2/.env for database cleanup.");
        }

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = "nordiska_bootstrap",
            Password = bootstrapPassword
        }.ConnectionString;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            TRUNCATE TABLE
                reporting.account_statement_jobs,
                reporting.tax_report_jobs,
                reporting.account_statement_documents,
                reporting.account_statement_entries,
                reporting.account_statements,
                reporting.tax_report_documents,
                reporting.tax_reports,
                reporting.generated_documents,
                reporting.legacy_tax_report_jobs,
                reporting.legacy_tax_reports
            CASCADE;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        // Optional telemetry is owned and provisioned by worker instrumentation.
        await using var metricsCleanup = new NpgsqlCommand(@"
            DO $$ BEGIN
                IF to_regclass('reporting.job_performance_metrics') IS NOT NULL THEN
                    TRUNCATE TABLE reporting.job_performance_metrics;
                END IF;
            END $$;", conn);
        await metricsCleanup.ExecuteNonQueryAsync();
    }

    private static async Task CleanDocumentVolumeAsync(string repoRoot)
    {
        var result = await RunProcessAsync(
            "docker",
            ["run", "--rm", "-v", "nordiska-v2_report_documents:/docs", "busybox", "sh", "-c", "rm -rf /docs/*"],
            repoRoot);

        if (result.ExitCode != 0)
        {
            Console.WriteLine($"[WARNING] Document volume cleanup exited with code {result.ExitCode}: {result.Error}");
        }
    }

    private static async Task<JobCounts> FetchJobCountsAsync(
        NpgsqlConnection connection,
        long[] distinctStmtIds,
        long[] distinctTaxIds,
        CancellationToken cancellationToken)
    {
        int pending = 0, processing = 0, completed = 0, failed = 0;

        if (distinctStmtIds.Length > 0)
        {
            const string sql = @"
                SELECT ""Status"", count(*)::int
                FROM reporting.account_statement_jobs
                WHERE ""Id"" = ANY(@ids)
                GROUP BY ""Status"";";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctStmtIds });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var status = reader.GetString(0);
                var count = reader.GetInt32(1);
                if (status == "Pending") pending += count;
                else if (status is "Processing" or "InProgress") processing += count;
                else if (status == "Completed") completed += count;
                else if (status == "Failed") failed += count;
            }
        }

        if (distinctTaxIds.Length > 0)
        {
            const string sql = @"
                SELECT ""Status"", count(*)::int
                FROM reporting.tax_report_jobs
                WHERE ""Id"" = ANY(@ids)
                GROUP BY ""Status"";";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctTaxIds });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var status = reader.GetString(0);
                var count = reader.GetInt32(1);
                if (status == "Pending") pending += count;
                else if (status is "Processing" or "InProgress") processing += count;
                else if (status == "Completed") completed += count;
                else if (status == "Failed") failed += count;
            }
        }

        return new JobCounts(pending, processing, completed, failed);
    }

    private static async Task<JobLatencyStats?> FetchJobLatencyStatsAsync(
        NpgsqlConnection connection,
        long[] distinctStmtIds,
        long[] distinctTaxIds,
        DateTimeOffset runStartedAt)
    {
        if (distinctStmtIds.Length == 0 && distinctTaxIds.Length == 0) return null;

        var sql = @"
            WITH target_jobs AS (
                SELECT ""StartedAt"", ""CompletedAt"", ""CreatedAt""
                FROM reporting.account_statement_jobs
                WHERE ""Id"" = ANY(@stmtIds) AND ""Status"" = 'Completed' AND ""StartedAt"" >= @runStartedAt AND ""CompletedAt"" IS NOT NULL
                UNION ALL
                SELECT ""StartedAt"", ""CompletedAt"", ""CreatedAt""
                FROM reporting.tax_report_jobs
                WHERE ""Id"" = ANY(@taxIds) AND ""Status"" = 'Completed' AND ""StartedAt"" >= @runStartedAt AND ""CompletedAt"" IS NOT NULL
            )
            SELECT
                COALESCE(AVG(EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))), 0)::double precision AS avg_proc,
                COALESCE(MIN(EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))), 0)::double precision AS min_proc,
                COALESCE(MAX(EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))), 0)::double precision AS max_proc,
                COALESCE(PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))), 0)::double precision AS p50_proc,
                COALESCE(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))), 0)::double precision AS p95_proc,
                COALESCE(PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))), 0)::double precision AS p99_proc,
                COALESCE(AVG(EXTRACT(EPOCH FROM (""StartedAt"" - ""CreatedAt""))), 0)::double precision AS avg_wait,
                MIN(""StartedAt"") AS min_started,
                MAX(""CompletedAt"") AS max_completed
            FROM target_jobs;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("@stmtIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctStmtIds });
        cmd.Parameters.Add(new NpgsqlParameter("@taxIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctTaxIds });
        cmd.Parameters.Add(new NpgsqlParameter("@runStartedAt", NpgsqlDbType.TimestampTz) { Value = runStartedAt });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var avgProc = reader.GetDouble(0);
            var minProc = reader.GetDouble(1);
            var maxProc = reader.GetDouble(2);
            var p50Proc = reader.GetDouble(3);
            var p95Proc = reader.GetDouble(4);
            var p99Proc = reader.GetDouble(5);
            var avgWait = reader.GetDouble(6);
            DateTimeOffset? minStarted = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7);
            DateTimeOffset? maxCompleted = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8);

            return new JobLatencyStats(
                avgProc, minProc, maxProc, p50Proc, p95Proc, p99Proc, avgWait, minStarted, maxCompleted);
        }

        return null;
    }

    private static async Task<List<(string JobType, long Id, string Error)>> FetchSampleFailuresAsync(
        NpgsqlConnection connection,
        long[] distinctStmtIds,
        long[] distinctTaxIds)
    {
        var result = new List<(string, long, string)>();

        if (distinctStmtIds.Length > 0)
        {
            const string sql = @"
                SELECT ""Id"", COALESCE(""LastError"", 'Unknown error')
                FROM reporting.account_statement_jobs
                WHERE ""Id"" = ANY(@ids) AND ""Status"" = 'Failed'
                LIMIT 5;";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctStmtIds });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(("Statement", reader.GetInt64(0), reader.GetString(1)));
        }

        if (distinctTaxIds.Length > 0)
        {
            const string sql = @"
                SELECT ""Id"", COALESCE(""LastError"", 'Unknown error')
                FROM reporting.tax_report_jobs
                WHERE ""Id"" = ANY(@ids) AND ""Status"" = 'Failed'
                LIMIT 5;";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctTaxIds });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(("TaxReport", reader.GetInt64(0), reader.GetString(1)));
        }

        return result;
    }

    private sealed record ProcessOutput(int ExitCode, string Output, string Error);

    private static async Task<ProcessOutput> RunProcessAsync(
        string executable,
        string[] args,
        string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start process {executable}.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        return new ProcessOutput(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private sealed record JobTelemetryRow(
        long JobId,
        string JobType,
        double QueueWaitMs,
        double ClaimMs,
        double LoadSnapshotMs,
        double JsonMappingMs,
        double NativeTotalMs,
        double NativeIngestMs,
        double NativeLayoutMs,
        double NativeRenderMs,
        double NativeSignMs,
        double NativeSignerCallMs,
        double ManagedHashMs,
        double StorageSaveMs,
        double DbCompleteMs,
        double TotalWorkerMs);

    private static double ComputePercentile(List<double> values, double percentile)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        int idx = (int)Math.Ceiling(percentile * values.Count) - 1;
        return values[Math.Clamp(idx, 0, values.Count - 1)];
    }

    private static async Task<AggregatePhaseMetrics?> FetchAggregateTelemetryAsync(
        NpgsqlConnection connection,
        long[] distinctStmtIds,
        long[] distinctTaxIds,
        DateTimeOffset runStartedAt,
        ConcurrentBag<double> submissionLatencies,
        double submitWallSec,
        double totalWallSec)
    {
        await using (var existsCommand = new NpgsqlCommand(
            "SELECT to_regclass('reporting.job_performance_metrics') IS NOT NULL", connection))
        {
            if (await existsCommand.ExecuteScalarAsync() is not true)
            {
                return null;
            }
        }

        var rows = new List<JobTelemetryRow>();
        var totalJobs = distinctStmtIds.Length + distinctTaxIds.Length;

        string sql;
        if (totalJobs > 0 && totalJobs <= 10000)
        {
            sql = @"
                WITH jobs_info AS (
                    SELECT 'statement' AS type, ""Id"", ""CreatedAt"", ""StartedAt"" FROM reporting.account_statement_jobs WHERE ""Id"" = ANY(@stmtIds)
                    UNION ALL
                    SELECT 'tax' AS type, ""Id"", ""CreatedAt"", ""StartedAt"" FROM reporting.tax_report_jobs WHERE ""Id"" = ANY(@taxIds)
                )
                SELECT
                    m.""JobId"", m.""JobType"",
                    COALESCE(EXTRACT(EPOCH FROM (COALESCE(j.""StartedAt"", m.""RecordedAt"") - j.""CreatedAt"")) * 1000.0, 0.0) AS ""QueueWaitMs"",
                    m.""ClaimMs"", m.""LoadSnapshotMs"", m.""JsonMappingMs"", m.""NativeTotalMs"",
                    m.""NativeIngestMs"", m.""NativeLayoutMs"", m.""NativeRenderMs"", m.""NativeSignMs"",
                    m.""NativeSignerCallMs"", m.""ManagedHashMs"", m.""StorageSaveMs"", m.""DbCompleteMs"",
                    m.""TotalWorkerMs""
                FROM reporting.job_performance_metrics m
                JOIN jobs_info j ON m.""JobId"" = j.""Id"" AND (m.""JobType"" = j.type OR LOWER(m.""JobType"") LIKE j.type || '%')
                ORDER BY m.""RecordedAt"" ASC;";
        }
        else
        {
            sql = @"
                WITH jobs_info AS (
                    SELECT 'statement' AS type, ""Id"", ""CreatedAt"", ""StartedAt"" FROM reporting.account_statement_jobs WHERE ""StartedAt"" >= @runStartedAt
                    UNION ALL
                    SELECT 'tax' AS type, ""Id"", ""CreatedAt"", ""StartedAt"" FROM reporting.tax_report_jobs WHERE ""StartedAt"" >= @runStartedAt
                )
                SELECT
                    m.""JobId"", m.""JobType"",
                    COALESCE(EXTRACT(EPOCH FROM (COALESCE(j.""StartedAt"", m.""RecordedAt"") - j.""CreatedAt"")) * 1000.0, 0.0) AS ""QueueWaitMs"",
                    m.""ClaimMs"", m.""LoadSnapshotMs"", m.""JsonMappingMs"", m.""NativeTotalMs"",
                    m.""NativeIngestMs"", m.""NativeLayoutMs"", m.""NativeRenderMs"", m.""NativeSignMs"",
                    m.""NativeSignerCallMs"", m.""ManagedHashMs"", m.""StorageSaveMs"", m.""DbCompleteMs"",
                    m.""TotalWorkerMs""
                FROM reporting.job_performance_metrics m
                JOIN jobs_info j ON m.""JobId"" = j.""Id"" AND (m.""JobType"" = j.type OR LOWER(m.""JobType"") LIKE j.type || '%')
                WHERE m.""RecordedAt"" >= @runStartedAt
                ORDER BY m.""RecordedAt"" ASC;";
        }

        await using var cmd = new NpgsqlCommand(sql, connection);
        if (totalJobs > 0 && totalJobs <= 10000)
        {
            cmd.Parameters.Add(new NpgsqlParameter("@stmtIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctStmtIds });
            cmd.Parameters.Add(new NpgsqlParameter("@taxIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = distinctTaxIds });
        }
        else
        {
            cmd.Parameters.Add(new NpgsqlParameter("@runStartedAt", NpgsqlDbType.TimestampTz) { Value = runStartedAt });
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new JobTelemetryRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetDouble(7),
                reader.GetDouble(8),
                reader.GetDouble(9),
                reader.GetDouble(10),
                reader.GetDouble(11),
                reader.GetDouble(12),
                reader.GetDouble(13),
                reader.GetDouble(14),
                reader.GetDouble(15)));
        }

        if (rows.Count == 0) return null;

        var subList = submissionLatencies.ToList();
        var subAvg = subList.Count > 0 ? subList.Average() : 0;
        var subP50 = ComputePercentile(subList, 0.50);
        var subP95 = ComputePercentile(subList, 0.95);

        List<double> Col(Func<JobTelemetryRow, double> selector) => rows.Select(selector).ToList();

        var qList = Col(r => r.QueueWaitMs);
        var claimList = Col(r => r.ClaimMs);
        var snapList = Col(r => r.LoadSnapshotMs);
        var jsonList = Col(r => r.JsonMappingMs);
        var natList = Col(r => r.NativeTotalMs);
        var ingestList = Col(r => r.NativeIngestMs);
        var layoutList = Col(r => r.NativeLayoutMs);
        var renderList = Col(r => r.NativeRenderMs);
        var signList = Col(r => r.NativeSignMs);
        var signerCallList = Col(r => r.NativeSignerCallMs);
        var hashList = Col(r => r.ManagedHashMs);
        var saveList = Col(r => r.StorageSaveMs);
        var dbList = Col(r => r.DbCompleteMs);
        var workerList = Col(r => r.TotalWorkerMs);

        return new AggregatePhaseMetrics(
            TotalJobs: rows.Count,
            TotalWallSec: totalWallSec,
            SubmissionWallSec: submitWallSec,
            SubmissionAvgMs: subAvg,
            SubmissionP50Ms: subP50,
            SubmissionP95Ms: subP95,
            QueueWaitAvgMs: qList.Average(),
            QueueWaitP50Ms: ComputePercentile(qList, 0.50),
            QueueWaitP95Ms: ComputePercentile(qList, 0.95),
            QueueWaitSumSec: qList.Sum() / 1000.0,
            ClaimSumSec: claimList.Sum() / 1000.0,
            ClaimAvgMs: claimList.Average(),
            ClaimP50Ms: ComputePercentile(claimList, 0.50),
            ClaimP95Ms: ComputePercentile(claimList, 0.95),
            LoadSnapshotSumSec: snapList.Sum() / 1000.0,
            LoadSnapshotAvgMs: snapList.Average(),
            LoadSnapshotP50Ms: ComputePercentile(snapList, 0.50),
            LoadSnapshotP95Ms: ComputePercentile(snapList, 0.95),
            JsonMappingSumSec: jsonList.Sum() / 1000.0,
            JsonMappingAvgMs: jsonList.Average(),
            JsonMappingP50Ms: ComputePercentile(jsonList, 0.50),
            JsonMappingP95Ms: ComputePercentile(jsonList, 0.95),
            NativeTotalSumSec: natList.Sum() / 1000.0,
            NativeTotalAvgMs: natList.Average(),
            NativeTotalP50Ms: ComputePercentile(natList, 0.50),
            NativeTotalP95Ms: ComputePercentile(natList, 0.95),
            NativeIngestSumSec: ingestList.Sum() / 1000.0,
            NativeIngestAvgMs: ingestList.Average(),
            NativeIngestP50Ms: ComputePercentile(ingestList, 0.50),
            NativeIngestP95Ms: ComputePercentile(ingestList, 0.95),
            NativeLayoutSumSec: layoutList.Sum() / 1000.0,
            NativeLayoutAvgMs: layoutList.Average(),
            NativeLayoutP50Ms: ComputePercentile(layoutList, 0.50),
            NativeLayoutP95Ms: ComputePercentile(layoutList, 0.95),
            NativeRenderSumSec: renderList.Sum() / 1000.0,
            NativeRenderAvgMs: renderList.Average(),
            NativeRenderP50Ms: ComputePercentile(renderList, 0.50),
            NativeRenderP95Ms: ComputePercentile(renderList, 0.95),
            NativeSignSumSec: signList.Sum() / 1000.0,
            NativeSignAvgMs: signList.Average(),
            NativeSignP50Ms: ComputePercentile(signList, 0.50),
            NativeSignP95Ms: ComputePercentile(signList, 0.95),
            NativeSignerCallSumSec: signerCallList.Sum() / 1000.0,
            NativeSignerCallAvgMs: signerCallList.Average(),
            NativeSignerCallP50Ms: ComputePercentile(signerCallList, 0.50),
            NativeSignerCallP95Ms: ComputePercentile(signerCallList, 0.95),
            ManagedHashSumSec: hashList.Sum() / 1000.0,
            ManagedHashAvgMs: hashList.Average(),
            ManagedHashP50Ms: ComputePercentile(hashList, 0.50),
            ManagedHashP95Ms: ComputePercentile(hashList, 0.95),
            StorageSaveSumSec: saveList.Sum() / 1000.0,
            StorageSaveAvgMs: saveList.Average(),
            StorageSaveP50Ms: ComputePercentile(saveList, 0.50),
            StorageSaveP95Ms: ComputePercentile(saveList, 0.95),
            DbCompleteSumSec: dbList.Sum() / 1000.0,
            DbCompleteAvgMs: dbList.Average(),
            DbCompleteP50Ms: ComputePercentile(dbList, 0.50),
            DbCompleteP95Ms: ComputePercentile(dbList, 0.95),
            WorkerTotalSumSec: workerList.Sum() / 1000.0,
            WorkerTotalAvgMs: workerList.Average(),
            WorkerTotalP50Ms: ComputePercentile(workerList, 0.50),
            WorkerTotalP95Ms: ComputePercentile(workerList, 0.95)
        );
    }

    private static void PrintPhaseBreakdown(AggregatePhaseMetrics m)
    {
        var workerAvg = Math.Max(0.001, m.WorkerTotalAvgMs);
        double Pct(double phaseAvg) => (phaseAvg / workerAvg) * 100.0;

        Console.WriteLine();
        Console.WriteLine("----------------------- AGGREGATE PHASE BREAKDOWN -----------------------");
        Console.WriteLine($"{"Phase",-27} {"Avg (ms)",10} {"p50 (ms)",10} {"p95 (ms)",10} {"% Worker",10}");
        Console.WriteLine("-------------------------------------------------------------------------");
        Console.WriteLine($"{"DB Claim",-27} {m.ClaimAvgMs,10:F2} {m.ClaimP50Ms,10:F2} {m.ClaimP95Ms,10:F2} {Pct(m.ClaimAvgMs),9:F1} %");
        Console.WriteLine($"{"Load Snapshot (DB)",-27} {m.LoadSnapshotAvgMs,10:F2} {m.LoadSnapshotP50Ms,10:F2} {m.LoadSnapshotP95Ms,10:F2} {Pct(m.LoadSnapshotAvgMs),9:F1} %");
        Console.WriteLine($"{"JSON Serialization",-27} {m.JsonMappingAvgMs,10:F2} {m.JsonMappingP50Ms,10:F2} {m.JsonMappingP95Ms,10:F2} {Pct(m.JsonMappingAvgMs),9:F1} %");
        Console.WriteLine($"{"Native PDF Generation",-27} {m.NativeTotalAvgMs,10:F2} {m.NativeTotalP50Ms,10:F2} {m.NativeTotalP95Ms,10:F2} {Pct(m.NativeTotalAvgMs),9:F1} %");
        Console.WriteLine($"{"  ├── JSON Ingest",-27} {m.NativeIngestAvgMs,10:F2} {m.NativeIngestP50Ms,10:F2} {m.NativeIngestP95Ms,10:F2} {Pct(m.NativeIngestAvgMs),9:F1} %");
        Console.WriteLine($"{"  ├── Page Layout",-27} {m.NativeLayoutAvgMs,10:F2} {m.NativeLayoutP50Ms,10:F2} {m.NativeLayoutP95Ms,10:F2} {Pct(m.NativeLayoutAvgMs),9:F1} %");
        Console.WriteLine($"{"  ├── Libharu Render",-27} {m.NativeRenderAvgMs,10:F2} {m.NativeRenderP50Ms,10:F2} {m.NativeRenderP95Ms,10:F2} {Pct(m.NativeRenderAvgMs),9:F1} %");
        Console.WriteLine($"{"  └── Digital Signing",-27} {m.NativeSignAvgMs,10:F2} {m.NativeSignP50Ms,10:F2} {m.NativeSignP95Ms,10:F2} {Pct(m.NativeSignAvgMs),9:F1} %");
        if (m.NativeSignerCallAvgMs > 0.01)
        {
            Console.WriteLine($"{"      └── Signer / TSA",-27} {m.NativeSignerCallAvgMs,10:F2} {m.NativeSignerCallP50Ms,10:F2} {m.NativeSignerCallP95Ms,10:F2} {Pct(m.NativeSignerCallAvgMs),9:F1} %");
        }
        Console.WriteLine($"{"SHA-256 Hashing",-27} {m.ManagedHashAvgMs,10:F2} {m.ManagedHashP50Ms,10:F2} {m.ManagedHashP95Ms,10:F2} {Pct(m.ManagedHashAvgMs),9:F1} %");
        Console.WriteLine($"{"Storage Save (Disk)",-27} {m.StorageSaveAvgMs,10:F2} {m.StorageSaveP50Ms,10:F2} {m.StorageSaveP95Ms,10:F2} {Pct(m.StorageSaveAvgMs),9:F1} %");
        Console.WriteLine($"{"DB Finalize & Complete",-27} {m.DbCompleteAvgMs,10:F2} {m.DbCompleteP50Ms,10:F2} {m.DbCompleteP95Ms,10:F2} {Pct(m.DbCompleteAvgMs),9:F1} %");
        Console.WriteLine("-------------------------------------------------------------------------");
        Console.WriteLine($"{"Total In-Worker Time",-27} {m.WorkerTotalAvgMs,10:F2} {m.WorkerTotalP50Ms,10:F2} {m.WorkerTotalP95Ms,10:F2} {"100.0 %",10}");
        Console.WriteLine($"{"Queue Wait (Backlog)",-27} {m.QueueWaitAvgMs,10:F2} {m.QueueWaitP50Ms,10:F2} {m.QueueWaitP95Ms,10:F2} {"-",10}");
        Console.WriteLine($"{"Submission (API Request)",-27} {m.SubmissionAvgMs,10:F2} {m.SubmissionP50Ms,10:F2} {m.SubmissionP95Ms,10:F2} {"-",10}");
        Console.WriteLine("=========================================================================");
    }

    private static async Task ExportProfilesAsync(string repoRoot, AggregatePhaseMetrics m)
    {
        // 1. Collapsed stack format: benchmark-flamegraph.txt
        var collapsedLines = new List<string>();

        if (m.SubmissionAvgMs > 0)
        {
            collapsedLines.Add($"Pipeline;Submission {(long)Math.Round(m.SubmissionAvgMs * m.TotalJobs)}");
        }

        if (m.QueueWaitSumSec > 0)
        {
            collapsedLines.Add($"Pipeline;Queue_Wait {(long)Math.Round(m.QueueWaitSumSec * 1000)}");
        }

        collapsedLines.Add($"Pipeline;Worker;DB_Claim {(long)Math.Round(m.ClaimSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;Load_Banking_Snapshot {(long)Math.Round(m.LoadSnapshotSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;JSON_Mapping {(long)Math.Round(m.JsonMappingSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;Native_PDF;JSON_Ingest {(long)Math.Round(m.NativeIngestSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;Native_PDF;Layout {(long)Math.Round(m.NativeLayoutSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;Native_PDF;Libharu_Render {(long)Math.Round(m.NativeRenderSumSec * 1000)}");

        if (m.NativeSignSumSec > 0)
        {
            if (m.NativeSignerCallSumSec > 0)
            {
                collapsedLines.Add($"Pipeline;Worker;Native_PDF;Digital_Signing;Signer_Socket_TSA {(long)Math.Round(m.NativeSignerCallSumSec * 1000)}");
                var injectMs = Math.Max(0, (long)Math.Round((m.NativeSignSumSec - m.NativeSignerCallSumSec) * 1000));
                if (injectMs > 0)
                {
                    collapsedLines.Add($"Pipeline;Worker;Native_PDF;Digital_Signing;PDF_Inject {injectMs}");
                }
            }
            else
            {
                collapsedLines.Add($"Pipeline;Worker;Native_PDF;Digital_Signing {(long)Math.Round(m.NativeSignSumSec * 1000)}");
            }
        }

        collapsedLines.Add($"Pipeline;Worker;SHA256_Hash {(long)Math.Round(m.ManagedHashSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;Storage_Save {(long)Math.Round(m.StorageSaveSumSec * 1000)}");
        collapsedLines.Add($"Pipeline;Worker;DB_Complete {(long)Math.Round(m.DbCompleteSumSec * 1000)}");

        var collapsedPath = Path.Combine(repoRoot, "benchmark-flamegraph.txt");
        await File.WriteAllLinesAsync(collapsedPath, collapsedLines);

        // 2. Speedscope JSON format: benchmark-speedscope.json
        var frames = new List<object>
        {
            new { name = "Pipeline" },                                    // 0
            new { name = "Submission" },                                  // 1
            new { name = "Queue_Wait" },                                  // 2
            new { name = "Worker" },                                      // 3
            new { name = "DB_Claim" },                                    // 4
            new { name = "Load_Banking_Snapshot" },                       // 5
            new { name = "JSON_Mapping" },                                // 6
            new { name = "Native_PDF" },                                  // 7
            new { name = "JSON_Ingest" },                                 // 8
            new { name = "Layout" },                                      // 9
            new { name = "Libharu_Render" },                              // 10
            new { name = "Digital_Signing" },                             // 11
            new { name = "Signer_Socket_TSA" },                           // 12
            new { name = "PDF_Inject" },                                  // 13
            new { name = "SHA256_Hash" },                                 // 14
            new { name = "Storage_Save" },                                // 15
            new { name = "DB_Complete" }                                  // 16
        };

        // 1. Single Customer Report Profile (Average per customer)
        var singleSamples = new List<int[]>();
        var singleWeights = new List<double>();
        void AddSingle(int[] stack, double weightMs)
        {
            if (weightMs > 0.0001)
            {
                singleSamples.Add(stack);
                singleWeights.Add(Math.Round(weightMs, 4));
            }
        }

        AddSingle(new[] { 0, 3, 4 }, m.ClaimAvgMs);
        AddSingle(new[] { 0, 3, 5 }, m.LoadSnapshotAvgMs);
        AddSingle(new[] { 0, 3, 6 }, m.JsonMappingAvgMs);
        AddSingle(new[] { 0, 3, 7, 8 }, m.NativeIngestAvgMs);
        AddSingle(new[] { 0, 3, 7, 9 }, m.NativeLayoutAvgMs);
        AddSingle(new[] { 0, 3, 7, 10 }, m.NativeRenderAvgMs);
        if (m.NativeSignerCallAvgMs > 0)
        {
            AddSingle(new[] { 0, 3, 7, 11, 12 }, m.NativeSignerCallAvgMs);
            AddSingle(new[] { 0, 3, 7, 11, 13 }, Math.Max(0, m.NativeSignAvgMs - m.NativeSignerCallAvgMs));
        }
        else if (m.NativeSignAvgMs > 0)
        {
            AddSingle(new[] { 0, 3, 7, 11 }, m.NativeSignAvgMs);
        }
        AddSingle(new[] { 0, 3, 14 }, m.ManagedHashAvgMs);
        AddSingle(new[] { 0, 3, 15 }, m.StorageSaveAvgMs);
        AddSingle(new[] { 0, 3, 16 }, m.DbCompleteAvgMs);

        // 2. Worker Active Execution Profile (Batch Total, No Backlog)
        var workerSamples = new List<int[]>();
        var workerWeights = new List<double>();
        void AddWorker(int[] stack, double weightMs)
        {
            if (weightMs > 0.001)
            {
                workerSamples.Add(stack);
                workerWeights.Add(Math.Round(weightMs, 2));
            }
        }

        AddWorker(new[] { 0, 3, 4 }, m.ClaimSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 5 }, m.LoadSnapshotSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 6 }, m.JsonMappingSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 7, 8 }, m.NativeIngestSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 7, 9 }, m.NativeLayoutSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 7, 10 }, m.NativeRenderSumSec * 1000.0);
        if (m.NativeSignerCallSumSec > 0)
        {
            AddWorker(new[] { 0, 3, 7, 11, 12 }, m.NativeSignerCallSumSec * 1000.0);
            AddWorker(new[] { 0, 3, 7, 11, 13 }, Math.Max(0, (m.NativeSignSumSec - m.NativeSignerCallSumSec) * 1000.0));
        }
        else if (m.NativeSignSumSec > 0)
        {
            AddWorker(new[] { 0, 3, 7, 11 }, m.NativeSignSumSec * 1000.0);
        }
        AddWorker(new[] { 0, 3, 14 }, m.ManagedHashSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 15 }, m.StorageSaveSumSec * 1000.0);
        AddWorker(new[] { 0, 3, 16 }, m.DbCompleteSumSec * 1000.0);

        // 3. Full Batch Pipeline Profile (with Backlog & Submission)
        var batchSamples = new List<int[]>();
        var batchWeights = new List<double>();
        void AddBatch(int[] stack, double weightMs)
        {
            if (weightMs > 0.001)
            {
                batchSamples.Add(stack);
                batchWeights.Add(Math.Round(weightMs, 2));
            }
        }

        AddBatch(new[] { 0, 1 }, m.SubmissionAvgMs * m.TotalJobs);
        AddBatch(new[] { 0, 2 }, m.QueueWaitSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 4 }, m.ClaimSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 5 }, m.LoadSnapshotSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 6 }, m.JsonMappingSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 7, 8 }, m.NativeIngestSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 7, 9 }, m.NativeLayoutSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 7, 10 }, m.NativeRenderSumSec * 1000.0);
        if (m.NativeSignerCallSumSec > 0)
        {
            AddBatch(new[] { 0, 3, 7, 11, 12 }, m.NativeSignerCallSumSec * 1000.0);
            AddBatch(new[] { 0, 3, 7, 11, 13 }, Math.Max(0, (m.NativeSignSumSec - m.NativeSignerCallSumSec) * 1000.0));
        }
        else if (m.NativeSignSumSec > 0)
        {
            AddBatch(new[] { 0, 3, 7, 11 }, m.NativeSignSumSec * 1000.0);
        }
        AddBatch(new[] { 0, 3, 14 }, m.ManagedHashSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 15 }, m.StorageSaveSumSec * 1000.0);
        AddBatch(new[] { 0, 3, 16 }, m.DbCompleteSumSec * 1000.0);

        // 4. Native Engine Only (Zero DB)
        var nativeOnlySamples = new List<int[]>();
        var nativeOnlyWeights = new List<double>();
        void AddNativeOnly(int[] stack, double weightMs)
        {
            if (weightMs > 0.0001)
            {
                nativeOnlySamples.Add(stack);
                nativeOnlyWeights.Add(Math.Round(weightMs, 4));
            }
        }
        AddNativeOnly(new[] { 0, 3, 6 }, m.JsonMappingAvgMs);
        AddNativeOnly(new[] { 0, 3, 7, 8 }, m.NativeIngestAvgMs);
        AddNativeOnly(new[] { 0, 3, 7, 9 }, m.NativeLayoutAvgMs);
        AddNativeOnly(new[] { 0, 3, 7, 10 }, m.NativeRenderAvgMs);
        if (m.NativeSignerCallAvgMs > 0)
        {
            AddNativeOnly(new[] { 0, 3, 7, 11, 12 }, m.NativeSignerCallAvgMs);
            AddNativeOnly(new[] { 0, 3, 7, 11, 13 }, Math.Max(0, m.NativeSignAvgMs - m.NativeSignerCallAvgMs));
        }
        else if (m.NativeSignAvgMs > 0)
        {
            AddNativeOnly(new[] { 0, 3, 7, 11 }, m.NativeSignAvgMs);
        }
        AddNativeOnly(new[] { 0, 3, 14 }, m.ManagedHashAvgMs);
        AddNativeOnly(new[] { 0, 3, 15 }, m.StorageSaveAvgMs);

        // Standalone Native PDF Engine file
        var nativeFrames = new List<object>
        {
            new { name = "Document_Pipeline" },                           // 0
            new { name = "JSON_Mapping" },                                // 1
            new { name = "Native_PDF" },                                  // 2
            new { name = "JSON_Ingest" },                                 // 3
            new { name = "Layout" },                                      // 4
            new { name = "Libharu_Render" },                              // 5
            new { name = "Digital_Signing" },                             // 6
            new { name = "Signer_Socket_TSA" },                           // 7
            new { name = "PDF_Inject" },                                  // 8
            new { name = "SHA256_Hash" },                                 // 9
            new { name = "Storage_Save" }                                 // 10
        };
        var standaloneNativeSamples = new List<int[]>();
        var standaloneNativeWeights = new List<double>();
        void AddStandaloneNative(int[] stack, double weightMs)
        {
            if (weightMs > 0.0001)
            {
                standaloneNativeSamples.Add(stack);
                standaloneNativeWeights.Add(Math.Round(weightMs, 4));
            }
        }
        AddStandaloneNative(new[] { 0, 1 }, m.JsonMappingAvgMs);
        AddStandaloneNative(new[] { 0, 2, 3 }, m.NativeIngestAvgMs);
        AddStandaloneNative(new[] { 0, 2, 4 }, m.NativeLayoutAvgMs);
        AddStandaloneNative(new[] { 0, 2, 5 }, m.NativeRenderAvgMs);
        if (m.NativeSignerCallAvgMs > 0)
        {
            AddStandaloneNative(new[] { 0, 2, 6, 7 }, m.NativeSignerCallAvgMs);
            AddStandaloneNative(new[] { 0, 2, 6, 8 }, Math.Max(0, m.NativeSignAvgMs - m.NativeSignerCallAvgMs));
        }
        else if (m.NativeSignAvgMs > 0)
        {
            AddStandaloneNative(new[] { 0, 2, 6 }, m.NativeSignAvgMs);
        }
        AddStandaloneNative(new[] { 0, 9 }, m.ManagedHashAvgMs);
        AddStandaloneNative(new[] { 0, 10 }, m.StorageSaveAvgMs);

        var nativePdfObj = new Dictionary<string, object?>
        {
            ["$schema"] = "https://www.speedscope.app/file-format-schema.json",
            ["shared"] = new { frames = nativeFrames },
            ["profiles"] = new object[]
            {
                new
                {
                    type = "sampled",
                    name = "Native PDF Engine & Storage (Zero DB)",
                    unit = "milliseconds",
                    startValue = 0,
                    endValue = standaloneNativeWeights.Sum(),
                    samples = standaloneNativeSamples,
                    weights = standaloneNativeWeights
                }
            },
            ["name"] = "Native PDF Engine",
            ["activeProfileIndex"] = 0,
            ["exporter"] = "Nordiska.ReportBenchmark"
        };
        var nativePdfPath = Path.Combine(repoRoot, "benchmark-native-pdf.speedscope.json");
        await File.WriteAllTextAsync(nativePdfPath, JsonSerializer.Serialize(nativePdfObj, new JsonSerializerOptions { WriteIndented = true }));

        // Standalone Database Operations file
        var dbFrames = new List<object>
        {
            new { name = "Database_Pipeline" },                           // 0
            new { name = "Queue_Wait" },                                  // 1
            new { name = "DB_Claim" },                                    // 2
            new { name = "Load_Banking_Snapshot" },                       // 3
            new { name = "DB_Complete" }                                  // 4
        };
        var dbSamples = new List<int[]>();
        var dbWeights = new List<double>();
        void AddDb(int[] stack, double weightMs)
        {
            if (weightMs > 0.001)
            {
                dbSamples.Add(stack);
                dbWeights.Add(Math.Round(weightMs, 4));
            }
        }
        AddDb(new[] { 0, 2 }, m.ClaimAvgMs);
        AddDb(new[] { 0, 3 }, m.LoadSnapshotAvgMs);
        AddDb(new[] { 0, 4 }, m.DbCompleteAvgMs);

        var dbObj = new Dictionary<string, object?>
        {
            ["$schema"] = "https://www.speedscope.app/file-format-schema.json",
            ["shared"] = new { frames = dbFrames },
            ["profiles"] = new object[]
            {
                new
                {
                    type = "sampled",
                    name = "Database Operations (Per-Report Average)",
                    unit = "milliseconds",
                    startValue = 0,
                    endValue = dbWeights.Sum(),
                    samples = dbSamples,
                    weights = dbWeights
                }
            },
            ["name"] = "Database Operations",
            ["activeProfileIndex"] = 0,
            ["exporter"] = "Nordiska.ReportBenchmark"
        };
        var dbPath = Path.Combine(repoRoot, "benchmark-database.speedscope.json");
        await File.WriteAllTextAsync(dbPath, JsonSerializer.Serialize(dbObj, new JsonSerializerOptions { WriteIndented = true }));

        var speedscopeObj = new Dictionary<string, object?>
        {
            ["$schema"] = "https://www.speedscope.app/file-format-schema.json",
            ["shared"] = new { frames },
            ["profiles"] = new object[]
            {
                new
                {
                    type = "sampled",
                    name = "Native PDF Engine (Per-Report, Zero DB)",
                    unit = "milliseconds",
                    startValue = 0,
                    endValue = nativeOnlyWeights.Sum(),
                    samples = nativeOnlySamples,
                    weights = nativeOnlyWeights
                },
                new
                {
                    type = "sampled",
                    name = "Single Customer Report (Full Lifecycle with DB)",
                    unit = "milliseconds",
                    startValue = 0,
                    endValue = singleWeights.Sum(),
                    samples = singleSamples,
                    weights = singleWeights
                },
                new
                {
                    type = "sampled",
                    name = "Worker Active Execution (Batch Total, No Backlog)",
                    unit = "milliseconds",
                    startValue = 0,
                    endValue = workerWeights.Sum(),
                    samples = workerSamples,
                    weights = workerWeights
                },
                new
                {
                    type = "sampled",
                    name = "Full Batch Pipeline (with Backlog & Submission)",
                    unit = "milliseconds",
                    startValue = 0,
                    endValue = batchWeights.Sum(),
                    samples = batchSamples,
                    weights = batchWeights
                }
            },
            ["name"] = "Nordiska Report Benchmark",
            ["activeProfileIndex"] = 0,
            ["exporter"] = "Nordiska.ReportBenchmark"
        };

        var speedscopeCanonicalPath = Path.Combine(repoRoot, "benchmark.speedscope.json");
        var speedscopeLegacyPath = Path.Combine(repoRoot, "benchmark-speedscope.json");
        var speedscopeJson = JsonSerializer.Serialize(speedscopeObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(speedscopeCanonicalPath, speedscopeJson);
        await File.WriteAllTextAsync(speedscopeLegacyPath, speedscopeJson);

        Console.WriteLine();
        Console.WriteLine("[PROFILE] Exported flamegraph data:");
        Console.WriteLine($"  - Collapsed Stack : {Path.GetFileName(collapsedPath)}");
        Console.WriteLine($"  - Pure Native PDF : {Path.GetFileName(nativePdfPath)} (zero DB, renderer only)");
        Console.WriteLine($"  - Pure Database   : {Path.GetFileName(dbPath)} (queries, locks, completion)");
        Console.WriteLine($"  - Complete Suite  : {Path.GetFileName(speedscopeCanonicalPath)} / {Path.GetFileName(speedscopeLegacyPath)}");
    }
}
