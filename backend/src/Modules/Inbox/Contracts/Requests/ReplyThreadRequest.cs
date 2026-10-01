namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Request for replying to an existing thread.
/// </summary>
/// <param name="Body">Message reply text.</param>
public sealed record ReplyThreadRequest(
    string Body);
