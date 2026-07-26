namespace AmharicHelper.Application.Common;

/// <summary>Canonical funnel event names tracked via IEventTracker — shared constants so call
/// sites and the admin funnel query always agree on spelling.</summary>
public static class EventNames
{
    public const string UserRegistered = "user_registered";
    public const string EmailVerified = "email_verified";
    public const string DocumentUploaded = "document_uploaded";
    public const string DocumentAnalyzed = "document_analyzed";

    /// <summary>All known event names — used to validate an event name coming from a query string
    /// before it reaches a SQL parameter (e.g. the funnel drill-down endpoint).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        UserRegistered, EmailVerified, DocumentUploaded, DocumentAnalyzed,
    };
}
