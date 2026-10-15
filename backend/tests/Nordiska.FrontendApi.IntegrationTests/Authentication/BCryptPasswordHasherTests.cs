using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Nordiska.FrontendApi.Authentication;
using Nordiska.Modules.Banking.Domain;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher<StaffMember> _sut = new();
    private readonly StaffMember _testStaff = new()
    {
        Id = 1,
        Email = "test@nordiska.se",
        FullName = "Test Staff"
    };

    [Fact]
    public void HashPassword_ProducesValidBCryptHash()
    {
        // Act
        var hash = _sut.HashPassword(_testStaff, "SecretPassword123!");

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2"); // BCrypt signature ($2a$ or $2b$)
        hash.Length.Should().Be(60);
    }

    [Fact]
    public void VerifyHashedPassword_WithCorrectPassword_ReturnsSuccess()
    {
        // Arrange
        const string password = "SecretPassword123!";
        var hash = _sut.HashPassword(_testStaff, password);

        // Act
        var result = _sut.VerifyHashedPassword(_testStaff, hash, password);

        // Assert
        result.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public void VerifyHashedPassword_WithIncorrectPassword_ReturnsFailed()
    {
        // Arrange
        var hash = _sut.HashPassword(_testStaff, "CorrectPassword123!");

        // Act
        var result = _sut.VerifyHashedPassword(_testStaff, hash, "WrongPassword");

        // Assert
        result.Should().Be(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_WithLegacyIdentityHash_ReturnsSuccessRehashNeeded()
    {
        // Arrange
        const string password = "LegacyPassword123!";
        var legacyHasher = new PasswordHasher<StaffMember>();
        var legacyHash = legacyHasher.HashPassword(_testStaff, password);

        // Verify that legacy hash does NOT start with $2
        legacyHash.Should().NotStartWith("$2");

        // Act
        var result = _sut.VerifyHashedPassword(_testStaff, legacyHash, password);

        // Assert
        result.Should().Be(PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact]
    public void VerifyHashedPassword_WithLegacyIdentityHashAndWrongPassword_ReturnsFailed()
    {
        // Arrange
        var legacyHasher = new PasswordHasher<StaffMember>();
        var legacyHash = legacyHasher.HashPassword(_testStaff, "CorrectPassword!");

        // Act
        var result = _sut.VerifyHashedPassword(_testStaff, legacyHash, "WrongPassword");

        // Assert
        result.Should().Be(PasswordVerificationResult.Failed);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("hash", "")]
    [InlineData(null, "password")]
    [InlineData("hash", null)]
    public void VerifyHashedPassword_WithInvalidInputs_ReturnsFailed(string? hash, string? password)
    {
        // Act
        var result = _sut.VerifyHashedPassword(_testStaff, hash!, password!);

        // Assert
        result.Should().Be(PasswordVerificationResult.Failed);
    }
}
