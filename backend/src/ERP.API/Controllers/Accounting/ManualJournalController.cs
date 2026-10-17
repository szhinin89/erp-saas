using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Accounting;

[ApiController]
[Route("api/v1/accounting/manual-journal-entries")]
[Authorize]
public sealed class ManualJournalController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualCreate)]
    public async Task<IActionResult> Create(CreateManualJournalCommand command, CancellationToken ct) =>
        this.ToCreatedOrBadRequest(await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualEdit)]
    public async Task<IActionResult> Edit(Guid id, EditManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualPost)]
    public async Task<IActionResult> Post(Guid id, PostManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));

    [HttpPost("{id:guid}/discard")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualDiscard)]
    public async Task<IActionResult> Discard(Guid id, DiscardManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualReverse)]
    public async Task<IActionResult> Reverse(Guid id, ReverseManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));
}
