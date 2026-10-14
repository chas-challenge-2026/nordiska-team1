using System.Security.Cryptography;
using System.Text;

namespace Nordiska.Modules.Faq.Application;

// Shared by the search and view logs so the same visitor gets the same hash in both tables
public static class FaqSessionHash
{
    public static string Compute(string salt, string sessionKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}|{sessionKey}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
