namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Parameters for querying inbox threads.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="PageSize">Page size (e.g. 20).</param>
/// <param name="Folder">Folder filter ('inbox', 'sent', 'archive'). Default is 'inbox'.</param>
/// <param name="SearchTerm">Optional search term to filter threads by subject or message content.</param>
public sealed record InboxQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string Folder = "inbox",
    string? SearchTerm = null);