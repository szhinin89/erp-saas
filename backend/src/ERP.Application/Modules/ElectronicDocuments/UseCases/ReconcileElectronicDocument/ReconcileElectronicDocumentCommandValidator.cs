using ERP.Domain.Modules.ElectronicDocuments.Entities;
using FluentValidation;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.ReconcileElectronicDocument;

public sealed class ReconcileElectronicDocumentCommandValidator
    : AbstractValidator<ReconcileElectronicDocumentCommand>
{
    public ReconcileElectronicDocumentCommandValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("La referencia de la evidencia es obligatoria.")
            .MaximumLength(ElectronicDocument.ReconciliationReferenceMaxLen);

        RuleFor(x => x.Observation)
            .NotEmpty()
            .WithMessage("La observación es obligatoria.")
            .MaximumLength(ElectronicDocument.ReconciliationObservationMaxLen);
    }
}
