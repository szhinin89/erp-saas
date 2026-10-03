namespace ERP.Domain.Modules.Communications.Enums;

/// <summary>
/// Resultado de un intento de entrega (<c>CommunicationDeliveryAttempt</c>). Null = en curso.
/// <list type="bullet">
/// <item><see cref="Sent"/> / <see cref="Failed"/>: el worker dueño del claim finalizó.</item>
/// <item><see cref="Abandoned"/>: el lease venció sin finalizar (worker muerto); lo cierra el
/// worker que recupera la fila. Resultado del transporte desconocido.</item>
/// <item><see cref="ClaimLost"/>: el worker terminó después de perder el claim (fencing); su
/// resultado no modificó la comunicación.</item>
/// </list>
/// </summary>
public enum CommunicationAttemptResult
{
    Sent = 1,
    Failed = 2,
    Abandoned = 3,
    ClaimLost = 4,
}
