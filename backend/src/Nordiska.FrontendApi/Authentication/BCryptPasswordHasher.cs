using System;
using Microsoft.AspNetCore.Identity;

namespace Nordiska.FrontendApi.Authentication;

/// <summary>
/// BCrypt-based implementation of <see cref="IPasswordHasher{TUser}"/> using BCrypt.Net-Next.
/// Conforms to DoD requirement for BCrypt passwords with backward-compatible verification for legacy Identity hashes.
/// </summary>
/// <typeparam name="TUser">The user type being hashed.</typeparam>
public class BCryptPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    private const int DefaultWorkFactor = 11;
    private readonly PasswordHasher<TUser> _legacyIdentityHasher = new();

    /// <summary>
    /// Hashes the provided password using BCrypt with Enhanced hashing (pre-hashed with SHA-384 to support any password length).
    /// </summary>
    public string HashPassword(TUser user, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, DefaultWorkFactor);
    }

    /// <summary>
    /// Verifies the provided password against the hashed password.
    /// Supports BCrypt hashes ($2a$, $2b$, $2y$) and falls back to legacy ASP.NET Core Identity PBKDF2 hashes.
    /// </summary>
    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        // Standard BCrypt hashes start with $2a$, $2b$, or $2y$
        if (hashedPassword.StartsWith("$2a$") || hashedPassword.StartsWith("$2b$") || hashedPassword.StartsWith("$2y$"))
        {
            try
            {
                bool isValid = BCrypt.Net.BCrypt.EnhancedVerify(providedPassword, hashedPassword);
                return isValid ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
            }
            catch
            {
                return PasswordVerificationResult.Failed;
            }
        }

        // Backward compatibility fallback: verify against ASP.NET Core Identity legacy PBKDF2 format
        try
        {
            var fallbackResult = _legacyIdentityHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
            if (fallbackResult != PasswordVerificationResult.Failed)
            {
                // Informs callers that authentication succeeded but the stored hash should be upgraded to BCrypt
                return PasswordVerificationResult.SuccessRehashNeeded;
            }
        }
        catch
        {
            // Ignore format exceptions on invalid hash strings
        }

        return PasswordVerificationResult.Failed;
    }
}