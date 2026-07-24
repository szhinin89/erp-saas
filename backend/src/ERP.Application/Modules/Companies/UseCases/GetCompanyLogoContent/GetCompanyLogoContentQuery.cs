using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyLogoContent;

public sealed record CompanyLogoContent(Stream Content, string ContentType, string FileName);

public sealed record GetCompanyLogoContentQuery : IRequest<Result<CompanyLogoContent>>;
