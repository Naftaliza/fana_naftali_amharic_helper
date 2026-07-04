using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Invoicing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>Admin: generate and email a PDF invoice for one provider's billable (Converted)
/// leads in a given calendar month. Persisted as an immutable snapshot — see Invoice entity.</summary>
[Authorize]
[Route("api/admin/providers/{providerId:guid}/invoices")]
public class AdminInvoicesController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(Guid providerId, [FromQuery] int year, [FromQuery] int month)
    {
        var result = await Mediator.Send(new GetInvoiceStatusQuery(CurrentUserId, providerId, year, month));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid providerId, [FromBody] GenerateInvoiceRequest body)
    {
        var result = await Mediator.Send(new GenerateAndSendInvoiceCommand(CurrentUserId, providerId, body.Year, body.Month));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{invoiceId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid providerId, Guid invoiceId)
    {
        var result = await Mediator.Send(new GetInvoicePdfQuery(CurrentUserId, invoiceId));
        if (!result.Success) return StatusCode(403, new { error = result.Error });
        return File(result.Value!, "application/pdf", "invoice.pdf");
    }
}
