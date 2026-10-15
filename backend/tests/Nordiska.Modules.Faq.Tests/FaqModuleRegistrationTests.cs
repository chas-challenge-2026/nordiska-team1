using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.Modules.Faq.Tests;

public class FaqModuleRegistrationTests
{
    private static ServiceProvider BuildProvider(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFaqModuleInfrastructure(new DictionaryConfiguration(values));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddFaqModuleInfrastructure_RegistersServicesThatResolve()
    {
        using var provider = BuildProvider(
            ("ConnectionStrings:FaqDatabase", "Host=localhost;Database=unused"),
            ("FaqSearchLog:Salt", "test-salt"));
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FaqService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFaqRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFaqSearchLogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFaqViewLogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FaqDbContext>());
        Assert.Same(
            provider.GetRequiredService<FaqSearchLogQueue>(),
            provider.GetRequiredService<FaqSearchLogQueue>());
    }

    [Fact]
    public void AddFaqModuleInfrastructure_MissingSalt_FailsOptionsValidation()
    {
        using var provider = BuildProvider(("ConnectionStrings:FaqDatabase", "Host=localhost;Database=unused"));

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<FaqSearchLogOptions>>().Value);
    }

    [Fact]
    public void AddFaqModuleInfrastructure_MissingConnectionString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => BuildProvider(("FaqSearchLog:Salt", "test-salt")));
    }

    // The Configuration package isn't referenced here and adding it would change the locked restore.
    private sealed class DictionaryConfiguration(IReadOnlyDictionary<string, string?> values, string path = "")
        : IConfigurationSection
    {
        public string? this[string key]
        {
            get => values.GetValueOrDefault(Combine(key));
            set => throw new NotSupportedException();
        }

        public string Key => path[(path.LastIndexOf(':') + 1)..];

        public string Path => path;

        public string? Value
        {
            get => values.GetValueOrDefault(path);
            set => throw new NotSupportedException();
        }

        public IConfigurationSection GetSection(string key) => new DictionaryConfiguration(values, Combine(key));

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            var prefix = path.Length == 0 ? string.Empty : path + ":";
            return values.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(key => key[prefix.Length..].Split(':')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(GetSection)
                .ToList();
        }

        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

        private string Combine(string key) => path.Length == 0 ? key : path + ":" + key;
    }
}
