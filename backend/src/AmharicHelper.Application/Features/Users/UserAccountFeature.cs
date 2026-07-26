using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using MediatR;

namespace AmharicHelper.Application.Features.Users;

// ---- Export account: GDPR Art. 15 "right of access" ----
public record ExportAccountQuery(Guid UserId) : IRequest<Result<AccountExportDto>>;

public class ExportAccountHandler(
    IUserRepository users,
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses,
    IChatMessageRepository messages) : IRequestHandler<ExportAccountQuery, Result<AccountExportDto>>
{
    public async Task<Result<AccountExportDto>> Handle(ExportAccountQuery q, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(q.UserId, ct);
        if (user is null) return Result<AccountExportDto>.Fail("User not found.");

        var docs = await documents.ListByUserAsync(q.UserId, ct);
        var exported = new List<ExportedDocumentDto>(docs.Count);
        foreach (var doc in docs)
        {
            var analysis = await analyses.GetByDocumentIdAsync(doc.Id, ct);
            var chat = await messages.ListByDocumentAsync(doc.Id, ct);
            DocumentAnalysisResult? result = analysis is null ? null : new DocumentAnalysisResult
            {
                Summary = analysis.Summary,
                DocumentType = analysis.DocumentType,
                Category = analysis.Category,
                UrgencyLevel = analysis.UrgencyLevel,
                KeyPoints = analysis.KeyPoints.ToList(),
                RequiredActions = analysis.RequiredActions
                    .Select(a => new RequiredActionDto { Description = a.Description, IsMandatory = a.IsMandatory }).ToList(),
                Deadlines = analysis.Deadlines
                    .Select(d => new DeadlineDto { Date = d.Date, Description = d.Description }).ToList(),
                Explanation = analysis.Explanation
            };

            exported.Add(new ExportedDocumentDto(
                doc.Id, doc.FileName, doc.UploadedAt, doc.OcrText, result,
                chat.Select(m => new ExportedChatMessageDto(m.Role, m.Content, m.CreatedAt)).ToList()));
        }

        return Result<AccountExportDto>.Ok(new AccountExportDto(
            user.Id, user.Email, user.DisplayName, DateTime.UtcNow, exported));
    }
}

// ---- Delete account: GDPR Art. 17 "right to erasure" ----
public record DeleteAccountCommand(Guid UserId) : IRequest<Result<bool>>;

public class DeleteAccountHandler(
    IUserRepository users,
    IDocumentRepository documents,
    IRefreshTokenRepository refreshTokens,
    IFileStorage storage) : IRequestHandler<DeleteAccountCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteAccountCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null) return Result<bool>.Fail("User not found.");

        // DocumentAnalyses/ChatMessages/TtsAudioCache all cascade from Documents at the DB level
        // (ON DELETE CASCADE) — only the physical files and the Document row itself need explicit
        // cleanup here, same as DeleteDocumentHandler for a single document.
        var docs = await documents.ListByUserAsync(cmd.UserId, ct);
        foreach (var doc in docs)
        {
            var paths = new HashSet<string>(doc.PagePaths, StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(doc.FilePath)) paths.Add(doc.FilePath);
            foreach (var path in paths)
                await storage.DeleteAsync(path, ct);
            await documents.DeleteAsync(doc.Id, ct);
        }

        // RefreshTokens has no cascading FK from Users, so it must be cleared before the Users row.
        await refreshTokens.DeleteAllForUserAsync(cmd.UserId, ct);
        await users.DeleteAsync(cmd.UserId, ct);

        return Result<bool>.Ok(true);
    }
}
