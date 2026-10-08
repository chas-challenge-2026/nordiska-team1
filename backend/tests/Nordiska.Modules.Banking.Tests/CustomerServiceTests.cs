using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Xunit;

namespace Nordiska.Modules.Banking.Tests;

public class CustomerServiceTests
{
    private static Mock<UserManager<Customer>> CreateMockUserManager(Customer? customer = null)
    {
        var store = new Mock<IUserStore<Customer>>();
        var userManagerMock = new Mock<UserManager<Customer>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        if (customer != null)
        {
            userManagerMock
                .Setup(m => m.FindByIdAsync(customer.Id.ToString()))
                .ReturnsAsync(customer);

            userManagerMock
                .Setup(m => m.UpdateAsync(It.IsAny<Customer>()))
                .ReturnsAsync(IdentityResult.Success);
        }

        return userManagerMock;
    }

    private static BankingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .Options;

        return new BankingDbContext(options);
    }

    [Fact]
    public async Task UpdateAsync_WithOverviewPreference_UpdatesCustomerOverviewPreference()
    {
        // Arrange
        var customer = new Customer
        {
            Id = 1,
            Name = "Anna",
            Email = "anna@exempel.se",
            PersonalNum = "198202116050",
            OverviewPreference = new List<string>()
        };

        var userManagerMock = CreateMockUserManager(customer);
        using var db = CreateDbContext();
        var service = new CustomerService(db, new TestLogger<CustomerService>(), userManagerMock.Object);

        var newPreferences = new List<string> { "savings", "accounts", "transactions" };

        // Act
        var result = await service.UpdateAsync(
            1,
            name: "Anna Updated",
            email: "anna.updated@exempel.se",
            personalNum: "198202116050",
            phoneNumber: "+46701234567",
            overviewPreference: newPreferences);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Anna Updated", result.Name);
        Assert.Equal("anna.updated@exempel.se", result.Email);
        Assert.Equal("+46701234567", result.PhoneNumber);
        Assert.Equal(newPreferences, result.OverviewPreference);
        userManagerMock.Verify(m => m.UpdateAsync(It.Is<Customer>(c => c.OverviewPreference == newPreferences)), Times.Once);
    }

    [Fact]
    public async Task PatchProfileAsync_WithOverviewPreference_UpdatesCustomerOverviewPreference()
    {
        // Arrange
        var customer = new Customer
        {
            Id = 2,
            Name = "Erik",
            Email = "erik@exempel.se",
            PersonalNum = "197903142380",
            OverviewPreference = new List<string> { "accounts" }
        };

        var userManagerMock = CreateMockUserManager(customer);
        using var db = CreateDbContext();
        var service = new CustomerService(db, new TestLogger<CustomerService>(), userManagerMock.Object);

        var newPreferences = new List<string> { "inbox", "savings", "accounts" };

        // Act
        var result = await service.PatchProfileAsync(
            2,
            name: null,
            email: null,
            phoneNumber: null,
            overviewPreference: newPreferences);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Erik", result.Name);
        Assert.Equal(newPreferences, result.OverviewPreference);
        userManagerMock.Verify(m => m.UpdateAsync(It.Is<Customer>(c => c.OverviewPreference == newPreferences)), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCustomerNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userManagerMock = CreateMockUserManager(null);
        using var db = CreateDbContext();
        var service = new CustomerService(db, new TestLogger<CustomerService>(), userManagerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }
}
