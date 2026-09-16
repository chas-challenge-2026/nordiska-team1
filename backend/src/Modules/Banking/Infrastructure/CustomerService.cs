using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        var customer = await _userManager.FindByIdAsync(id.ToString());
        if (customer is null)
            throw new KeyNotFoundException($"Customer with id {id} was not found.");

        return customer;
    }

    public async Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        var customer = await GetByIdAsync(id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(name))
            customer.Name = name;

        if (!string.IsNullOrWhiteSpace(email))
        {
            customer.Email = email;
            customer.UserName = email;
        }

        if (!string.IsNullOrWhiteSpace(personalNum))
            customer.PersonalNum = personalNum;

        if (phoneNumber != null)
            customer.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber;

        customer.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(customer);
        if (!result.Succeeded)
        {
            var errors = string.Join(';', result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to update user: {Errors}", errors);
            throw new InvalidOperationException($"Unable to update user: {errors}");
        }

        _logger.LogInformation("Updated customer {Id}", customer.Id);
        return customer;
    }

    public Task<Customer> PatchProfileAsync(long id, string? name, string? email, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, name, email, personalNum: null, phoneNumber: phoneNumber, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var customer = await GetByIdAsync(id, cancellationToken);

        var activeAccounts = await _db.SavingsAccounts
            .Where(a => a.CustomerId == id)
            .ToListAsync(cancellationToken);

        if (activeAccounts.Any(a => a.Balance > 0))
        {
            throw new InvalidOperationException("Cannot delete customer with positive account balance. Withdraw or transfer all funds before deleting account.");
        }

        if (activeAccounts.Count > 0)
        {
            _db.SavingsAccounts.RemoveRange(activeAccounts);
        }

        var result = await _userManager.DeleteAsync(customer);
        if (!result.Succeeded)
        {
            var errors = string.Join(';', result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to delete user: {Errors}", errors);
            throw new InvalidOperationException($"Unable to delete user: {errors}");
        }

        _logger.LogInformation("Deleted customer {Id}", customer.Id);
    }
}
