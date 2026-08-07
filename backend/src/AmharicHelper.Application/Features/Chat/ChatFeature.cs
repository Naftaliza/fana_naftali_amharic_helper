using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Chat;

public record GetChatHistoryQuery(Guid UserId, Guid DocumentId)
    : IRequest<Result<IReadOnlyList<ChatMessageDto>>>;

public class GetChatHistoryHandler(
    IDocumentRepository documents,
    IChatMessageRepository messages)
    : IRequestHandler<GetChatHistoryQuery, Result<IReadOnlyList<ChatMessageDto>>>
{
    public async Task<Result<IReadOnlyList<ChatMessageDto>>> Handle(GetChatHistoryQuery q, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(q.DocumentId, ct);
        if (doc is null || doc.UserId != q.UserId)
            return Result<IReadOnlyList<ChatMessageDto>>.Fail("Document not found.");

        var history = await messages.ListByDocumentAsync(q.DocumentId, ct);
        return Result<IReadOnlyList<ChatMessageDto>>.Ok(
            history.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt)).ToList());
    }
}

public record SendChatMessageCommand(Guid UserId, Guid DocumentId, string Question, Language ResponseLanguage)
    : IRequest<Result<ChatMessageDto>>;

public class SendChatMessageHandler(
    IDocumentRepository documents,
    IChatMessageRepository messages,
    IAiProvider ai,
    IWalletService wallet) : IRequestHandler<SendChatMessageCommand, Result<ChatMessageDto>>
{
    public async Task<Result<ChatMessageDto>> Handle(SendChatMessageCommand cmd, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null || doc.UserId != cmd.UserId)
            return Result<ChatMessageDto>.Fail("Document not found.");

        // Every chat turn is a fresh AI call (no cache), so charge before making it.
        if (!await wallet.TryConsumeAsync(UsageSubject.ForUser(cmd.UserId), "chat", cmd.DocumentId, ct))
            return Result<ChatMessageDto>.Fail(WalletErrors.OutOfCredits);

        var history = await messages.ListByDocumentAsync(cmd.DocumentId, ct);

        // Persist the user's question.
        await messages.AddAsync(new ChatMessage
        {
            DocumentId = cmd.DocumentId,
            Role = ChatRole.User,
            Content = cmd.Question
        }, ct);

        var answer = await ai.ChatAsync(
            doc.OcrText ?? string.Empty,
            history.Select(m => new ChatTurn(m.Role, m.Content)).ToList(),
            cmd.Question,
            cmd.ResponseLanguage,
            ct);

        var assistantMsg = new ChatMessage
        {
            DocumentId = cmd.DocumentId,
            Role = ChatRole.Assistant,
            Content = answer
        };
        await messages.AddAsync(assistantMsg, ct);

        return Result<ChatMessageDto>.Ok(
            new ChatMessageDto(assistantMsg.Id, assistantMsg.Role, assistantMsg.Content, assistantMsg.CreatedAt));
    }
}
