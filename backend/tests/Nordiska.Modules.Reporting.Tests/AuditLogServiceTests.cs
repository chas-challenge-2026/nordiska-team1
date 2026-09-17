using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public class AuditLogServiceTests
{
    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private static (AuditLogService service, ReportingDbContext dbContext) CreateService(string? customKey = null)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ReportingDbContext(options);

        var configValues = new Dictionary<string, string?>();
        if (customKey != null)
        {
            configValues["AuditLogging:SigningKey"] = customKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var logger = new TestLogger<AuditLogService>();
        var service = new AuditLogService(dbContext, configuration, logger);

        return (service, dbContext);
    }

    [Fact]
    public async Task LogAsync_ShouldCreateAuditEntryWithValidSignature_AndPersistToDb()
    {
        // Arrange
        var (service, dbContext) = CreateService("Test-Audit-Signing-Key-12345");

        // Act
        var entry = await service.LogAsync("ACCOUNT_OPEN", 42, "{\"accountNumber\":\"NOR-100001\"}");

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("ACCOUNT_OPEN", entry.Action);
        Assert.Equal(42, entry.UserId);
        Assert.Equal("{\"accountNumber\":\"NOR-100001\"}", entry.Details);
        Assert.False(string.IsNullOrWhiteSpace(entry.Signature));
        Assert.Equal(64, entry.Signature.Length); // Hex representation of SHA-256 is 64 chars

        // Verify stored in DB
        var stored = await dbContext.AuditEntries.FindAsync(entry.Id);
        Assert.NotNull(stored);
        Assert.Equal(entry.Signature, stored.Signature);

        // Verify signature check
        var isValid = service.VerifySignature(entry);
        Assert.True(isValid);
    }

    [Fact]
    public async Task VerifySignature_ShouldReturnFalse_WhenDetailsAreModified()
    {
        // Arrange
        var (service, _) = CreateService();
        var entry = await service.LogAsync("TRANSACTION_DEPOSIT", 100, "{\"amount\":5000.00}");

        // Act - Simulate malicious database tampering
        entry.Details = "{\"amount\":500000.00}"; // Tampered!

        // Assert
        var isValid = service.VerifySignature(entry);
        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifySignature_ShouldReturnFalse_WhenUserIdOrActionIsModified()
    {
        // Arrange
        var (service, _) = CreateService();
        var entry = await service.LogAsync("CUSTOMER_UPDATE", 10, "{\"phone\":\"0701234567\"}");

        // Act - Tamper with UserId
        entry.UserId = 999;

        // Assert
        Assert.False(service.VerifySignature(entry));

        // Act - Tamper with Action
        entry.UserId = 10;
        entry.Action = "CUSTOMER_DELETE";

        // Assert
        Assert.False(service.VerifySignature(entry));
    }

    [Fact]
    public async Task GetEntriesAsync_ShouldFilterByUserIdAndAction_AndSupportPagination()
    {
        // Arrange
        var (service, _) = CreateService();

        await service.LogAsync("AUTH_LOGIN", 1, "{\"ip\":\"127.0.0.1\"}");
        await service.LogAsync("ACCOUNT_OPEN", 1, "{\"account\":\"NOR-001\"}");
        await service.LogAsync("TRANSACTION_DEPOSIT", 1, "{\"amount\":100}");
        await service.LogAsync("AUTH_LOGIN", 2, "{\"ip\":\"127.0.0.1\"}");
        await service.LogAsync("ACCOUNT_OPEN", 2, "{\"account\":\"NOR-002\"}");

        // Act - Filter by User 1
        var (user1Entries, user1Count) = await service.GetEntriesAsync(userId: 1, page: 1, pageSize: 10);

        // Assert
        Assert.Equal(3, user1Count);
        Assert.Equal(3, user1Entries.Count);
        Assert.All(user1Entries, e => Assert.Equal(1, e.UserId));

        // Act - Filter by Action "AUTH_LOGIN"
        var (authEntries, authCount) = await service.GetEntriesAsync(action: "AUTH_LOGIN", page: 1, pageSize: 10);
        Assert.Equal(2, authCount);
        Assert.Equal(2, authEntries.Count);

        // Act - Pagination
        var (pagedEntries, total) = await service.GetEntriesAsync(page: 1, pageSize: 2);
        Assert.Equal(5, total);
        Assert.Equal(2, pagedEntries.Count);
    }

    [Fact]
    public void Constructor_ShouldThrowInvalidOperationException_WhenSigningKeyMissingInProduction()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new ReportingDbContext(options);

        var configValues = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var logger = new TestLogger<AuditLogService>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new AuditLogService(dbContext, configuration, logger));
    }
}
