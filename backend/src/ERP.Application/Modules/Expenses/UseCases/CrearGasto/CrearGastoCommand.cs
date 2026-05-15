using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;


namespace ERP.Application.Modules.Expenses.UseCases.CrearGasto;

public enum ModoCreacionGasto { Manual = 1, Xml = 2 }

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record CrearGastoCommand(
    ModoCreacionGasto Modo,
    byte[]?           XmlContent,
    string?           XmlNombreArchivo,
    Guid?     SupplierId,
    DateTime? IssueDate,
    string?   Concept,
    string?   Category,
    decimal?  Subtotal,
    decimal?  VatTotal,
    decimal?  Total,
    string?   Notes
) : IRequest<Result<ExpenseInvoiceDto>>;
