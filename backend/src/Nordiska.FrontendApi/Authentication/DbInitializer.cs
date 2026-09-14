using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

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
    }
}
