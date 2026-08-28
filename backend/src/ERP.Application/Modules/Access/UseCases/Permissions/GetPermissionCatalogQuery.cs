using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — catálogo de permisos asignables (grupo → pantalla →
/// acciones), derivado de <see cref="ERP.Domain.Kernel.KernelRegistry"/>. Sin parámetros: el
/// catálogo es el mismo para cualquier perfil, solo cambia qué claves aparecen marcadas al
/// combinarlo con <c>GET .../profiles/{id}/permissions</c> en el frontend.
/// </summary>
public record GetPermissionCatalogQuery : IRequest<Result<PermissionCatalogDto>>;
