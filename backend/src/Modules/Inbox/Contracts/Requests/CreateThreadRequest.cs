namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Request for creating a new support conversation/ticket in the inbox.
/// </summary>
/// <param name="Subject">Subject or title of the inquiry.</param>
/// <param name="Body">Initial message content.</param>
/// <param name="Category">Optional category (Sparkonto, Sparmål, Skatteunderlag, Allmänt).</param>
public sealed record CreateThreadRequest(
    string Subject,
    string Body,
    string? Category = null);
