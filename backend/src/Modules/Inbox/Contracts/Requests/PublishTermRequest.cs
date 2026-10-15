namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Request payload to publish a new terms and conditions version.
/// </summary>
public sealed record PublishTermRequest(
    string Code,
    int Version,
    string Title,
    long DocumentId,
    DateTimeOffset? EffectiveFrom = null
);