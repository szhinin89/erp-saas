using MediatR;
using ERP.Application.Common;
using ERP.Application.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentValidation;

namespace ERP.Application.Pricing.UseCases.CreatePriceList;

public sealed record CreatePriceListCommand(
    string          Code,
    string          Name,
    string          CurrencyCode,
    DateOnly        ValidFrom,
    bool            PriceIncludesVat = false,
    DateOnly?       ValidTo          = null,
    bool            IsDefault        = false,
    PriceListTarget Target           = PriceListTarget.General
) : IRequest<Result<PriceListDto>>, ICompanyScopedRequest;

public sealed class CreatePriceListCommandValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().Length(3).WithMessage("El código de moneda debe tener 3 caracteres.");
        RuleFor(x => x)
            .Must(x => !x.ValidTo.HasValue || x.ValidTo >= x.ValidFrom)
            .WithMessage("La fecha de fin no puede ser anterior a la de inicio.")
            .WithName("ValidTo");
    }
}

public sealed class CreatePriceListCommandHandler
    : IRequestHandler<CreatePriceListCommand, Result<PriceListDto>>
{
    private readonly IPriceListRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreatePriceListCommandHandler(
        IPriceListRepository repository,
        ICurrentSubscriber subscriber, ICurrentCompany company, ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
    }

    public async Task<Result<PriceListDto>> Handle(CreatePriceListCommand cmd, CancellationToken ct)
    {
        if (await _repository.ExistsByCodeAsync(cmd.Code, ct))
            return Result<PriceListDto>.Conflict($"Ya existe una lista de precios con código '{cmd.Code}'.");

        if (cmd.IsDefault && await _repository.HasDefaultAsync(ct))
            return Result<PriceListDto>.Conflict(
                "Ya existe una lista de precios predeterminada activa. Desactívela primero.");

        var priceList = PriceList.Create(
            _subscriber.SubscriberId, _company.CompanyId,
            cmd.Code, cmd.Name, cmd.CurrencyCode, cmd.ValidFrom,
            _user.UserId, cmd.PriceIncludesVat, cmd.ValidTo, cmd.IsDefault, cmd.Target);

        await _repository.AddAsync(priceList, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<PriceListDto>.Success(ToDto(priceList));
    }

    private static PriceListDto ToDto(PriceList pl) => new(
        pl.Id, pl.Code, pl.Name, pl.CurrencyCode,
        pl.PriceIncludesVat, pl.ValidFrom, pl.ValidTo,
        pl.IsDefault, pl.Target.ToString(), pl.IsActive, pl.CreatedAt);
}
