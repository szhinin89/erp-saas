using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.InspectSriCertificate;

/// <summary>Prueba una contraseña contra el certificado ya subido sin persistirla — feedback en vivo mientras el usuario escribe.</summary>
public sealed record InspectSriCertificateQuery(string Password) : IRequest<Result<SriCertificateInfoDto>>;
