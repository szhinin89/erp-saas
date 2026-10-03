namespace ERP.Domain.Modules.Communications.Enums;

/// <summary>
/// Rol estable del destinatario dentro de la identidad lógica de una comunicación (ADR-039 D8): el
/// email es un dato mutable y nunca forma parte de la identidad. Un registro = un canal × un rol.
/// </summary>
public enum CommunicationRecipientRole
{
    Customer = 1,
    Supplier = 2,
    User = 3,
    CompanyCopy = 4,
}
