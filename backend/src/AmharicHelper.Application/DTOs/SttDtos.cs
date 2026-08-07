namespace AmharicHelper.Application.DTOs;

/// <summary>A transcribed voice recording, returned to fill (never auto-send) the chat input.</summary>
public record TranscriptDto(string Text, string? Locale);
