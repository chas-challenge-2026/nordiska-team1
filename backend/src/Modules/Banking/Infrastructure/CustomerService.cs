using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class CustomerService : ICustomerService
{
    private readonly BankingDbContext _db;
    private readonly ILogger<CustomerService> _logger;
    private readonly UserManager<Customer> _userManager;

    public CustomerService(BankingDbContext db, ILogger<CustomerService> logger, UserManager<Customer> userManager)
    {
        _db = db;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<Customer> CreateAsync(string name, string email, string personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            Name = name,
            Email = email,
            UserName = email,
            PersonalNum = personalNum,
            PhoneNumber = phoneNumber,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = string.Empty
        };

        var result = await _userManager.CreateAsync(customer);
        if (!result.Succeeded)
        {
            var errors = string.Join(';', result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user: {Errors}", errors);
            throw new InvalidOperationException($"Unable to create user: {errors}");
        }

        _logger.LogInformation("Created customer {Id}", customer.Id);

        return customer;
    }

    public async Task<Customer> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        // Use UserManager to ensure we get Identity-managed user
        var customer = await _userManager.FindByIdAsync(id.ToString());
        if (customer is null)
            throw new KeyNotFoundException($"Customer with id {id} was not found.");

        return customer;
    }

    public async Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        var customer = await _userManager.FindByIdAsync(id.ToString());
        if (customer is null)
            throw new KeyNotFoundException($"Customer with id {id} was not found.");

        if (!string.IsNullOrWhiteSpace(name)) customer.Name = name!;
        if (!string.IsNullOrWhiteSpace(email))
        {
            customer.Email = email!;
            customer.UserName = email!;
        }
        if (!string.IsNullOrWhiteSpace(personalNum)) customer.PersonalNum = personalNum!;
        if (!string.IsNullOrWhiteSpace(phoneNumber)) customer.PhoneNumber = phoneNumber!;

        var result = await _userManager.UpdateAsync(customer);
        if (!result.Succeeded)
        {
            var errors = string.Join(';', result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to update user {Id}: {Errors}", id, errors);
            throw new InvalidOperationException($"Unable to update user: {errors}");
        }

        _logger.LogInformation("Updated customer {Id}", id);

        return customer;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var customer = await _userManager.FindByIdAsync(id.ToString());
        if (customer is null)
            throw new KeyNotFoundException($"Customer with id {id} was not found.");

        var hasActiveBalance = await _db.SavingsAccounts
            .AnyAsync(a => a.CustomerId == id && a.Balance > 0, cancellationToken);

        if (hasActiveBalance)
        {
            _logger.LogWarning("Cannot delete customer {Id} because active accounts have non-zero balance", id);
            throw new InvalidOperationException("Kan inte radera kund med kvarvarande saldo på sparkonton.");
        }

        var result = await _userManager.DeleteAsync(customer);
        if (!result.Succeeded)
        {
            var errors = string.Join(';', result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to delete user {Id}: {Errors}", id, errors);
            throw new InvalidOperationException($"Unable to delete user: {errors}");
        }

        _logger.LogInformation("Deleted customer {Id}", id);
    }
}
