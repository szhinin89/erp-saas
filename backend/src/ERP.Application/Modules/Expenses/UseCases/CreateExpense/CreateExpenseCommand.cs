using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;


namespace ERP.Application.Modules.Expenses.UseCases.CreateExpense;

public enum ExpenseCreationMode { Manual = 1, Xml = 2 }

[RequireFeature(SubscriptionFeatureCodes.Expenses)]
public sealed record CreateExpenseCommand(
    ExpenseCreationMode Mode,
    byte[]?           XmlContent,
    string?           XmlFileName,
    Guid?     BusinessPartnerId,
    DateTime? IssueDate,
    string?   Concept,
    string?   Category,
    decimal?  Subtotal,
    decimal?  VatTotal,
    decimal?  Total,
    string?   Notes
) : IRequest<Result<ExpenseInvoiceDto>>, ICompanyScopedRequest;
