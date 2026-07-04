using AmharicHelper.Domain.Entities;

namespace AmharicHelper.Application.Abstractions;

/// <summary>Renders an Invoice + its Provider into a PDF byte stream. The MVP implementation is
/// QuestPdfInvoiceBuilder, selected because generation is deterministic from the persisted
/// snapshot — no need to store the PDF bytes themselves, just rebuild on demand.</summary>
public interface IInvoicePdfBuilder
{
    byte[] Build(Invoice invoice, Provider provider);
}
