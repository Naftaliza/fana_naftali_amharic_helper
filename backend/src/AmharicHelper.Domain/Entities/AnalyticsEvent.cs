namespace AmharicHelper.Domain.Entities;

/// <summary>
/// A minimal funnel event (register / verify / upload / analyze — see EventNames). The app had
/// zero product analytics before this: no way to see signups, verification completion, activation
/// (first upload), or whether anyone returns for a second document. Deliberately just a name +
/// optional user + timestamp, not a general-purpose event schema — see the plan.
/// </summary>
public class AnalyticsEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
