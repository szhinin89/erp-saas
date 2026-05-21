using FluentValidation;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed class CrearPettyCashCommandValidator : AbstractValidator<CrearPettyCashCommand>
{
    public CrearPettyCashCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.AssignedBalance).GreaterThanOrEqualTo(0);
    }
}

public sealed class CrearGastoPettyCashCommandValidator : AbstractValidator<CrearGastoPettyCashCommand>
{
    public CrearGastoPettyCashCommandValidator()
    {
        RuleFor(x => x.PettyCashId).NotEmpty();
        RuleFor(x => x.Concept).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.VoucherType).NotEmpty().MaximumLength(30);
    }
}

public sealed class CrearCashCountCommandValidator : AbstractValidator<CrearCashCountCommand>
{
    public CrearCashCountCommandValidator()
    {
        RuleFor(x => x.PettyCashId).NotEmpty();
        RuleFor(x => x.PhysicalCash).GreaterThanOrEqualTo(0);
    }
}

public sealed class AprobarCashCountCommandValidator : AbstractValidator<AprobarCashCountCommand>
{
    public AprobarCashCountCommandValidator()
    {
        RuleFor(x => x.ArqueoId).NotEmpty();
    }
}

public sealed class ReposicionPettyCashCommandValidator : AbstractValidator<ReposicionPettyCashCommand>
{
    public ReposicionPettyCashCommandValidator()
    {
        RuleFor(x => x.PettyCashId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class CrearBankAccountCommandValidator : AbstractValidator<CrearBankAccountCommand>
{
    public CrearBankAccountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AccountType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
        RuleFor(x => x.InitialBalance).GreaterThanOrEqualTo(0);
    }
}

public sealed class ConciliarBankTransactionCommandValidator : AbstractValidator<ConciliarBankTransactionCommand>
{
    public ConciliarBankTransactionCommandValidator()
    {
        RuleFor(x => x.MovimientoId).NotEmpty();
        RuleFor(x => x.JournalEntryId).NotEmpty();
    }
}

public sealed class ImportarBankStatementCommandValidator : AbstractValidator<ImportarBankStatementCommand>
{
    public ImportarBankStatementCommandValidator()
    {
        RuleFor(x => x.BankAccountId).NotEmpty();
        RuleFor(x => x.PeriodTo).GreaterThanOrEqualTo(x => x.PeriodFrom)
            .WithMessage("PeriodTo debe ser posterior o igual a PeriodFrom.");
        RuleFor(x => x.Rows).NotNull();
    }
}
