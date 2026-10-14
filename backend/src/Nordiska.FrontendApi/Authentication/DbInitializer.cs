using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;
using Nordiska.Modules.CustomerCenter.Domain;

namespace Nordiska.FrontendApi.Authentication;

public class DbInitializer
{
    /// <summary>
    /// Ensures that essential Identity roles and a bootstrap administrator exist.
    /// This method is safe to run in all environments (Development, Staging, Production).
    /// If an administrator already exists, their existing password and settings are preserved.
    /// </summary>
    public static async Task EnsureAdminAndRolesAsync(IServiceProvider serviceProvider, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();
        var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole<long>>>();

        if (!await db.Database.CanConnectAsync() || roleManager == null)
        {
            return;
        }

        string[] requiredRoles = ["Admin", "Customer", "BankStaff", "Staff"];
        foreach (var role in requiredRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<long>(role));
            }
        }

        var adminEmail = configuration?["AdminUser:Email"] ?? "admin@nordiska.se";
        var adminName = configuration?["AdminUser:Name"] ?? "Jesper Adminsson";
        var adminPersonalNum = configuration?["AdminUser:PersonalNum"] ?? "200505032383";
        
        var adminPassword = configuration?["AdminUser:Password"] ?? "password123";

        // 1. Ensure StaffMember table contains bootstrap administrator (RBAC)
        var existingStaff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Email == adminEmail);
        if (existingStaff == null)
        {
            var adminStaff = new StaffMember
            {
                Email = adminEmail,
                FullName = adminName,
                Role = "Admin",
                Department = "IT Operations",
                EmployeeNumber = "ADM-001",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var passwordHasher = new PasswordHasher<StaffMember>();
            adminStaff.PasswordHash = passwordHasher.HashPassword(adminStaff, adminPassword);
            db.StaffMembers.Add(adminStaff);
            await db.SaveChangesAsync();
        }

        // 2. Legacy customer fallback for backwards compatibility
        var adminCustomer = await userManager.FindByEmailAsync(adminEmail)
            ?? await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == adminPersonalNum || c.Email == adminEmail || c.UserName == adminEmail);

        if (adminCustomer == null)
        {
            adminCustomer = new Customer
            {
                UserName = adminEmail,
                Name = adminName,
                PersonalNum = adminPersonalNum,
                Email = adminEmail,
                PhoneNumber = "+46700999999",
                CreatedAt = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(adminCustomer, adminPassword);
            if (!createResult.Succeeded)
            {
                await userManager.CreateAsync(adminCustomer);
                await userManager.AddPasswordAsync(adminCustomer, adminPassword);
            }
        }
        else
        {
            if (string.IsNullOrEmpty(adminCustomer.PasswordHash))
            {
                await userManager.AddPasswordAsync(adminCustomer, adminPassword);
            }
        }

        if (!await userManager.IsInRoleAsync(adminCustomer, "Admin"))
        {
            await userManager.AddToRoleAsync(adminCustomer, "Admin");
        }

        var anna = await userManager.FindByEmailAsync("anna@example.com");
        if (anna != null && await userManager.IsInRoleAsync(anna, "Admin"))
        {
            await userManager.RemoveFromRoleAsync(anna, "Admin");
        }
    }

    public static async Task SeedAsync(IServiceProvider serviceProvider, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        await EnsureAdminAndRolesAsync(serviceProvider, configuration);

        // Skapa ett temporärt DI-scope för att hämta Scoped-tjänster (som UserManager)
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();

        if (!await db.Database.CanConnectAsync())
        {
            return;
        }

        var testPersonalNum = "198202116050";

        // Kontrollera om testkunden redan finns i databasen
        var existingCustomer = await db.Customers
            .FirstOrDefaultAsync(c => c.PersonalNum == testPersonalNum);

        if (existingCustomer == null)
        {
            var testCustomer = new Customer
            {
                UserName = "anna@example.com",
                Name = "Anna Smith",
                PersonalNum = testPersonalNum,
                Email = "anna@example.com",
                PhoneNumber = "+46700767029",
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(testCustomer);
        }
        else
        {
            existingCustomer.UserName = "anna@example.com";
            existingCustomer.Email = "anna@example.com";
            existingCustomer.PersonalNum = testPersonalNum;
            existingCustomer.NormalizedUserName = "ANNA@EXAMPLE.COM";
            existingCustomer.NormalizedEmail = "ANNA@EXAMPLE.COM";
            await db.SaveChangesAsync();

        }

        var erikPersonalNum = "197903142380";
        var existingErik = await db.Customers
            .FirstOrDefaultAsync(c => c.PersonalNum == erikPersonalNum);

        if (existingErik == null)
        {
            var testErik = new Customer
            {
                UserName = "erik@example.com",
                Name = "Erik Svensson",
                PersonalNum = erikPersonalNum,
                Email = "erik@example.com",
                PhoneNumber = "+46700123456",
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(testErik);
        }
        else
        {
            existingErik.UserName = "erik@example.com";
            existingErik.Email = "erik@example.com";
            existingErik.NormalizedUserName = "ERIK@EXAMPLE.COM";
            existingErik.NormalizedEmail = "ERIK@EXAMPLE.COM";
            await db.SaveChangesAsync();
        }

        var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<DbInitializer>>();

        try
        {
            await SeedAccountsAndTransactionsAsync(db);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "SeedAccountsAndTransactionsAsync failed"); }

        try
        {
            await SeedLoansAsync(db);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "SeedLoansAsync failed"); }

        try
        {
            await SeedNotificationsAsync(db);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "SeedNotificationsAsync failed"); }

        try
        {
            await SeedOperationalMessagesAsync(db);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "SeedOperationalMessagesAsync failed"); }

        try
        {
            await SeedFaqAsync(scope.ServiceProvider);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "SeedFaqAsync failed"); }

        try
        {
            await SeedDocumentsAsync(scope.ServiceProvider);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "SeedDocumentsAsync failed"); }
    }

    private static async Task SeedLoansAsync(BankingDbContext db)
    {
        var anna = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "198202116050");
        if (anna is null || await db.Loans.AnyAsync(l => l.CustomerId == anna.Id))
        {
            return;
        }

        // Fixed loan numbers and rates so the demo data looks the same every time the database is recreated
        db.Loans.AddRange(
            new Loan(anna.Id, "LN-DEMO-PERSONAL-0001", LoanType.Personal, 120000m, 0.0675m, new DateOnly(2026, 8, 1), new DateOnly(2031, 8, 1)),
            new Loan(anna.Id, "LN-DEMO-MORTGAGE-0001", LoanType.Mortgage, 2450000m, 0.0325m, new DateOnly(2026, 6, 1), new DateOnly(2076, 6, 1)));

        await db.SaveChangesAsync();
    }

    private static async Task SeedAccountsAndTransactionsAsync(BankingDbContext db)
    {
        // Normalize any legacy account types on existing savings accounts before modifying configs
        var legacyAccounts = await db.SavingsAccounts.ToListAsync();
        foreach (var acc in legacyAccounts)
        {
            if (string.Equals(acc.AccountType, "Standard", StringComparison.OrdinalIgnoreCase))
                acc.AccountType = "standard";
            else if (string.Equals(acc.AccountType, "Sparkonto Flex", StringComparison.OrdinalIgnoreCase) || string.Equals(acc.AccountType, "flex", StringComparison.OrdinalIgnoreCase))
                acc.AccountType = "flex";
            else if (string.Equals(acc.AccountType, "Fasträntekonto Fix", StringComparison.OrdinalIgnoreCase) || string.Equals(acc.AccountType, "fix", StringComparison.OrdinalIgnoreCase))
                acc.AccountType = "fix";
            else if (string.Equals(acc.AccountType, "Savings", StringComparison.OrdinalIgnoreCase) || string.Equals(acc.AccountType, "saving", StringComparison.OrdinalIgnoreCase))
                acc.AccountType = "saving";
            else if (string.Equals(acc.AccountType, "Premium", StringComparison.OrdinalIgnoreCase) || string.Equals(acc.AccountType, "premium", StringComparison.OrdinalIgnoreCase))
                acc.AccountType = "premium";
        }
        await db.SaveChangesAsync();

        // Ensure standard account type configurations exist and are updated with standard descriptions
        var standardConfigs = new[]
        {
            new AccountTypeConfig { AccountType = "flex", InterestRate = 0.0350m, Description = "Flexibelt sparkonto med rörlig ränta och fria uttag." },
            new AccountTypeConfig { AccountType = "fix", InterestRate = 0.0410m, Description = "Fasträntekonto med 3 månaders bindningstid och hög sparränta." },
            new AccountTypeConfig { AccountType = "standard", InterestRate = 0.0250m, Description = "Standard sparkonto för tryggt vardagssparande." },
            new AccountTypeConfig { AccountType = "saving", InterestRate = 0.0350m, Description = "Högräntekonto med förmånlig avkastning." },
            new AccountTypeConfig { AccountType = "premium", InterestRate = 0.0400m, Description = "Premium sparkonto med vår högsta ränta för större sparbelopp." }
        };

        foreach (var cfg in standardConfigs)
        {
            var existingConfig = await db.AccountTypeConfigs.FirstOrDefaultAsync(x => x.AccountType == cfg.AccountType);
            if (existingConfig == null)
            {
                db.AccountTypeConfigs.Add(cfg);
            }
            else
            {
                existingConfig.InterestRate = cfg.InterestRate;
                existingConfig.Description = cfg.Description;
            }
        }
        await db.SaveChangesAsync();

        // Automatically clean up any legacy account type configs (e.g. 'Fasträntekonto Fix', 'Sparkonto Flex')
        var validTypes = standardConfigs.Select(c => c.AccountType).ToList();
        var legacyConfigs = await db.AccountTypeConfigs
            .Where(x => !validTypes.Contains(x.AccountType))
            .ToListAsync();
        if (legacyConfigs.Any())
        {
            try
            {
                db.AccountTypeConfigs.RemoveRange(legacyConfigs);
                await db.SaveChangesAsync();
            }
            catch
            {
                // Silently ignore if foreign key constraint is active on legacy records
            }
        }

        // 1. Seed / Update Anna Smith's Accounts
        var anna = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "198202116050");
        if (anna != null)
        {
            var annaAccounts = await db.SavingsAccounts
                .Where(a => a.CustomerId == anna.Id)
                .ToListAsync();

            var legacyAcc1 = annaAccounts.FirstOrDefault(a => a.AccountNumber == "ABC-123" || a.AccountNumber == "NOR-100001");
            var legacyAcc2 = annaAccounts.FirstOrDefault(a => a.AccountNumber == "XYZ-234" || a.AccountNumber == "NOR-100002");

            if (legacyAcc1 != null)
            {
                legacyAcc1.AccountNumber = "NOR-100001";
                legacyAcc1.AccountName = "Sparkonto";
                legacyAcc1.AccountType = "flex";
                legacyAcc1.InterestRate = 0.0350m;
                legacyAcc1.Status = "active";
            }

            if (legacyAcc2 != null)
            {
                legacyAcc2.AccountNumber = "NOR-100002";
                legacyAcc2.AccountName = "Lönekonto";
                legacyAcc2.AccountType = "saving";
                legacyAcc2.InterestRate = 0.0350m;
                legacyAcc2.Status = "active";
            }

            if (!annaAccounts.Any())
            {
                var acc1 = new SavingsAccount
                {
                    CustomerId = anna.Id,
                    AccountNumber = "NOR-100001",
                    AccountName = "Sparkonto",
                    AccountType = "flex",
                    Balance = 88210.50m,
                    InterestRate = 0.0350m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };
                var acc2 = new SavingsAccount
                {
                    CustomerId = anna.Id,
                    AccountNumber = "NOR-100002",
                    AccountName = "Lönekonto",
                    AccountType = "saving",
                    Balance = 68099.66m,
                    InterestRate = 0.0350m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                db.SavingsAccounts.AddRange(acc1, acc2);
                await db.SaveChangesAsync();

                var demoTransferCorrelationId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

                var annaTransactions = new List<LedgerEntry>
                {
                    // Account NOR-100001 transactions (Sparkonto)
                    new() { AccountId = acc1.Id, Type = "deposit", Amount = 95772.50m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc1.Id, Type = "transfer", Amount = -5000.00m, Label = "Överföring till Lönekonto", TargetAccountId = acc2.Id, CorrelationId = demoTransferCorrelationId, CreatedAt = new DateTime(2026, 8, 25, 15, 20, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -412.00m, Label = "Restaurang Prego", CreatedAt = new DateTime(2026, 8, 26, 3, 12, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -412.00m, Label = "Kafé Espresso", CreatedAt = new DateTime(2026, 8, 26, 3, 12, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -540.00m, Label = "Bokia Bokhandel", CreatedAt = new DateTime(2026, 8, 18, 20, 15, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -799.00m, Label = "H&M Kläder", CreatedAt = new DateTime(2026, 8, 14, 16, 30, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -399.00m, Label = "Friskis & Svettis", CreatedAt = new DateTime(2026, 8, 10, 17, 0, 0, DateTimeKind.Utc) },

                    // Account NOR-100002 transactions (Lönekonto)
                    new() { AccountId = acc2.Id, Type = "transfer", Amount = 5000.00m, Label = "Överföring från Sparkonto", TargetAccountId = acc1.Id, CorrelationId = demoTransferCorrelationId, CreatedAt = new DateTime(2026, 8, 25, 15, 20, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "deposit", Amount = 18057.08m, Label = "Månadsinsättning", CreatedAt = new DateTime(2026, 8, 25, 15, 22, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "deposit", Amount = 23057.08m, Label = "Månadsinsättning", CreatedAt = new DateTime(2026, 8, 25, 15, 22, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -645.50m, Label = "ICA Kvantum", CreatedAt = new DateTime(2026, 8, 24, 12, 5, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -12500.00m, Label = "Hyra / Boende", CreatedAt = new DateTime(2026, 8, 24, 0, 1, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "deposit", Amount = 32000.00m, Label = "Lön från Arbetsgivare", CreatedAt = new DateTime(2026, 8, 23, 8, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -1230.00m, Label = "Elräkning Vattenfall", CreatedAt = new DateTime(2026, 8, 19, 7, 30, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "deposit", Amount = 250.00m, Label = "Swish-inbetalning", CreatedAt = new DateTime(2026, 8, 17, 14, 2, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -389.00m, Label = "Trygg-Hansa Försäkring", CreatedAt = new DateTime(2026, 8, 13, 6, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc2.Id, Type = "deposit", Amount = 4500.00m, Label = "Återbäring Skatteverket", CreatedAt = new DateTime(2026, 8, 12, 13, 20, 0, DateTimeKind.Utc) }
                };

                db.LedgerEntries.AddRange(annaTransactions);
            }
            else
            {
                // Ensure all existing accounts have proper names and active status
                foreach (var acc in annaAccounts)
                {
                    if (string.IsNullOrWhiteSpace(acc.AccountName))
                    {
                        acc.AccountName = acc.AccountType == "saving" ? "Lönekonto" : "Sparkonto";
                    }
                    acc.Status = "active";
                }
            }
            await db.SaveChangesAsync();

            var annaReportAccounts = await db.SavingsAccounts
                .Where(a => a.CustomerId == anna.Id)
                .ToListAsync();

            var annaTaxReportTransactions = new[]
            {
                new { AccountNumber = "NOR-100001", Type = "interest", Amount = 1250.00m, Label = "Ränteutbetalning 2026", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                new { AccountNumber = "NOR-100001", Type = "tax", Amount = -375.00m, Label = "Preliminärskatt på ränta 2026", CreatedAt = new DateTime(2026, 9, 30, 0, 1, 0, DateTimeKind.Utc) },
                new { AccountNumber = "NOR-100002", Type = "interest", Amount = 300.00m, Label = "Ränteutbetalning 2026", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                new { AccountNumber = "NOR-100002", Type = "tax", Amount = -90.00m, Label = "Preliminärskatt på ränta 2026", CreatedAt = new DateTime(2026, 9, 30, 0, 1, 0, DateTimeKind.Utc) }
            };

            foreach (var transaction in annaTaxReportTransactions)
            {
                var account = annaReportAccounts.Single(a =>
                    a.AccountNumber == transaction.AccountNumber);

                var alreadyExists = await db.LedgerEntries.AnyAsync(entry =>
                    entry.AccountId == account.Id &&
                    entry.Type == transaction.Type &&
                    entry.Label == transaction.Label);

                if (!alreadyExists)
                {
                    db.LedgerEntries.Add(new LedgerEntry
                    {
                        AccountId = account.Id,
                        Type = transaction.Type,
                        Amount = transaction.Amount,
                        Label = transaction.Label,
                        CreatedAt = transaction.CreatedAt
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        // 2. Seed / Update Erik Svensson's Accounts
        var erik = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "197903142380");
        if (erik != null)
        {
            var erikAccounts = await db.SavingsAccounts
                .Where(a => a.CustomerId == erik.Id)
                .ToListAsync();

            var legacyAcc3 = erikAccounts.FirstOrDefault(a => a.AccountNumber == "DEF-345" || a.AccountNumber == "NOR-200001");
            var legacyAcc4 = erikAccounts.FirstOrDefault(a => a.AccountNumber == "GHI-456" || a.AccountNumber == "NOR-200002");

            if (legacyAcc3 != null)
            {
                legacyAcc3.AccountNumber = "NOR-200001";
                legacyAcc3.AccountName = "Sparkonto";
                legacyAcc3.AccountType = "standard";
                legacyAcc3.InterestRate = 0.0250m;
                legacyAcc3.Status = "active";
            }

            if (legacyAcc4 != null)
            {
                legacyAcc4.AccountNumber = "NOR-200002";
                legacyAcc4.AccountName = "Buffertkonto";
                legacyAcc4.AccountType = "fix";
                legacyAcc4.InterestRate = 0.0410m;
                legacyAcc4.Status = "active";
            }

            if (!erikAccounts.Any())
            {
                var acc3 = new SavingsAccount
                {
                    CustomerId = erik.Id,
                    AccountNumber = "NOR-200001",
                    AccountName = "Sparkonto",
                    AccountType = "standard",
                    Balance = 12040.00m,
                    InterestRate = 0.0250m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };
                var acc4 = new SavingsAccount
                {
                    CustomerId = erik.Id,
                    AccountNumber = "NOR-200002",
                    AccountName = "Buffertkonto",
                    AccountType = "fix",
                    Balance = 150000.00m,
                    InterestRate = 0.0410m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                db.SavingsAccounts.AddRange(acc3, acc4);
                await db.SaveChangesAsync();

                var erikTransactions = new List<LedgerEntry>
                {
                    // Account NOR-200001 transactions (Sparkonto)
                    new() { AccountId = acc3.Id, Type = "deposit", Amount = 13329.00m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -119.00m, Label = "Spotify Premium", CreatedAt = new DateTime(2026, 8, 22, 9, 14, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -890.00m, Label = "Systembolaget", CreatedAt = new DateTime(2026, 8, 21, 18, 40, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -215.00m, Label = "Apoteket Hjärtat", CreatedAt = new DateTime(2026, 8, 16, 11, 11, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -65.00m, Label = "Café Rast", CreatedAt = new DateTime(2026, 8, 11, 8, 55, 0, DateTimeKind.Utc) },

                    // Account NOR-200002 transactions (Buffertkonto)
                    new() { AccountId = acc4.Id, Type = "deposit", Amount = 147000.00m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc4.Id, Type = "deposit", Amount = 5000.00m, Label = "Överföring till buffert", CreatedAt = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = acc4.Id, Type = "withdrawal", Amount = -2000.00m, Label = "Överföring mellan konton", CreatedAt = new DateTime(2026, 8, 15, 9, 45, 0, DateTimeKind.Utc) }
                };

                db.LedgerEntries.AddRange(erikTransactions);
            }
            else
            {
                // Ensure all existing accounts have proper names and active status
                foreach (var acc in erikAccounts)
                {
                    if (string.IsNullOrWhiteSpace(acc.AccountName))
                    {
                        acc.AccountName = acc.AccountType == "fix" ? "Buffertkonto" : "Sparkonto";
                    }
                    acc.Status = "active";
                }
            }
            await db.SaveChangesAsync();
        }

        // 3. Ensure all accounts with positive balance have matching initial ledger entries (ADR 0001 Ledger Pattern sync)
        var allAccounts = await db.SavingsAccounts.ToListAsync();
        foreach (var acc in allAccounts)
        {
            var hasEntries = await db.LedgerEntries.AnyAsync(l => l.AccountId == acc.Id && !l.IsPlanned);
            if (!hasEntries && acc.Balance > 0)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    AccountId = acc.Id,
                    Type = "deposit",
                    Amount = acc.Balance,
                    Label = "Startsaldo / Initial insättning",
                    CreatedAt = acc.CreatedAt
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedNotificationsAsync(BankingDbContext db)
    {
        if (!await db.Notifications.AnyAsync())
        {
            var notifications = new List<Notification>
            {
                new()
                {
                    Recipient = "anna@example.com",
                    Type = "welcome",
                    RefId = 1,
                    Status = "SENT",
                    SentAt = DateTime.UtcNow.AddDays(-7),
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Recipient = "anna@example.com",
                    Type = "transfer",
                    RefId = 1001,
                    Status = "SENT",
                    SentAt = DateTime.UtcNow.AddDays(-1),
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    Recipient = "erik@example.com",
                    Type = "welcome",
                    RefId = 2,
                    Status = "SENT",
                    SentAt = DateTime.UtcNow.AddDays(-7),
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                }
            };

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync();
        }
    }

    private static Task SeedFaqAsync(IServiceProvider serviceProvider)
    {
        // FAQ startar tom så att administratörer kan lägga in alla frågor och svar via Admin-gränssnittet.
        return Task.CompletedTask;
    }

    private static async Task SeedOperationalMessagesAsync(BankingDbContext db)
    {
        if (!await db.OperationalMessages.AnyAsync())
        {
            var messages = new List<OperationalMessage>
            {
                new()
                {
                    TitleSv = "Planerat systemunderhåll",
                    TitleEn = "Scheduled System Maintenance",
                    MessageSv = "Söndag 28 september kl. 02:00–04:00 utför vi planerat underhåll. Vissa tjänster kan vara tillfälligt otillgängliga.",
                    MessageEn = "Sunday September 28 at 02:00–04:00 UTC, scheduled maintenance will be performed. Some services may be temporarily unavailable.",
                    Severity = "warning",
                    Priority = 10,
                    IsActive = true,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    EndDate = DateTime.UtcNow.AddDays(7),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            db.OperationalMessages.AddRange(messages);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedDocumentsAsync(IServiceProvider serviceProvider)
    {
        var inboxDb = serviceProvider.GetService<Nordiska.Modules.Inbox.Infrastructure.Db.InboxDbContext>();
        if (inboxDb == null || !await inboxDb.Database.CanConnectAsync())
        {
            return;
        }

        if (!await inboxDb.Documents.AnyAsync())
        {
            var bankingDb = serviceProvider.GetService<BankingDbContext>();
            var customer = bankingDb != null 
                ? await bankingDb.Customers.FirstOrDefaultAsync(c => c.Email == "anna@example.com" || c.PersonalNum == "198505051234")
                : null;
            var customerId = customer?.Id ?? 1L;

            var doc1 = new Nordiska.Modules.Documents.Domain.Document(
                documentType: "TaxReport",
                title: "Skatteunderlag 2025",
                fileName: "skatteunderlag-2025.pdf",
                mimeType: "application/pdf",
                storageKey: "documents/skatteunderlag-2025.pdf",
                fileSizeBytes: 245760,
                sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                sourceType: "ReportingModule"
            );

            var doc2 = new Nordiska.Modules.Documents.Domain.Document(
                documentType: "AnnualStatement",
                title: "Årsbesked 2025",
                fileName: "arsbesked-2025.pdf",
                mimeType: "application/pdf",
                storageKey: "documents/arsbesked-2025.pdf",
                fileSizeBytes: 184320,
                sha256: "ca978112ca1bbdcafac231b39a23dc4da786081951026bc70a39e2ae3f5002a6",
                sourceType: "ReportingModule"
            );

            var doc3 = new Nordiska.Modules.Documents.Domain.Document(
                documentType: "Agreement",
                title: "Allmänna kontovillkor 2026",
                fileName: "allmanna-kontovillkor-2026.pdf",
                mimeType: "application/pdf",
                storageKey: "documents/allmanna-kontovillkor-2026.pdf",
                fileSizeBytes: 312000,
                sha256: "5891b5b522d5df086d0ff0b110fbd9d21bb4fc7163af34d08286a2e846f6be03",
                sourceType: "Legal"
            );

            inboxDb.Documents.AddRange(doc1, doc2, doc3);
            await inboxDb.SaveChangesAsync();

            var cd1 = new Nordiska.Modules.Documents.Domain.CustomerDocument(doc1.Id, customerId);
            var cd2 = new Nordiska.Modules.Documents.Domain.CustomerDocument(doc2.Id, customerId);
            var cd3 = new Nordiska.Modules.Documents.Domain.CustomerDocument(doc3.Id, customerId);

            inboxDb.CustomerDocuments.AddRange(cd1, cd2, cd3);
            inboxDb.FeedItems.AddRange(
                new FeedItem(customerId, FeedItemType.Document, doc1.Id, doc1.Title, doc1.FileName, FeedPriority.Normal, false, cd1.PublishedAt),
                new FeedItem(customerId, FeedItemType.Document, doc2.Id, doc2.Title, doc2.FileName, FeedPriority.Normal, false, cd2.PublishedAt),
                new FeedItem(customerId, FeedItemType.Document, doc3.Id, doc3.Title, doc3.FileName, FeedPriority.Normal, false, cd3.PublishedAt));
            await inboxDb.SaveChangesAsync();

            if (!await inboxDb.Terms.AnyAsync())
            {
                var term2026 = new Nordiska.Modules.Agreements.Domain.Term(
                    code: "ALLMANNA_VILLKOR_2026",
                    version: 2,
                    title: "Allmänna kontovillkor 2026",
                    documentId: doc3.Id,
                    effectiveFrom: DateTimeOffset.UtcNow
                );

                inboxDb.Terms.Add(term2026);
                await inboxDb.SaveChangesAsync();

                var acceptance = new Nordiska.Modules.Agreements.Domain.TermAcceptance(term2026.Id, customerId);
                inboxDb.TermAcceptances.Add(acceptance);
                inboxDb.FeedItems.Add(new FeedItem(
                    customerId,
                    FeedItemType.Terms,
                    term2026.Id,
                    term2026.Title,
                    $"Version {term2026.Version}. Inväntar digital acceptans.",
                    FeedPriority.Important,
                    true,
                    term2026.PublishedAt));
                await inboxDb.SaveChangesAsync();
            }
        }
    }
}
