namespace Nordiska.FrontendApi.Contracts.Requests;

/// <summary>
/// Request payload for email and password authentication.
/// </summary>
/// <param name="Email">The user's registered email address.</param>
/// <param name="Password">The user's account password.</param>
public record LoginRequest(string Email, string Password);
