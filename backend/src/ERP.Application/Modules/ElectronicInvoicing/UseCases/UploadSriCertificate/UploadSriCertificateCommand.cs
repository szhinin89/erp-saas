using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UploadSriCertificate;

public sealed record UploadSriCertificateCommand(MediaUploadContent File) : IRequest<Result<SriCertificateUploadResultDto>>;
