using FluentValidation;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;

public sealed class DownloadPurchaseReceptionXmlValidator
    : AbstractValidator<DownloadPurchaseReceptionXmlCommand>
{
    public DownloadPurchaseReceptionXmlValidator()
    {
        RuleFor(x => x.PurchaseReceptionDocumentId)
            .NotEmpty()
            .WithMessage("El documento de recepción es obligatorio.");
    }
}
