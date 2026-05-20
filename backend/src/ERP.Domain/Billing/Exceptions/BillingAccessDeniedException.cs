namespace ERP.Domain.Billing.Exceptions;

public sealed class BillingAccessDeniedException : Exception
{
    public BillingAccessDeniedException(string message) : base(message) { }

    public static BillingAccessDeniedException Suspended()
        => new("El acceso a la plataforma está suspendido por estado de facturación.");

    public static BillingAccessDeniedException PastDue()
        => new("El acceso está restringido: facturación vencida. Actualice su método de pago.");
}
