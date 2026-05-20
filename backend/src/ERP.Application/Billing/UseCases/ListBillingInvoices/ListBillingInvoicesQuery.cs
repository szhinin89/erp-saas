using ERP.Application.Billing.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Billing.UseCases.ListBillingInvoices;

public sealed record ListBillingInvoicesQuery(int Take = 50) : IRequest<Result<IReadOnlyList<SaasBillingInvoiceDto>>>;
