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
        
        var hostEnv = serviceProvider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        var isDevOrTest = hostEnv == null || hostEnv.IsDevelopment() || hostEnv.EnvironmentName == "Testing";

        var adminPassword = configuration?["AdminUser:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Contains("PLACEHOLDER"))
        {
            if (!isDevOrTest)
            {
                throw new InvalidOperationException("AdminUser:Password must be configured via environment variable or secret in production.");
            }
            adminPassword = "password123";
        }

        // 1. Ensure StaffMember table contains bootstrap administrator (RBAC)
        var existingStaff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Email == adminEmail);
        var passwordHasher = new BCryptPasswordHasher<StaffMember>();
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
            adminStaff.PasswordHash = passwordHasher.HashPassword(adminStaff, adminPassword);
            db.StaffMembers.Add(adminStaff);
            await db.SaveChangesAsync();
        }
        else if (!existingStaff.PasswordHash.StartsWith("$2"))
        {
            // Upgrade legacy PBKDF2 hash to BCrypt (DoD requirement)
            existingStaff.PasswordHash = passwordHasher.HashPassword(existingStaff, adminPassword);
            await db.SaveChangesAsync();
        }

        // 2. Clean up legacy admin ghost customer from Customers table if present (ensures strict RBAC separation)
        var legacyAdminCustomer = await db.Customers.FirstOrDefaultAsync(c => c.Email == adminEmail || c.UserName == adminEmail);
        if (legacyAdminCustomer != null)
        {
            db.Customers.Remove(legacyAdminCustomer);
            await db.SaveChangesAsync();
        }

        var anna = await userManager.FindByEmailAsync("anna@example.com");
        if (anna != null && await userManager.IsInRoleAsync(anna, "Admin"))
        {
            await userManager.RemoveFromRoleAsync(anna, "Admin");
        }

        // 3. Ensure official bilingual FAQ entries exist across all environments (Development, Staging, Production)
        try
        {
            await SeedFaqAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<DbInitializer>>();
            logger?.LogWarning(ex, "Could not seed FAQ entries during startup: {Message}", ex.Message);
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

            await userManager.CreateAsync(testCustomer, "password123");
        }
        else
        {
            existingCustomer.UserName = "anna@example.com";
            existingCustomer.Email = "anna@example.com";
            existingCustomer.PersonalNum = testPersonalNum;
            existingCustomer.NormalizedUserName = "ANNA@EXAMPLE.COM";
            existingCustomer.NormalizedEmail = "ANNA@EXAMPLE.COM";
            if (string.IsNullOrEmpty(existingCustomer.PasswordHash))
            {
                await userManager.AddPasswordAsync(existingCustomer, "password123");
            }
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

            await userManager.CreateAsync(testErik, "password123");
        }
        else
        {
            existingErik.UserName = "erik@example.com";
            existingErik.Email = "erik@example.com";
            existingErik.NormalizedUserName = "ERIK@EXAMPLE.COM";
            existingErik.NormalizedEmail = "ERIK@EXAMPLE.COM";
            if (string.IsNullOrEmpty(existingErik.PasswordHash))
            {
                await userManager.AddPasswordAsync(existingErik, "password123");
            }
            await db.SaveChangesAsync();
        }

        var dennisPersonalNum = "198908092383";
        var existingDennis = await db.Customers
            .FirstOrDefaultAsync(c => c.PersonalNum == dennisPersonalNum);

        if (existingDennis == null)
        {
            var testDennis = new Customer
            {
                UserName = "dennis@demo.com",
                Name = "Dennis Svensson",
                PersonalNum = dennisPersonalNum,
                Email = "dennis@demo.com",
                PhoneNumber = "+46708901234",
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(testDennis, "password123");
        }
        else
        {
            existingDennis.UserName = "dennis@demo.com";
            existingDennis.Email = "dennis@demo.com";
            existingDennis.NormalizedUserName = "DENNIS@DEMO.COM";
            existingDennis.NormalizedEmail = "DENNIS@DEMO.COM";
            if (string.IsNullOrEmpty(existingDennis.PasswordHash))
            {
                await userManager.AddPasswordAsync(existingDennis, "password123");
            }
            await db.SaveChangesAsync();
        }

        var peterPersonalNum = "198404142385";
        var existingPeter = await db.Customers
            .FirstOrDefaultAsync(c => c.PersonalNum == peterPersonalNum);

        if (existingPeter == null)
        {
            var testPeter = new Customer
            {
                UserName = "peter@demo.com",
                Name = "Peter Hansson",
                PersonalNum = peterPersonalNum,
                Email = "peter@demo.com",
                PhoneNumber = "+46708456789",
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(testPeter, "password123");
        }
        else
        {
            existingPeter.UserName = "peter@demo.com";
            existingPeter.Email = "peter@demo.com";
            existingPeter.NormalizedUserName = "PETER@DEMO.COM";
            existingPeter.NormalizedEmail = "PETER@DEMO.COM";
            if (string.IsNullOrEmpty(existingPeter.PasswordHash))
            {
                await userManager.AddPasswordAsync(existingPeter, "password123");
            }
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

        // 3. Seed / Update Dennis Svensson's Accounts
        var dennis = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "198908092383");
        if (dennis != null)
        {
            var dennisAccounts = await db.SavingsAccounts
                .Where(a => a.CustomerId == dennis.Id)
                .ToListAsync();

            var dennisSalaryAcc = dennisAccounts.FirstOrDefault(a => a.AccountNumber == "NOR-300001");
            var dennisSavingsAcc = dennisAccounts.FirstOrDefault(a => a.AccountNumber == "NOR-300002");

            if (dennisSalaryAcc != null)
            {
                dennisSalaryAcc.AccountName = "Lönekonto";
                dennisSalaryAcc.AccountType = "standard";
                dennisSalaryAcc.InterestRate = 0.0250m;
                dennisSalaryAcc.Status = "active";
            }

            if (dennisSavingsAcc != null)
            {
                dennisSavingsAcc.AccountName = "Sparkonto";
                dennisSavingsAcc.AccountType = "saving";
                dennisSavingsAcc.InterestRate = 0.0350m;
                dennisSavingsAcc.Status = "active";
            }

            if (!dennisAccounts.Any())
            {
                dennisSalaryAcc = new SavingsAccount
                {
                    CustomerId = dennis.Id,
                    AccountNumber = "NOR-300001",
                    AccountName = "Lönekonto",
                    AccountType = "standard",
                    Balance = 34850.00m,
                    InterestRate = 0.0250m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };
                dennisSavingsAcc = new SavingsAccount
                {
                    CustomerId = dennis.Id,
                    AccountNumber = "NOR-300002",
                    AccountName = "Sparkonto",
                    AccountType = "saving",
                    Balance = 75000.00m,
                    InterestRate = 0.0350m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                db.SavingsAccounts.AddRange(dennisSalaryAcc, dennisSavingsAcc);
                await db.SaveChangesAsync();

                var dennisTransactions = new List<LedgerEntry>
                {
                    // NOR-300001 (Lönekonto)
                    new() { AccountId = dennisSalaryAcc.Id, Type = "deposit", Amount = 15685.50m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = dennisSalaryAcc.Id, Type = "deposit", Amount = 31500.00m, Label = "Lön", CreatedAt = new DateTime(2026, 8, 25, 8, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = dennisSalaryAcc.Id, Type = "withdrawal", Amount = -9800.00m, Label = "Hyra", CreatedAt = new DateTime(2026, 8, 27, 0, 1, 0, DateTimeKind.Utc) },
                    new() { AccountId = dennisSalaryAcc.Id, Type = "withdrawal", Amount = -1420.50m, Label = "ICA Maxi", CreatedAt = new DateTime(2026, 8, 28, 16, 30, 0, DateTimeKind.Utc) },
                    new() { AccountId = dennisSalaryAcc.Id, Type = "withdrawal", Amount = -1020.00m, Label = "SL Månadsbiljett", CreatedAt = new DateTime(2026, 8, 29, 7, 15, 0, DateTimeKind.Utc) },
                    new() { AccountId = dennisSalaryAcc.Id, Type = "withdrawal", Amount = -95.00m, Label = "Espresso House", CreatedAt = new DateTime(2026, 8, 30, 11, 45, 0, DateTimeKind.Utc) },

                    // NOR-300002 (Sparkonto)
                    new() { AccountId = dennisSavingsAcc.Id, Type = "deposit", Amount = 73000.00m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = dennisSavingsAcc.Id, Type = "deposit", Amount = 2000.00m, Label = "Månadssparande", CreatedAt = new DateTime(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc) }
                };

                db.LedgerEntries.AddRange(dennisTransactions);
                await db.SaveChangesAsync();
            }

            // Planned transaction for Dennis
            var hasDennisPlanned = await db.LedgerEntries.AnyAsync(l => l.AccountId == dennisSalaryAcc!.Id && l.IsPlanned);
            if (!hasDennisPlanned && dennisSalaryAcc != null && dennisSavingsAcc != null)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    AccountId = dennisSalaryAcc.Id,
                    Type = "transfer",
                    Amount = 2000.00m,
                    Label = "Månadssparande",
                    TargetAccountId = dennisSavingsAcc.Id,
                    IsPlanned = true,
                    Repeating = "monthly",
                    PlannedDate = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc),
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // Savings goal for Dennis
            var hasDennisGoal = await db.SavingsGoals.AnyAsync(g => g.CustomerId == dennis.Id && g.AccountId == dennisSavingsAcc!.Id);
            if (!hasDennisGoal && dennisSavingsAcc != null)
            {
                db.SavingsGoals.Add(new SavingsGoal
                {
                    CustomerId = dennis.Id,
                    AccountId = dennisSavingsAcc.Id,
                    Title = "Sommarsemester 2027",
                    TargetAmount = 30000.00m,
                    CurrentAmount = 24500.00m,
                    TargetDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                    Status = "active",
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        // 4. Seed / Update Peter Hansson's Accounts
        var peter = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "198404142385");
        if (peter != null)
        {
            var peterAccounts = await db.SavingsAccounts
                .Where(a => a.CustomerId == peter.Id)
                .ToListAsync();

            var peterSalaryAcc = peterAccounts.FirstOrDefault(a => a.AccountNumber == "NOR-400001");
            var peterSavingsAcc = peterAccounts.FirstOrDefault(a => a.AccountNumber == "NOR-400002");

            if (peterSalaryAcc != null)
            {
                peterSalaryAcc.AccountName = "Lönekonto";
                peterSalaryAcc.AccountType = "standard";
                peterSalaryAcc.InterestRate = 0.0250m;
                peterSalaryAcc.Status = "active";
            }

            if (peterSavingsAcc != null)
            {
                peterSavingsAcc.AccountName = "Sparkonto";
                peterSavingsAcc.AccountType = "saving";
                peterSavingsAcc.InterestRate = 0.0350m;
                peterSavingsAcc.Status = "active";
            }

            if (!peterAccounts.Any())
            {
                peterSalaryAcc = new SavingsAccount
                {
                    CustomerId = peter.Id,
                    AccountNumber = "NOR-400001",
                    AccountName = "Lönekonto",
                    AccountType = "standard",
                    Balance = 34850.00m,
                    InterestRate = 0.0250m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };
                peterSavingsAcc = new SavingsAccount
                {
                    CustomerId = peter.Id,
                    AccountNumber = "NOR-400002",
                    AccountName = "Sparkonto",
                    AccountType = "saving",
                    Balance = 75000.00m,
                    InterestRate = 0.0350m,
                    Status = "active",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                db.SavingsAccounts.AddRange(peterSalaryAcc, peterSavingsAcc);
                await db.SaveChangesAsync();

                var peterTransactions = new List<LedgerEntry>
                {
                    // NOR-400001 (Lönekonto)
                    new() { AccountId = peterSalaryAcc.Id, Type = "deposit", Amount = 15685.50m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = peterSalaryAcc.Id, Type = "deposit", Amount = 31500.00m, Label = "Lön", CreatedAt = new DateTime(2026, 8, 25, 8, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = peterSalaryAcc.Id, Type = "withdrawal", Amount = -9800.00m, Label = "Hyra", CreatedAt = new DateTime(2026, 8, 27, 0, 1, 0, DateTimeKind.Utc) },
                    new() { AccountId = peterSalaryAcc.Id, Type = "withdrawal", Amount = -1420.50m, Label = "ICA Maxi", CreatedAt = new DateTime(2026, 8, 28, 16, 30, 0, DateTimeKind.Utc) },
                    new() { AccountId = peterSalaryAcc.Id, Type = "withdrawal", Amount = -1020.00m, Label = "SL Månadsbiljett", CreatedAt = new DateTime(2026, 8, 29, 7, 15, 0, DateTimeKind.Utc) },
                    new() { AccountId = peterSalaryAcc.Id, Type = "withdrawal", Amount = -95.00m, Label = "Espresso House", CreatedAt = new DateTime(2026, 8, 30, 11, 45, 0, DateTimeKind.Utc) },

                    // NOR-400002 (Sparkonto)
                    new() { AccountId = peterSavingsAcc.Id, Type = "deposit", Amount = 73000.00m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new() { AccountId = peterSavingsAcc.Id, Type = "deposit", Amount = 2000.00m, Label = "Månadssparande", CreatedAt = new DateTime(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc) }
                };

                db.LedgerEntries.AddRange(peterTransactions);
                await db.SaveChangesAsync();
            }

            // Planned transaction for Peter
            var hasPeterPlanned = await db.LedgerEntries.AnyAsync(l => l.AccountId == peterSalaryAcc!.Id && l.IsPlanned);
            if (!hasPeterPlanned && peterSalaryAcc != null && peterSavingsAcc != null)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    AccountId = peterSalaryAcc.Id,
                    Type = "transfer",
                    Amount = 2000.00m,
                    Label = "Månadssparande",
                    TargetAccountId = peterSavingsAcc.Id,
                    IsPlanned = true,
                    Repeating = "monthly",
                    PlannedDate = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc),
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // Savings goal for Peter
            var hasPeterGoal = await db.SavingsGoals.AnyAsync(g => g.CustomerId == peter.Id && g.AccountId == peterSavingsAcc!.Id);
            if (!hasPeterGoal && peterSavingsAcc != null)
            {
                db.SavingsGoals.Add(new SavingsGoal
                {
                    CustomerId = peter.Id,
                    AccountId = peterSavingsAcc.Id,
                    Title = "Sommarsemester 2027",
                    TargetAmount = 30000.00m,
                    CurrentAmount = 24500.00m,
                    TargetDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                    Status = "active",
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        // 5. Ensure all accounts with positive balance have matching initial ledger entries (ADR 0001 Ledger Pattern sync)
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

    public static async Task SeedFaqAsync(IServiceProvider serviceProvider)
    {
        var faqDb = serviceProvider.GetService<FaqDbContext>();
        if (faqDb == null || !await faqDb.Database.CanConnectAsync())
        {
            return;
        }

        var faqPairs = new (string SvQ, string SvA, string SvCat, string SvKey, string EnQ, string EnA, string EnCat, string EnKey, Guid RelationId, int SvHelpful, int EnHelpful)[]
        {
            (
                "Hur gör jag ett uttag?",
                "Du kan enkelt göra uttag och överföringar under fliken “Överföringar”. Där kan du föra över pengar mellan dina egna konton eller till dina sparade mottagare. Du kan också schemalägga återkommande överföringar veckovis, månadsvis eller årsvis.",
                "Överföringar",
                "uttag, uttagning, ta ut pengar, överföring, föra över pengar, överföra pengar, flytta pengar, ta ut, uttaget, uttagsgräns, stående överföring, återkommande överföring, schemalägga överföring, mottagare, mellan konton",
                "How do I withdraw money?",
                "You can easily make withdrawals and transfers under the “Transfers” tab. There, you can transfer money between your own accounts or to your saved recipients. You can also schedule recurring transfers on a weekly, monthly, or yearly basis.",
                "Transfers",
                "withdrawal, withdrawals, withdraw money, take out money, transfer, money transfer, transfer money, move money, withdrawal limit, recurring transfer, scheduled transfer, standing transfer, saved recipient, between accounts",
                Guid.Parse("a0000001-0000-0000-0000-000000000001"),
                8,
                4
            ),
            (
                "När betalas räntan ut?",
                "Räntan beräknas dagligen och betalas ut den 31 december varje år.",
                "Ränta",
                "ränta, räntan, räntesats, procent, utbetalning",
                "When is interest paid?",
                "Interest is calculated daily and paid on December 31st each year.",
                "Interest",
                "interest, rate, payment, percentage, yield",
                Guid.Parse("a0000001-0000-0000-0000-000000000002"),
                9,
                6
            ),
            (
                "Hur gör jag en insättning?",
                "Gå till fliken “Överföringar” och välj alternativet för insättning. Ange beloppet och bekräfta transaktionen. När insättningen har genomförts visas pengarna på ditt konto.",
                "Överföringar",
                "insättning, sätta in pengar, sätt in pengar, insätta pengar, överföra pengar, fylla på konto, insättningsbelopp, insättningsalternativ, transaktion",
                "How do I make a deposit?",
                "Go to the Transfers tab and select the deposit option. Enter the amount and confirm the transaction. Once the deposit has been completed, the money will appear in your account.",
                "Transfers",
                "deposit, make a deposit, deposit money, add money, transfer money, top up account, deposit amount, deposit option, transaction",
                Guid.Parse("a0000001-0000-0000-0000-000000000003"),
                5,
                2
            ),
            (
                "Var hittar jag mitt årsbesked?",
                "Ditt årsbesked ingår i skatteunderlaget. Du kan ladda ner skatteunderlaget under fliken “Bankkonton” genom att klicka på knappen “Skatteunderlag” högst upp på sidan.\nDu hittar även ditt årsbesked i din inbox under fliken “Dokument”.",
                "Rapporter",
                "årsbesked, årsbeskedet, skatteunderlag, skatteunderlaget, skatt, deklaration, deklarationen, skattedokument, dokument, ladda ner årsbesked, hitta årsbesked, bankkonton, inkorg, räntebesked",
                "Where can I find my annual statement?",
                "Your annual statement is included in your tax statement. You can download the tax statement under the \"Bank Accounts\" tab by clicking the \"Tax Report\" button at the top of the page.\nYou can also find your annual statement in your inbox under the \"Documents\" tab.",
                "Reports",
                "annual statement, year-end statement, tax statement, tax documentation, tax return, tax declaration, download annual statement, find annual statement, bank accounts, inbox, documents, interest statement",
                Guid.Parse("a0000001-0000-0000-0000-000000000004"),
                7,
                -2
            ),
            (
                "Hur får jag ett kontoutdrag?",
                "Gå till fliken \"Bankkonton\" och välj det konto du vill ha ett kontoutdrag för i menyn. Längst ner på sidan hittar du knappen \"Kontoutdrag\", där du kan skapa och ladda ner ett kontoutdrag för det valda kontot.",
                "Rapporter",
                "kontoutdrag, kontoutdraget, utdrag, bankutdrag, kontoöversikt, transaktioner, transaktionshistorik, kontohistorik, betalningshistorik, skapa kontoutdrag, ladda ner kontoutdrag, bankkonton",
                "How do I get an account statement?",
                "Go to the \"Bank Accounts\" tab and select the account you want an account statement for from the menu. At the bottom of the page, you will find the \"Account Statement\" button, where you can generate a statement for the selected account.",
                "Reports",
                "account statement, bank statement, statement, transaction history, transactions, account history, payment history, generate account statement, download account statement, bank accounts",
                Guid.Parse("a0000001-0000-0000-0000-000000000005"),
                6,
                1
            ),
            (
                "Var hittar jag villkoren för mitt sparkonto?",
                "Du hittar villkoren för ditt sparkonto i din inkorg under fliken \"Villkor\". Öppna inkorgen genom att klicka på ikonen längst upp till höger i menyn.",
                "Villkor",
                "villkor, villkoren, sparkonto, sparkontovillkor, kontoavtal, avtal, regler, avtalsvillkor, läsa villkor, hitta villkor, inkorg",
                "Where can I find the terms for my savings account?",
                "You can find the terms and conditions for your savings account in your inbox under the \"Terms and Conditions\" tab. Open your inbox by clicking the icon in the top-right corner of the menu.",
                "Terms",
                "terms and conditions, savings account, account terms, agreement, account agreement, rules, contractual terms, view terms, find terms, inbox",
                Guid.Parse("a0000001-0000-0000-0000-000000000006"),
                -3,
                3
            ),
            (
                "Hur öppnar jag ett nytt sparkonto?",
                "Gå till fliken \"Bankkonton\" och klicka på knappen \"Nytt Konto\". Välj vilken typ av konto du vill öppna. Du kan också välja att namnge kontot om du vill.",
                "Konton",
                "öppna konto, öppna sparkonto, nytt konto, nytt sparkonto, skapa konto, skapa sparkonto, lägga till konto, bankkonto, sparkonto, kontotyp, namnge konto, nytt bankkonto",
                "How do I open a new savings account?",
                "Go to the \"Bank Accounts\" tab and click the \"New Account\" button. Choose the type of account you want to open. You can also give the account a name if you wish.",
                "Accounts",
                "open account, open savings account, new account, new savings account, create account, create savings account, add account, bank account, savings account, account type, name an account, new bank account",
                Guid.Parse("a0000001-0000-0000-0000-000000000007"),
                10,
                8
            ),
            (
                "Hur avslutar jag mitt sparkonto?",
                "För att avsluta ditt sparkonto behöver du först ta ut hela saldot. Gå till fliken \"Bankkonton\" och välj det konto du vill avsluta i menyn. När saldot är 0,00 kr visas en röd knapp längst ner på sidan med texten \"Avsluta Konto\". Klicka på knappen för att avsluta kontot.",
                "Konton",
                "avsluta konto, avsluta sparkonto, stänga konto, stänga sparkonto, säga upp konto, ta bort konto, avsluta kontot, kontosaldo, nollsaldo, saldo, ta ut pengar, Avsluta Konto",
                "How do I close my savings account?",
                "To close your savings account, you must first withdraw the entire balance. Go to the \"Bank Accounts\" tab and select the account you want to close from the menu. When the balance is 0.00 SEK, a red button labelled \"Close Account\" will appear at the bottom of the page. Click the button to close the account.",
                "Accounts",
                "close account, close savings account, closing an account, terminate account, cancel account, remove account, account balance, zero balance, withdraw money, empty account, Close Account",
                Guid.Parse("a0000001-0000-0000-0000-000000000008"),
                -5,
                -8
            ),
            (
                "Vilka villkor gäller för mitt sparkonto?",
                "Du hittar de aktuella villkoren för ditt sparkonto i din inkorg under fliken \"Villkor\". Öppna inkorgen genom att klicka på ikonen längst upp till höger i menyn.",
                "Villkor",
                "villkor, sparkonto, sparkontovillkor, kontovillkor, regler, avgifter, ränta, avtalsvillkor, kontoavtal, aktuella villkor, läsa villkor, hitta villkor, dokument, inkorg",
                "What terms and conditions apply to my savings account?",
                "You can find the current terms and conditions for your savings account in your inbox under the \"Terms and Conditions\" tab. Open your inbox by clicking the icon in the top-right corner of the menu.",
                "Terms",
                "terms and conditions, savings account, account terms, account rules, fees, interest rate, contractual terms, account agreement, current terms, view terms, find terms, documents, inbox",
                Guid.Parse("a0000001-0000-0000-0000-000000000009"),
                -1,
                -4
            ),
            (
                "Kan jag hantera mitt sparkonto via mobilen?",
                "Ja, du kan hantera ditt sparkonto direkt i mobilen. Webbplatsen är mobilanpassad och fungerar lika bra på dator, surfplatta och mobiltelefon.",
                "Hemsidan",
                "hemsidan, Nordiska, mobil, mobilanpassad, sparkonto, saldo, kontohantering",
                "Can I manage my savings account from my mobile phone?",
                "Yes, you can manage your savings account directly from your mobile phone. The website is mobile-friendly and works just as well on a computer, tablet, or mobile phone.",
                "Website",
                "website, Nordiska, mobile, mobile friendly, savings account, balance, account management",
                Guid.Parse("a0000001-0000-0000-0000-000000000010"),
                4,
                -1
            ),
            (
                "Är Nordiskas hemsida säker att använda?",
                "Ja, Nordiskas hemsida är utformad med säkerhet i fokus. Om du glömmer att logga ut själv loggas du automatiskt ut efter en period av inaktivitet. Det minskar risken för att någon annan kan få tillgång till ditt konto om du lämnar sidan öppen.",
                "Hemsidan",
                "hemsidan, säkerhet, säker, automatisk utloggning, logga ut, inaktivitet, konto, trygg",
                "Is Nordiska's website safe to use?",
                "Yes, Nordiska's website is designed with security in mind. If you forget to log out yourself, you will be automatically logged out after a period of inactivity. This helps reduce the risk of someone else accessing your account if you leave the website open.",
                "Website",
                "website, security, safe, automatic logout, log out, inactivity, account, secure",
                Guid.Parse("a0000001-0000-0000-0000-000000000011"),
                3,
                5
            ),
            (
                "Hur skapar jag ett sparmål?",
                "Gå till fliken \"Bankkonton\" och välj det konto du vill skapa ett sparmål för. Klicka på knappen \"Skapa sparmål\". Ange ett namn på sparmålet och det belopp du vill spara ihop. Du kan även ange ett måldatum om du vill.\nNär sparmålet har skapats kan du följa hur ditt sparande utvecklas och se hur nära du är att nå ditt mål. Du kan när som helst redigera eller ta bort ditt sparmål.",
                "Konton",
                "sparmål, skapa sparmål, nytt sparmål, sparande, sparmål för konto, målnamn, sparbelopp, målsumma, måldatum, följa sparande, sparutveckling, redigera sparmål, ändra sparmål, ta bort sparmål, radera sparmål, Skapa sparmål, Bankkonton",
                "How do I create a savings goal?",
                "Go to the \"Bank Accounts\" tab and select the account you want to create a savings goal for. Click the \"Create Savings Goal\" button. Enter a name for your savings goal and the amount you want to save. You can also set a target date if you wish.\nOnce your savings goal has been created, you can track your savings progress and see how close you are to reaching your goal. You can edit or delete your savings goal at any time.",
                "Accounts",
                "savings goal, create savings goal, new savings goal, savings, account savings goal, goal name, savings amount, target amount, target date, track savings, savings progress, edit savings goal, update savings goal, delete savings goal, remove savings goal, Create Savings Goal, Bank Accounts",
                Guid.Parse("a0000001-0000-0000-0000-000000000012"),
                6,
                2
            ),
            (
                "Var kan jag se mina transaktioner?",
                "Under fliken \"Transaktioner\" hittar du din samlade transaktionshistorik. Du kan filtrera transaktionerna efter ett specifikt konto, en tidsperiod eller transaktionstyp, till exempel insättningar och uttag.\nDu kan även se transaktionshistoriken för avslutade konton genom att markera \"Visa stängda konton\" i kontofiltret.",
                "Transaktioner",
                "transaktioner, transaktionshistorik, transaktionshistorik konto, kontohistorik, betalningshistorik, insättning, insättningar, uttag, uttagshistorik, filtrera transaktioner, söka transaktioner, transaktionstyp, tidsperiod, avslutade konton, stängda konton, Visa stängda konton",
                "Where can I view my transactions?",
                "Under the \"Transactions\" tab, you can find your complete transaction history. You can filter transactions by a specific account, a date range, or transaction type, such as deposits and withdrawals.\nYou can also view the transaction history of closed accounts by selecting \"Show closed accounts\" in the account filter.",
                "Transactions",
                "transactions, transaction history, account history, payment history, deposit, deposits, withdrawal, withdrawals, filter transactions, search transactions, transaction type, date range, closed accounts, view closed accounts, Show closed accounts",
                Guid.Parse("a0000001-0000-0000-0000-000000000013"),
                4,
                1
            ),
            (
                "Var kan jag ändra mina kontaktuppgifter?",
                "Klicka på hamburgermenyn uppe i högra hörnet och välj \"Inställningar\". Där kan du uppdatera din e-postadress och ditt telefonnummer.",
                "Inställningar",
                "kontaktuppgifter, ändra kontaktuppgifter, uppdatera kontaktuppgifter, e-post, e-postadress, ändra e-postadress, uppdatera e-post, telefonnummer, ändra telefonnummer, uppdatera telefonnummer, inställningar, personuppgifter, kontaktinformation, hamburgermeny",
                "Where can I update my contact details?",
                "Click the hamburger menu in the top-right corner and select \"Settings\". There, you can update your email address and phone number.",
                "Settings",
                "contact details, update contact details, change contact information, email, email address, change email address, update email, phone number, change phone number, update phone number, settings, personal information, contact information, hamburger menu",
                Guid.Parse("a0000001-0000-0000-0000-000000000014"),
                2,
                0
            ),
            (
                "Hur ändrar jag språk på hemsidan?",
                "Klicka på hamburgermenyn uppe i högra hörnet. Längst ner i menyn hittar du språkinställningarna, där du kan välja vilket språk du vill använda. Nordiska.se stöder för närvarande svenska och engelska.",
                "Inställningar",
                "språk, byta språk, ändra språk, språkinställningar, språkval, svenska, engelska, hemsida, webbplats, språk på hemsidan, ändra språk på webbplatsen, inställningar, hamburgermeny",
                "How do I change the language on the website?",
                "Click the hamburger menu in the top-right corner. At the bottom of the menu, you will find the language settings, where you can choose your preferred language. Nordiska.se currently supports Swedish and English.",
                "Settings",
                "language, change language, switch language, language settings, language selection, Swedish, English, website, change website language, language preferences, settings, hamburger menu",
                Guid.Parse("a0000001-0000-0000-0000-000000000015"),
                1,
                3
            ),
            (
                "Var loggar jag ut?",
                "Du loggar ut genom att klicka på \"Logga ut\" i hamburgermenyn uppe i högra hörnet.",
                "Hemsidan",
                "logga ut, loggar ut, utloggning, avsluta session, lämna kontot, logga ut från kontot, utloggningsknapp, Logga ut, hamburgermeny",
                "Where can I log out?",
                "You can log out by clicking \"Log Out\" in the hamburger menu in the top-right corner.",
                "Website",
                "log out, logout, sign out, log off, end session, leave account, log out of account, sign-out button, Log Out, hamburger menu",
                Guid.Parse("a0000001-0000-0000-0000-000000000016"),
                -2,
                -6
            ),
            (
                "Var hanterar jag mina planerade överföringar?",
                "Under fliken \"Överföringar\" hittar du sektionen för dina planerade överföringar. Där kan du ändra datum och intervall för en planerad överföring eller ta bort den om du inte längre behöver den.",
                "Överföringar",
                "planerade överföringar, planerad överföring, schemalagda överföringar, återkommande överföring, stående överföring, hantera överföringar, redigera överföring, ändra överföring, ändra datum, ändra intervall, ta bort överföring, avbryta överföring, överföringsdatum, överföringsintervall",
                "Where can I manage my scheduled transfers?",
                "Under the \"Transfers\" tab, you will find the section for your scheduled transfers. There, you can change the date and frequency of a scheduled transfer or delete it if you no longer need it.",
                "Transfers",
                "scheduled transfers, scheduled transfer, recurring transfers, recurring transfer, standing order, manage transfers, edit transfer, update transfer, change date, change frequency, delete transfer, cancel transfer, transfer date, transfer frequency",
                Guid.Parse("a0000001-0000-0000-0000-000000000017"),
                5,
                -3
            )
        };

        foreach (var pair in faqPairs)
        {
            var existingSv = await faqDb.FaqEntries.FirstOrDefaultAsync(f => (f.Question == pair.SvQ || f.RelationId == pair.RelationId) && f.Language == "sv");
            if (existingSv == null)
            {
                var entry = FaqEntry.Create(pair.SvQ, pair.SvA, pair.SvCat, pair.SvKey, "sv", pair.RelationId);
                faqDb.Entry(entry).Property(e => e.HelpfulCount).CurrentValue = pair.SvHelpful;
                faqDb.FaqEntries.Add(entry);
            }
            else
            {
                existingSv.ReviseEntry(pair.SvQ, pair.SvA, pair.SvCat, pair.SvKey, "sv");
                faqDb.Entry(existingSv).Property(e => e.HelpfulCount).CurrentValue = pair.SvHelpful;
            }

            var existingEn = await faqDb.FaqEntries.FirstOrDefaultAsync(f => (f.Question == pair.EnQ || f.RelationId == pair.RelationId) && f.Language == "en");
            if (existingEn == null)
            {
                var entry = FaqEntry.Create(pair.EnQ, pair.EnA, pair.EnCat, pair.EnKey, "en", pair.RelationId);
                faqDb.Entry(entry).Property(e => e.HelpfulCount).CurrentValue = pair.EnHelpful;
                faqDb.FaqEntries.Add(entry);
            }
            else
            {
                existingEn.ReviseEntry(pair.EnQ, pair.EnA, pair.EnCat, pair.EnKey, "en");
                faqDb.Entry(existingEn).Property(e => e.HelpfulCount).CurrentValue = pair.EnHelpful;
            }
        }

        await faqDb.SaveChangesAsync();
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
