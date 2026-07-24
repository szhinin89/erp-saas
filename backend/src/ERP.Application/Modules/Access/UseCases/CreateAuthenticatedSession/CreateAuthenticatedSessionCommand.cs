using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateAuthenticatedSession;

/// <summary>
/// Recibe primitivos de un login ya resuelto (LoginHandler/SwitchCompanyHandler), sin
/// ICurrentUser/ICurrentTenant/ICurrentCompany ambientes porque se ejecuta antes de que ese
/// contexto exista — nunca se invoca directamente desde un controller público, solo vía
/// mediator desde handlers de Auth ya autenticados/autorizados. Crea la UserSession y su
/// RefreshToken asociado (vía RefreshTokenId) en una única unidad transaccional. No genera JWT
/// ni gestiona cookies — eso pertenece a Fase 7/8.
/// </summary>
public sealed record CreateAuthenticatedSessionCommand(
    Guid TenantId,
    Guid CompanyId,
    Guid IdentityUserId,
    Guid BranchId,
    string TerminalId
) : IRequest<Result<AuthenticatedSessionDto>>;
