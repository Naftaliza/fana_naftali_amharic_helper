using System.Reflection;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestDocument = QuestPDF.Fluent.Document;

namespace AmharicHelper.Infrastructure.Invoicing;

/// <summary>Renders a single-page, Fana-branded invoice PDF from a persisted Invoice snapshot.
/// This is a billing statement, not a payment-collection/legal tax document — no payment is
/// collected through Fana, so it deliberately omits a tax/business ID and bank details (see the
/// Invoicing plan). Invoices are a back-office/provider-facing document, not part of the
/// localized user-facing product, so labels are plain English — no i18n needed here.</summary>
public class QuestPdfInvoiceBuilder(IConfiguration config) : IInvoicePdfBuilder
{
    // Falls back to the first Admin:Emails entry (the same allowlist already used for admin
    // access) rather than a fabricated domain, since no dedicated support-email config exists.
    private string SupportEmail =>
        config["Company:SupportEmail"] is { Length: > 0 } configured
            ? configured
            : config["Admin:Emails"]?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
              ?? "the Fana team";

    // Matches frontend/tailwind.config.ts's brand/accent colors exactly, so the invoice reads as
    // the same product as the app rather than a generic back-office document.
    private const string BrandPrimary = "#2563EB";
    private const string BrandAccent = "#0EA5A4";
    private const string InkMuted = "#6B7280";
    private const string RowAlt = "#F8FAFC";

    private static readonly byte[] Logo = LoadLogo();

    private static byte[] LoadLogo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
            "AmharicHelper.Infrastructure.Invoicing.Assets.fana-logo.png")!;
        using var mem = new MemoryStream();
        stream.CopyTo(mem);
        return mem.ToArray();
    }

    private static readonly Dictionary<DocumentCategory, string> CategoryLabels = new()
    {
        [DocumentCategory.Government] = "Government",
        [DocumentCategory.Bank] = "Bank",
        [DocumentCategory.Insurance] = "Insurance",
        [DocumentCategory.Employment] = "Employment",
        [DocumentCategory.Healthcare] = "Healthcare",
        [DocumentCategory.Municipality] = "Municipality",
        [DocumentCategory.Other] = "Other",
    };

    public byte[] Build(Invoice invoice, Provider provider)
    {
        return QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1F2937"));

                page.Header().Column(col =>
                {
                    // Logo + wordmark on the start side, invoice identity on the end side.
                    col.Item().Row(row =>
                    {
                        row.AutoItem().Width(40).Height(40).Image(Logo);
                        row.AutoItem().PaddingLeft(10).Column(brand =>
                        {
                            brand.Item().Text("Fana").FontSize(20).Bold().FontColor(BrandPrimary);
                            brand.Item().Text("Understand any document, in your language").FontSize(8).FontColor(InkMuted);
                        });
                        row.RelativeItem();
                        row.AutoItem().Column(meta =>
                        {
                            meta.Item().AlignRight().Text("INVOICE").FontSize(16).Bold().FontColor(BrandPrimary);
                            meta.Item().AlignRight().Text(invoice.InvoiceNumber).FontSize(9);
                            meta.Item().AlignRight().Text($"Period: {new DateTime(invoice.PeriodYear, invoice.PeriodMonth, 1):MMMM yyyy}").FontSize(9);
                            meta.Item().AlignRight().Text($"Issued: {invoice.GeneratedAt:yyyy-MM-dd}").FontSize(9).FontColor(InkMuted);
                        });
                    });

                    // Brand-colored divider, echoing the app's blue-to-teal gradient.
                    col.Item().PaddingTop(12).Height(3).Background(BrandPrimary);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Item().Text("Bill to").FontSize(9).Bold().FontColor(InkMuted);
                    col.Item().PaddingTop(2).Text(provider.DisplayName).FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(provider.City)) col.Item().Text(provider.City!).FontSize(9).FontColor(InkMuted);
                    if (!string.IsNullOrWhiteSpace(provider.ContactEmail)) col.Item().Text(provider.ContactEmail!).FontSize(9).FontColor(InkMuted);

                    col.Item().PaddingTop(20).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); // date
                            c.RelativeColumn(2); // ref
                            c.RelativeColumn(3); // category
                            c.RelativeColumn(2); // price
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(BrandPrimary).Padding(6).Text("Date").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(BrandPrimary).Padding(6).Text("Ref").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(BrandPrimary).Padding(6).Text("Document category").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(BrandPrimary).Padding(6).AlignRight().Text("Price").FontColor(Colors.White).Bold().FontSize(9);
                        });

                        for (var i = 0; i < invoice.LineItems.Count; i++)
                        {
                            var item = invoice.LineItems[i];
                            QuestPDF.Infrastructure.Color bg = i % 2 == 1 ? RowAlt : Colors.White;
                            table.Cell().Background(bg).Padding(6).Text(item.LeadCreatedAt.ToString("yyyy-MM-dd")).FontSize(9);
                            table.Cell().Background(bg).Padding(6).Text(item.Ref ?? "-").FontSize(9);
                            table.Cell().Background(bg).Padding(6).Text(CategoryLabels.GetValueOrDefault(item.Category, "Other")).FontSize(9);
                            table.Cell().Background(bg).Padding(6).AlignRight().Text($"{item.Price:0.00} {invoice.Currency}").FontSize(9);
                        }
                    });

                    col.Item().PaddingTop(16).AlignRight().Width(220).Column(totals =>
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Converted leads").FontSize(9).FontColor(InkMuted);
                            r.AutoItem().Text($"{invoice.LeadCount}").FontSize(9);
                        });
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Price per lead").FontSize(9).FontColor(InkMuted);
                            r.AutoItem().Text($"{invoice.PricePerLead:0.00} {invoice.Currency}").FontSize(9);
                        });
                        totals.Item().PaddingTop(6).BorderTop(1).BorderColor(InkMuted).PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("Total due").FontSize(12).Bold();
                            r.AutoItem().Text($"{invoice.TotalAmount:0.00} {invoice.Currency}").FontSize(14).Bold().FontColor(BrandPrimary);
                        });
                    });

                    col.Item().PaddingTop(28).Background(RowAlt).Padding(12).Column(note =>
                    {
                        note.Item().Text("This is a billing statement for referral leads delivered through Fana — payment is").FontSize(8).FontColor(InkMuted);
                        note.Item().Text("handled directly between you and Fana, not through this document.").FontSize(8).FontColor(InkMuted);
                    });
                });

                page.Footer().PaddingTop(10).Column(footer =>
                {
                    footer.Item().Height(1).Background("#E5E7EB");
                    footer.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text($"Questions about this invoice? Contact us — {SupportEmail}").FontSize(8).FontColor(InkMuted);
                        row.AutoItem().Text(text =>
                        {
                            text.Span("Fana").FontColor(BrandPrimary).Bold().FontSize(8);
                            text.Span(" — thank you for partnering with us.").FontSize(8).FontColor(InkMuted);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
