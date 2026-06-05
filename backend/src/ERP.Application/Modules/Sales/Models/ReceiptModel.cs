using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;

namespace ERP.Application.Sales.Models;

public sealed class ReceiptModel
{
    public ReceiptDocument Invoice { get; set; } = null!;
    public BusinessPartner? Buyer { get; set; }
    public SubscriberBillingProfile Configuration { get; set; } = null!;
    public bool IsTestMode { get; set; }
}
