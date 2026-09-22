using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.FrontendApi.Authentication;

public class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
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

        // The BankID simulator always returns this fixed personal number,
        // regardless of what was passed in the Requirement. We need a
        // matching customer so CollectBankIdAsync can find them.
        var simulatedPersonalNum = "199908072391";
        var existingSimulated = await db.Customers
            .FirstOrDefaultAsync(c => c.PersonalNum == simulatedPersonalNum);

        if (existingSimulated == null)
        {
            var simulatedCustomer = new Customer
            {
                UserName = "simulated@bankid.se",
                Name = "BankID Simulerad",
                PersonalNum = simulatedPersonalNum,
                Email = "simulated@bankid.se",
                PhoneNumber = "+46700000000",
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(simulatedCustomer);
        }

        await SeedAccountsAndTransactionsAsync(db);
        await SeedNotificationsAsync(db);
        await SeedFaqAsync(scope.ServiceProvider);
    }

    private static async Task SeedAccountsAndTransactionsAsync(BankingDbContext db)
    {
        // Ensure standard account type configurations exist
        var standardConfigs = new[]
        {
            new AccountTypeConfig { AccountType = "flex", InterestRate = 0.0350m, Description = "Flexible savings account with variable interest rate." },
            new AccountTypeConfig { AccountType = "fix", InterestRate = 0.0410m, Description = "Fixed-term savings account with 3-month lock-in." },
            new AccountTypeConfig { AccountType = "standard", InterestRate = 0.0250m, Description = "Standard savings account for everyday savings." },
            new AccountTypeConfig { AccountType = "saving", InterestRate = 0.0350m, Description = "High-yield savings account." },
            new AccountTypeConfig { AccountType = "premium", InterestRate = 0.0400m, Description = "Premium savings account with top-tier interest rate." }
        };

        foreach (var cfg in standardConfigs)
        {
            if (!await db.AccountTypeConfigs.AnyAsync(x => x.AccountType == cfg.AccountType))
            {
                db.AccountTypeConfigs.Add(cfg);
            }
        }
        await db.SaveChangesAsync();

        var anna = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "198202116050");
        if (anna != null && !await db.SavingsAccounts.AnyAsync(a => a.CustomerId == anna.Id))
        {
            var acc1 = new SavingsAccount
            {
                CustomerId = anna.Id,
                AccountNumber = "ABC-123",
                AccountType = "flex",
                Balance = 88210.50m,
                InterestRate = 0.0350m,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            var acc2 = new SavingsAccount
            {
                CustomerId = anna.Id,
                AccountNumber = "XYZ-234",
                AccountType = "saving",
                Balance = 68099.66m,
                InterestRate = 0.0350m,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            db.SavingsAccounts.AddRange(acc1, acc2);
            await db.SaveChangesAsync();

            var annaTransactions = new List<LedgerEntry>
            {
                // Account ABC-123 transactions (Sparkonto — 1234)
                new() { AccountId = acc1.Id, Type = "deposit", Amount = 90772.50m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -412.00m, CreatedAt = new DateTime(2026, 8, 26, 3, 12, 0, DateTimeKind.Utc) },
                new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -412.00m, CreatedAt = new DateTime(2026, 8, 26, 3, 12, 0, DateTimeKind.Utc) },
                new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -540.00m, CreatedAt = new DateTime(2026, 8, 18, 20, 15, 0, DateTimeKind.Utc) },
                new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -799.00m, CreatedAt = new DateTime(2026, 8, 14, 16, 30, 0, DateTimeKind.Utc) },
                new() { AccountId = acc1.Id, Type = "withdrawal", Amount = -399.00m, CreatedAt = new DateTime(2026, 8, 10, 17, 0, 0, DateTimeKind.Utc) },

                // Account XYZ-234 transactions (Lönekonto — 9012)
                new() { AccountId = acc2.Id, Type = "deposit", Amount = 23057.08m, CreatedAt = new DateTime(2026, 8, 25, 15, 22, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "deposit", Amount = 23057.08m, CreatedAt = new DateTime(2026, 8, 25, 15, 22, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -645.50m, CreatedAt = new DateTime(2026, 8, 24, 12, 5, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -12500.00m, CreatedAt = new DateTime(2026, 8, 24, 0, 1, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "deposit", Amount = 32000.00m, CreatedAt = new DateTime(2026, 8, 23, 8, 0, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -1230.00m, CreatedAt = new DateTime(2026, 8, 19, 7, 30, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "deposit", Amount = 250.00m, CreatedAt = new DateTime(2026, 8, 17, 14, 2, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "withdrawal", Amount = -389.00m, CreatedAt = new DateTime(2026, 8, 13, 6, 0, 0, DateTimeKind.Utc) },
                new() { AccountId = acc2.Id, Type = "deposit", Amount = 4500.00m, CreatedAt = new DateTime(2026, 8, 12, 13, 20, 0, DateTimeKind.Utc) }
            };

            db.LedgerEntries.AddRange(annaTransactions);
            await db.SaveChangesAsync();
        }

        var erik = await db.Customers.FirstOrDefaultAsync(c => c.PersonalNum == "197903142380");
        if (erik != null && !await db.SavingsAccounts.AnyAsync(a => a.CustomerId == erik.Id))
        {
            var acc3 = new SavingsAccount
            {
                CustomerId = erik.Id,
                AccountNumber = "DEF-345",
                AccountType = "standard",
                Balance = 12040.00m,
                InterestRate = 0.0250m,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            var acc4 = new SavingsAccount
            {
                CustomerId = erik.Id,
                AccountNumber = "GHI-456",
                AccountType = "fix",
                Balance = 150000.00m,
                InterestRate = 0.0410m,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            db.SavingsAccounts.AddRange(acc3, acc4);
            await db.SaveChangesAsync();

            var erikTransactions = new List<LedgerEntry>
            {
                // Account DEF-345 transactions (Sparkonto — 5678)
                new() { AccountId = acc3.Id, Type = "deposit", Amount = 13329.00m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -119.00m, CreatedAt = new DateTime(2026, 8, 22, 9, 14, 0, DateTimeKind.Utc) },
                new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -890.00m, CreatedAt = new DateTime(2026, 8, 21, 18, 40, 0, DateTimeKind.Utc) },
                new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -215.00m, CreatedAt = new DateTime(2026, 8, 16, 11, 11, 0, DateTimeKind.Utc) },
                new() { AccountId = acc3.Id, Type = "withdrawal", Amount = -65.00m, CreatedAt = new DateTime(2026, 8, 11, 8, 55, 0, DateTimeKind.Utc) },

                // Account GHI-456 transactions (Buffertkonto — 3456)
                new() { AccountId = acc4.Id, Type = "deposit", Amount = 147000.00m, Label = "Ingående saldo", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                new() { AccountId = acc4.Id, Type = "deposit", Amount = 5000.00m, CreatedAt = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc) },
                new() { AccountId = acc4.Id, Type = "withdrawal", Amount = -2000.00m, CreatedAt = new DateTime(2026, 8, 15, 9, 45, 0, DateTimeKind.Utc) }
            };

            db.LedgerEntries.AddRange(erikTransactions);
            await db.SaveChangesAsync();
        }
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

    private static async Task SeedFaqAsync(IServiceProvider serviceProvider)
    {
        var faqDb = serviceProvider.GetService<FaqDbContext>();
        if (faqDb == null || !await faqDb.Database.CanConnectAsync())
        {
            return;
        }

        if (!await faqDb.FaqEntries.AnyAsync())
        {
            var faqItems = new List<FaqEntry>
            {
                FaqEntry.Create(
                    "När betalas räntan ut?",
                    "Räntan beräknas dagligen och betalas ut den 31 december varje år.",
                    "Ränta",
                    "ränta, räntan, räntesats, procent, utbetalning"
                ),
                FaqEntry.Create(
                    "Hur gör jag en insättning?",
                    "Logga in och välj Insättning / Uttag i menyn. Ange belopp och bekräfta. Pengarna syns direkt på kontot.",
                    "Insättning",
                    "insättning, insättningar, sätta, pengar, överföring"
                ),
                FaqEntry.Create(
                    "Hur gör jag ett uttag?",
                    "Logga in och välj Insättning / Uttag i menyn, välj Uttag som typ. Max 50 000 kr per transaktion.",
                    "Uttag",
                    "uttag, uttaget, ta, gräns"
                ),
                FaqEntry.Create(
                    "Var hittar jag mitt årsbesked?",
                    "Årsbeskedet ingår i skatteunderlaget. Välj Skatteunderlag i menyn och ladda ner filen för det år du vill se.",
                    "Rapporter",
                    "årsbesked, årsbeskedet, skatt, deklaration, deklarationen"
                ),
                FaqEntry.Create(
                    "Hur får jag ett kontoutdrag?",
                    "Dina senaste transaktioner visas på Mitt konto. Fullständigt kontoutdrag ingår i skatteunderlaget.",
                    "Rapporter",
                    "kontoutdrag, utdrag, transaktioner, historik"
                ),
                FaqEntry.Create(
                    "Var hittar jag villkoren för mitt sparkonto?",
                    "Villkoren finns på sidan Insättning / Uttag under Information. Fullständiga avtalsvillkor skickas per post.",
                    "Villkor",
                    "villkor, villkoren, avtal, regler"
                ),
                FaqEntry.Create(
                    "Hur öppnar jag ett nytt sparkonto?",
                    "Kontakta kundtjänst på 08-123 456 78 så hjälper vi dig att öppna ett nytt sparkonto.",
                    "Konto",
                    "öppna, öppnar, nytt, konto, skapa"
                ),
                FaqEntry.Create(
                    "Hur avslutar jag mitt sparkonto?",
                    "Ta ut hela saldot och kontakta sedan kundtjänst på 08-123 456 78 för att avsluta kontot.",
                    "Konto",
                    "avsluta, avslutar, stänga, säga, upp"
                )
            };

            faqDb.FaqEntries.AddRange(faqItems);
            await faqDb.SaveChangesAsync();
        }
    }
}
