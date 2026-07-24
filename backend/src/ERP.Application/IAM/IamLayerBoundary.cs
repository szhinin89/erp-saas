namespace ERP.Application.IAM;

/// <summary>
/// Marcador de capa IAM (identidad, sesión, memberships, permisos).
/// </summary>
/// <remarks>
/// Mapeo físico → lógico:
/// <list type="bullet">
///   <item><c>Modules/Access</c> → IAM.Access</item>
///   <item><c>Modules/Auth</c> → IAM.Auth</item>
///   <item><c>Controllers/AccessController</c> → <c>/api/admin/iam/*</c> (legacy alias)</item>
///   <item><c>Controllers/AuthController</c> → <c>/api/auth/*</c></item>
/// </list>
/// Objetivo futuro: <c>/api/iam/*</c> como alias canónico; rutas actuales se mantienen.
/// </remarks>
public static class IamLayerBoundary;
