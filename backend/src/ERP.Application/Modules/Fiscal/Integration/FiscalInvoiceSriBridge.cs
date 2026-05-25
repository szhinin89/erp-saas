using System.Reflection;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Fiscal.Integration;

/// <summary>
/// Puente temporal hacia servicios SRI acoplados a <see cref="SalesBill"/>.
/// Eliminar cuando SRI consuma <see cref="Invoice"/> directamente.
/// </summary>
public static class FiscalInvoiceSriBridge
{
    public static SalesBill ToLegacyBill(Invoice invoice)
    {
        var bill = SalesBill.Create(
            subscriberId: invoice.SubscriberId,
            branchId: invoice.BranchId,
            businessPartnerId: invoice.BusinessPartnerId,
            warehouseId: invoice.WarehouseId,
            docType: invoice.DocType,
            estabCode: invoice.EstabCode,
            emPointCode: invoice.EmPointCode,
            sequential: invoice.Sequential,
            accessKey: invoice.AccessKey,
            issueDate: invoice.IssueDate,
            subtotal: invoice.Subtotal,
            vatTotal: invoice.TaxTotal,
            total: invoice.Total,
            totalDiscount: invoice.TotalDiscount,
            paymentMethodCode: invoice.PaymentMethodCode,
            paymentDays: invoice.PaymentTermDays,
            notes: invoice.Notes,
            xmlSignedPath: invoice.Electronic?.XmlSignedPath,
            xmlAuthPath: invoice.Electronic?.XmlAuthorizedPath,
            authNumber: invoice.Electronic?.AuthorizationNumber,
            authDate: invoice.Electronic?.AuthorizationDate,
            errorMessage: invoice.Electronic?.ErrorMessage,
            createdBy: invoice.CreatedBy,
            companyId: invoice.CompanyId);

        OverrideBillId(bill, invoice.PublicId);
        OverrideBillStatus(bill, invoice.Status);
        return bill;
    }

    public static List<SalesBillLine> ToLegacyLines(Invoice invoice, Guid createdBy)
    {
        return invoice.Lines.Select(line =>
        {
            var legacy = SalesBillLine.Create(
                subscriberId: line.SubscriberId,
                productId: line.ProductId,
                productCode: line.SkuSnapshot,
                quantity: line.Quantity,
                unitPrice: line.UnitPriceSnapshot,
                discountAmount: line.DiscountAmount,
                vatCode: line.TaxCodeSnapshot,
                vatPercentage: line.TaxRateSnapshot,
                vatTotal: line.LineTax,
                description: line.DescriptionSnapshot,
                createdBy: createdBy);
            legacy.AssignBillId(invoice.PublicId);
            return legacy;
        }).ToList();
    }

    private static void OverrideBillId(SalesBill bill, Guid publicId)
    {
        typeof(SalesBill).GetProperty(nameof(SalesBill.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(bill, publicId);
    }

    private static void OverrideBillStatus(SalesBill bill, string status)
    {
        typeof(SalesBill).GetProperty(nameof(SalesBill.Status), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(bill, status);
    }
}
