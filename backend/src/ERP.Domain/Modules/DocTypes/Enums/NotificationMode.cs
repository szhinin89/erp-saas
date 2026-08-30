namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cuándo se dispara una notificación por un documento de este tipo.</summary>
public enum NotificationMode
{
    None = 0,
    OnPendingAuthorization = 1,
    OnConfirmation = 2,
    OnCancellation = 3,
}
