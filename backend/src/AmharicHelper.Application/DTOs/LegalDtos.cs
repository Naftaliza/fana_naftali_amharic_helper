namespace AmharicHelper.Application.DTOs;

/// <summary>The current version of one legal document, in the caller's requested language.</summary>
public record LegalDocumentDto(string Kind, int Version, string Body, DateTime EffectiveAt);
