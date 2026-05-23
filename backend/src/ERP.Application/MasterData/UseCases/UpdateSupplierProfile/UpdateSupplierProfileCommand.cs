using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateSupplierProfile;

public sealed record UpdateSupplierProfileCommand(
    Guid    BusinessPartnerId,
    string? DefaultTaxSupportCode,
    string? DefaultRetentionVatCode,
    string? DefaultRetentionIncomeCode,
    string? PaymentTerms) : IRequest<Result<bool>>, ISubscriberOnlyRequest;
