namespace ERP.Domain.Access.Enums;

public enum UserSessionStatus
{
    Active = 1,
    ClosedManually = 2,
    ClosedByNewLogin = 3,
    Expired = 4,
}
