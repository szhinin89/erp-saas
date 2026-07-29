using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;

/// <summary>
/// Admin IAM — un administrador asigna manualmente una contraseña temporal a un usuario de su
/// empresa activa (sin infraestructura de invitación por email todavía). Nunca acepta
/// UserId/TenantId/CompanyId del cliente: el usuario objetivo se resuelve por Username (mismo
/// principio que <see cref="ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin.UpsertCompanyUserMembershipAdminCommand"/>)
/// y el scoping de empresa se valida explícitamente en el handler contra
/// <see cref="ERP.Domain.Access.Interfaces.IAccessRepository.GetCompanyUserMembershipAsync"/>
/// (IdentityUser es una entidad global, no tiene filtro automático de ICompanyOperationalEntity).
///
/// <see cref="TemporaryPassword"/> es obligatorio hoy (el admin la escribe a mano). Cuando se
/// implemente el envío por correo, este campo pasará a nullable — null significará "generar
/// automáticamente" — sin cambiar el resto de la firma ni del handler.
/// </summary>
public sealed record AssignTemporaryPasswordAdminCommand(string Username, string TemporaryPassword)
    : IRequest<Result<string>>,
        IRequiresCompanyContext;
