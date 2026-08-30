namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cuántos pasos de autorización requiere un documento de este tipo antes de confirmarse.</summary>
public enum AuthorizationMode
{
    None = 0,
    SingleStep = 1,
    MultiStep = 2,
}
