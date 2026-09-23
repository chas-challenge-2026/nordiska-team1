using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Infrastructure.External.Riksbank;

namespace Nordiska.Modules.Banking.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddBankingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModulePostgresDbContext<BankingDbContext>(
            configuration,
            BankingDatabase.Details);

        // Register repositories and services for the banking module (FAQ-style)
        services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
        services.AddScoped<IAccountTypeConfigRepository, AccountTypeConfigRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ISavingsAccountService, SavingsAccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ICustomerService, CustomerService>();

        // Interest rates & account types are read often and change rarely, so they are cached in memory
        services.AddMemoryCache();
        services.AddScoped<IAccountTypeConfigService, AccountTypeConfigService>();
        services.AddScoped<IInterestRateService, InterestRateService>();
        services.AddScoped<IOperationalMessageRepository, OperationalMessageRepository>();
        services.AddScoped<IOperationalMessageService, OperationalMessageService>();

        // Policy rate comes from the Riksbank SWEA API. Bad config should fail at startup, not on the first request.
        services.AddOptions<RiksbankOptions>()
            .Bind(configuration.GetSection(RiksbankOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _) && o.BaseUrl.EndsWith('/'), "Riksbank:BaseUrl must be an absolute URL ending with '/'.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SeriesId), "Riksbank:SeriesId is required.")
            .Validate(o => o.CacheDuration > TimeSpan.Zero, "Riksbank:CacheDuration must be greater than zero.")
            .ValidateOnStart();

        // Typed client, IHttpClientFactory owns the handler lifetime so we don't run out of sockets or keep stale DNS.
        // The standard resilience handler adds retry with backoff, circuit breaker and timeouts.
        services.AddHttpClient<IRiksbankClient, RiksbankClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<RiksbankOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .AddStandardResilienceHandler();

        services.AddScoped<IPolicyRateService, PolicyRateService>();

        return services;
    }
}