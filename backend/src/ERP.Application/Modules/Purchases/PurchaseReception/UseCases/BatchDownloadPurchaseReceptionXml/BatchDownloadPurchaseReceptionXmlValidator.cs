using FluentValidation;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;

public sealed class BatchDownloadPurchaseReceptionXmlValidator
    : AbstractValidator<BatchDownloadPurchaseReceptionXmlCommand>
{
    public const int MaxBatchSize = 50;

    public BatchDownloadPurchaseReceptionXmlValidator()
    {
        RuleFor(x => x.DocumentIds)
            .NotEmpty()
            .WithMessage("Debe indicar al menos un documento.");

        RuleFor(x => x.DocumentIds)
            .Must(ids => ids.Count <= MaxBatchSize)
            .WithMessage($"No se pueden procesar más de {MaxBatchSize} documentos por lote.")
            .When(x => x.DocumentIds is not null);

        RuleFor(x => x.OnlyMissingXml)
            .Equal(true)
            .WithMessage("La descarga en lote solo puede solicitarse para documentos sin XML.");
    }
}
