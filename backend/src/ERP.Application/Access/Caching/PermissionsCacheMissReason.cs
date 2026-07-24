namespace ERP.Application.Access.Caching;

public enum PermissionsCacheMissReason
{
    NotFound,
    Disabled,
    Exception,
    VersionMismatch,
    StampedeFallback,
}
