using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;

/// <summary>
/// ADR-033, Fase 3d — crea o actualiza el default de condición de pago de un PROVEEDOR en la
/// empresa activa. Company-scoped: requiere company_id en el JWT.
/// Semántica Upsert: si ya existe → Update, si no → Create.
/// PaymentTermId es opcional: null limpia el default ("sin default configurado" — Compras/Gastos
/// exigirán selección explícita, ver IPaymentTermDefaultResolver, Fase 3b).
/// </summary>
public sealed record UpsertCompanyBpPurchaseSettingsCommand(Guid BusinessPartnerId, Guid? PaymentTermId)
    : IRequest<Result<CompanyBpPurchaseSettingsDto>>, ICompanyScopedRequest;
