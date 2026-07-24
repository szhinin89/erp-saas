using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UploadCompanyLogoAlt;

public sealed record UploadCompanyLogoAltCommand(MediaUploadContent File) : IRequest<Result<CompanyProfileDto>>;
