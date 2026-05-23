using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateCustomerNotes;

public sealed record UpdateCustomerNotesCommand(
    Guid    BusinessPartnerId,
    string? Notes) : IRequest<Result<bool>>, ISubscriberOnlyRequest;
