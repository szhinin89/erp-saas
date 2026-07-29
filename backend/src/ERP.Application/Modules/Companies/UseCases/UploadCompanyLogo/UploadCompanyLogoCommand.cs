using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UploadCompanyLogo;

public sealed record UploadCompanyLogoCommand(MediaUploadContent File)
    : IRequest<Result<CompanyProfileDto>>;
