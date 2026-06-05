using FluentValidation;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed class CreatePettyCashCommandValidator : AbstractValidator<CreatePettyCashCommand>
{
    public CreatePettyCashCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.AssignedBalance).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateExpensePettyCashCommandValidator : AbstractValidator<CreateExpensePettyCashCommand>
{
    public CreateExpensePettyCashCommandValidator()
    {
        RuleFor(x => x.PettyCashId).NotEmpty();
        RuleFor(x => x.Concept).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.VoucherType).NotEmpty().MaximumLength(30);
    }
}

public sealed class CreateCashCountCommandValidator : AbstractValidator<CreateCashCountCommand>
{
    public CreateCashCountCommandValidator()
    {
        RuleFor(x => x.PettyCashId).NotEmpty();
        RuleFor(x => x.PhysicalCash).GreaterThanOrEqualTo(0);
    }
}

public sealed class ApproveCashCountCommandValidator : AbstractValidator<ApproveCashCountCommand>
{
    public ApproveCashCountCommandValidator()
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

public sealed class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
    public CreateBankAccountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AccountType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
        RuleFor(x => x.InitialBalance).GreaterThanOrEqualTo(0);
    }
}

public sealed class ReconcileBankTransactionCommandValidator : AbstractValidator<ReconcileBankTransactionCommand>
{
    public ReconcileBankTransactionCommandValidator()
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
