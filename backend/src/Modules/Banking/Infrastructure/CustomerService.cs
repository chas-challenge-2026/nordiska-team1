using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class CustomerService : ICustomerService
{
    private readonly BankingDbContext _db;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(BankingDbContext db, ILogger<CustomerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Customer> CreateAsync(string name, string email, string personalNum, CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            Name = name,
            Email = email,
            UserName = email,
            PersonalNum = personalNum,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = string.Empty
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created customer {Id}", customer.Id);

        return customer;
    }

    public async Task<Customer> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
            throw new KeyNotFoundException($"Customer with id {id} was not found.");

        return customer;
    }

    public async Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
            throw new KeyNotFoundException($"Customer with id {id} was not found.");

        if (!string.IsNullOrWhiteSpace(name)) customer.Name = name!;
        if (!string.IsNullOrWhiteSpace(email)) customer.Email = email!;
        if (!string.IsNullOrWhiteSpace(personalNum)) customer.PersonalNum = personalNum!;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated customer {Id}", id);

        return customer;
    }
}
