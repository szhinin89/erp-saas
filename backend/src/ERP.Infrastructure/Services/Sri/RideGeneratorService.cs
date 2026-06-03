using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Converters;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Genera el RIDE (RepresentaciÃ³n Impresa del Documento ElectrÃ³nico) en PDF.
/// ImplementaciÃ³n con QuestPDF â€” sin dependencias HTTP.
/// Reutilizable desde API, jobs, email o CLI.
/// </summary>
public sealed class RideGeneratorService : IRideGeneratorService
{
    // â”€â”€ Paleta de colores â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string ColPrimary   = "#1F3864";
    private const string ColSecondary = "#2E75B6";
    private const string ColLight     = "#EEF3FA";
    private const string ColBorder    = "#B8CCE4";
    private const string ColText      = "#1A1A1A";
    private const string ColMuted     = "#666666";
    private const string ColWhite     = "#FFFFFF";
    private const string ColAlert     = "#C00000";
    private const string ColRowAlt    = "#F5F8FD";

    private readonly ErpDbContext                  _db;
    private readonly ILogger<RideGeneratorService> _logger;

    public RideGeneratorService(ErpDbContext db, ILogger<RideGeneratorService> logger)
    {
        _db     = db;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // â”€â”€ API pÃºblica â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<byte[]> GenerateFacturaPdfAsync(Guid salesBillId, CancellationToken ct = default)
    {
        var factura = await _db.FiscalInvoices
            .AsNoTracking()
            .Include(f => f.Lines)
            .Include(f => f.Electronic)
            .FirstOrDefaultAsync(f => f.PublicId == salesBillId, ct)
            ?? throw new KeyNotFoundException($"Factura {salesBillId} no encontrada.");

        var buyer = await _db.Set<BusinessPartner>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == factura.BusinessPartnerId, ct);

        var cfg      = await LoadBillingProfile(factura.SubscriberId, ct);
        var esPrueba = IsAmbientePrueba(factura.AccessKey);
        var snapshot = ToRideSnapshot(factura);

        _logger.LogDebug("[RIDE] Generando PDF factura {Id} ({Doc})", factura.PublicId, factura.AccessKey);

        return BuildFacturaPdf(snapshot, buyer, cfg, esPrueba);
    }

    public async Task<byte[]> GenerateNotaPdfAsync(Guid salesNoteId, CancellationToken ct = default)
    {
        var nota = await _db.Set<SalesNote>()
            .AsNoTracking()
            .Include(n => n.OriginalBill)
            .Include(n => n.Lines)
            .FirstOrDefaultAsync(n => n.Id == salesNoteId, ct)
            ?? throw new KeyNotFoundException($"Nota {salesNoteId} no encontrada.");

        var buyerPartnerId = nota.OriginalBill?.BusinessPartnerId;
        var buyer = buyerPartnerId.HasValue && buyerPartnerId.Value != Guid.Empty
            ? await _db.Set<BusinessPartner>()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == buyerPartnerId.Value, ct)
            : null;

        var cfg      = await LoadBillingProfile(nota.SubscriberId, ct);
        var esPrueba = IsAmbientePrueba(nota.AccessKey);

        _logger.LogDebug("[RIDE] Generando PDF nota {Id} ({Doc})", nota.Id, nota.AccessKey);

        return BuildNotaPdf(nota, buyer, cfg, esPrueba);
    }

    // â”€â”€ Generadores QuestPDF â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private byte[] BuildFacturaPdf(RideFacturaSnapshot f, BusinessPartner? buyer, SubscriberBillingProfile cfg, bool esPrueba)
    {
        var lines   = f.Lines.ToList();
        var numDoc  = $"{f.EstabCode}-{f.EmPointCode}-{f.Sequential.PadLeft(9, '0')}";
        var docLabel = "FACTURA";

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(8).FontFamily("Arial").FontColor(ColText));

            // â”€â”€ Encabezado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    // Bloque izquierdo â€” logo + empresa
                    row.RelativeItem(2).Column(emp =>
                    {
                        if (!string.IsNullOrWhiteSpace(cfg.LogoBase64))
                        {
                            try
                            {
                                var imgBytes = Convert.FromBase64String(
                                    cfg.LogoBase64.Contains(',')
                                        ? cfg.LogoBase64.Split(',')[1]
                                        : cfg.LogoBase64);
                                emp.Item().MaxHeight(55).Image(imgBytes).FitArea();
                            }
                            catch { /* logo invÃ¡lido â€” omitir */ }
                        }
                        emp.Item().Text(cfg.LegalName).Bold().FontSize(10).FontColor(ColPrimary);
                        emp.Item().Text(cfg.TradeName).FontSize(8).FontColor(ColMuted);
                        emp.Item().Text($"RUC: {cfg.IdentificationNumber}").Bold();
                        emp.Item().Text(cfg.Address).FontSize(7.5f);
                        if (!string.IsNullOrWhiteSpace(cfg.Phone))
                            emp.Item().Text($"Tel: {cfg.Phone}").FontSize(7.5f);
                        if (!string.IsNullOrWhiteSpace(cfg.Email))
                            emp.Item().Text(cfg.Email).FontSize(7.5f);
                    });

                    // Bloque derecho â€” nÃºmero y datos de emisiÃ³n
                    row.RelativeItem().Border(1).BorderColor(ColBorder).Padding(8).Column(doc =>
                    {
                        doc.Item().AlignCenter()
                           .Background(ColPrimary).Padding(4)
                           .Text(docLabel).Bold().FontSize(11).FontColor(ColWhite);

                        doc.Item().PaddingTop(4).AlignCenter()
                           .Text(numDoc).Bold().FontSize(10).FontColor(ColSecondary);

                        if (f.AuthNumber is { Length: > 0 })
                        {
                            doc.Item().PaddingTop(6).Text("AUTORIZACIÃ“N SRI").Bold().FontSize(7).FontColor(ColMuted);
                            doc.Item().Text(f.AuthNumber).FontSize(7).FontColor(ColText);
                            if (f.AuthDate.HasValue)
                                doc.Item().Text($"Fecha auth: {f.AuthDate.Value:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(ColMuted);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.SpecialTaxpayer))
                            doc.Item().PaddingTop(4).Text($"Cont. Especial: {cfg.SpecialTaxpayer}").FontSize(7);

                        doc.Item().Text($"Oblig. Contabilidad: {(cfg.RequiresAccounting ? "SI" : "NO")}").FontSize(7);
                        doc.Item().Text($"Ambiente: {(esPrueba ? "PRUEBAS" : "PRODUCCIÃ“N")}").FontSize(7)
                           .FontColor(esPrueba ? ColAlert : ColText);
                    });
                });

                col.Item().PaddingTop(4).BorderBottom(1.5f).BorderColor(ColSecondary);

                if (esPrueba)
                    col.Item().AlignCenter().Background(ColAlert).Padding(3)
                       .Text("âš  DOCUMENTO DE PRUEBA â€” NO TIENE VALIDEZ TRIBUTARIA âš ")
                       .Bold().FontSize(8).FontColor(ColWhite);
            });

            // â”€â”€ Contenido â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            page.Content().PaddingTop(8).Column(col =>
            {
                // Datos del cliente
                col.Item().Background(ColLight).Border(1).BorderColor(ColBorder).Padding(6).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("CLIENTE / COMPRADOR").Bold().FontSize(7).FontColor(ColSecondary);
                        r.ConstantItem(120).Text($"Fecha: {f.IssueDate:dd/MM/yyyy}").FontSize(7).FontColor(ColMuted).AlignRight();
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("RazÃ³n Social:").Bold().FontSize(7.5f);
                        r.RelativeItem().Text(buyer?.LegalName ?? "CONSUMIDOR FINAL").FontSize(7.5f);
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("RUC / CI:").Bold().FontSize(7.5f);
                        r.RelativeItem().Text(buyer?.Identification.Number ?? "9999999999").FontSize(7.5f);
                    });
                    if (!string.IsNullOrWhiteSpace(buyer?.Email))
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(60).Text("Email:").Bold().FontSize(7.5f);
                            r.RelativeItem().Text(buyer.Email).FontSize(7.5f);
                        });
                });

                col.Item().PaddingTop(8);

                // Tabla de lÃ­neas
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);  // CÃ³digo
                        cols.RelativeColumn(4);   // DescripciÃ³n
                        cols.ConstantColumn(45);  // Cantidad
                        cols.ConstantColumn(55);  // P. Unitario
                        cols.ConstantColumn(45);  // Descuento
                        cols.ConstantColumn(45);  // IVA%
                        cols.ConstantColumn(60);  // Total
                    });

                    table.Header(h =>
                    {
                        foreach (var (label, right) in new (string, bool)[]
                        {
                            ("CÃ³digo",      false),
                            ("DescripciÃ³n", false),
                            ("Cantidad",    true),
                            ("P.Unitario",  true),
                            ("Descuento",   true),
                            ("IVA %",       true),
                            ("Total",       true),
                        })
                        {
                            var cell = h.Cell().Background(ColPrimary).Padding(4);
                            var aligned = right ? cell.AlignRight() : cell.AlignLeft();
                            aligned.Text(label).Bold().FontSize(7.5f).FontColor(ColWhite);
                        }
                    });

                    var row = 0;
                    foreach (var l in lines)
                    {
                        var bg    = row++ % 2 == 0 ? ColWhite : ColRowAlt;
                        var vatPct = l.Subtotal > 0 ? l.VatTotal / l.Subtotal * 100 : 0;

                        CellText(table, bg, l.ProductId.ToString()[..8], false, 7.5f);
                        CellText(table, bg, l.Description,               false, 7.5f);
                        CellText(table, bg, $"{l.Quantity:N4}",          true,  7.5f);
                        CellText(table, bg, $"{l.UnitPrice:N4}",         true,  7.5f);
                        CellText(table, bg, $"{l.DiscountAmount:N2}",    true,  7.5f);
                        CellText(table, bg, $"{vatPct:N0}%",             true,  7.5f);
                        CellText(table, bg, $"{l.Total:N2}",             true,  7.5f);
                    }
                });

                col.Item().PaddingTop(6);

                // Totales
                col.Item().AlignRight().Width(200).Column(tot =>
                {
                    TotalRow(tot, "Subtotal 0%:",  "$",  "0.00");
                    TotalRow(tot, $"Subtotal IVA:", "$",  $"{f.Subtotal:N2}");
                    TotalRow(tot, "IVA:",           "$",  $"{f.VatTotal:N2}");
                    TotalRow(tot, "Propina:",       "$",  "0.00");
                    tot.Item().BorderTop(1.5f).BorderColor(ColSecondary);
                    TotalRow(tot, "TOTAL:",         "$",  $"{f.Total:N2}", bold: true, bg: ColLight);
                });

                col.Item().PaddingTop(10);

                // Clave de acceso
                col.Item().Background(ColLight).Border(1).BorderColor(ColBorder).Padding(6).Column(ck =>
                {
                    ck.Item().Text("CLAVE DE ACCESO (49 dÃ­gitos):").Bold().FontSize(7).FontColor(ColSecondary);
                    ck.Item().PaddingTop(2)
                      .Text(FormatAccessKey(f.AccessKey))
                      .FontSize(7.5f).FontFamily("Courier New").FontColor(ColText);
                });

                // Leyenda / footer
                if (!string.IsNullOrWhiteSpace(cfg.FooterText))
                {
                    col.Item().PaddingTop(6)
                       .Text(cfg.FooterText).FontSize(7).FontColor(ColMuted).Italic();
                }
            });

            // â”€â”€ Pie de pÃ¡gina â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            page.Footer().BorderTop(0.5f).BorderColor(ColBorder).PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text("Documento generado electrÃ³nicamente. Autorizado por el SRI Ecuador.")
                 .FontSize(6.5f).FontColor(ColMuted);
                r.ConstantItem(60).AlignRight()
                 .Text(x =>
                 {
                     x.Span("PÃ¡g. ").FontSize(6.5f).FontColor(ColMuted);
                     x.CurrentPageNumber().FontSize(6.5f).FontColor(ColMuted);
                     x.Span(" / ").FontSize(6.5f).FontColor(ColMuted);
                     x.TotalPages().FontSize(6.5f).FontColor(ColMuted);
                 });
            });
        })).GeneratePdf();
    }

    private byte[] BuildNotaPdf(SalesNote n, BusinessPartner? buyer, SubscriberBillingProfile cfg, bool esPrueba)
    {
        var esCredito = n.NoteType == NoteType.Credit;
        var docLabel  = esCredito ? "NOTA DE CRÃ‰DITO" : "NOTA DE DÃ‰BITO";
        var numDoc    = $"{n.EstabCode}-{n.EmPointCode}-{n.Sequential.PadLeft(9, '0')}";
        var origNum   = $"{n.OriginalBill.EstabCode}-{n.OriginalBill.EmPointCode}-{n.OriginalBill.Sequential.PadLeft(9, '0')}";
        var lines     = n.Lines.ToList();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(8).FontFamily("Arial").FontColor(ColText));

            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem(2).Column(emp =>
                    {
                        if (!string.IsNullOrWhiteSpace(cfg.LogoBase64))
                        {
                            try
                            {
                                var imgBytes = Convert.FromBase64String(
                                    cfg.LogoBase64.Contains(',')
                                        ? cfg.LogoBase64.Split(',')[1]
                                        : cfg.LogoBase64);
                                emp.Item().MaxHeight(55).Image(imgBytes).FitArea();
                            }
                            catch { }
                        }
                        emp.Item().Text(cfg.LegalName).Bold().FontSize(10).FontColor(ColPrimary);
                        emp.Item().Text($"RUC: {cfg.IdentificationNumber}").Bold();
                        emp.Item().Text(cfg.Address).FontSize(7.5f);
                    });

                    row.RelativeItem().Border(1).BorderColor(ColBorder).Padding(8).Column(doc =>
                    {
                        doc.Item().AlignCenter().Background(ColSecondary).Padding(4)
                           .Text(docLabel).Bold().FontSize(11).FontColor(ColWhite);
                        doc.Item().PaddingTop(4).AlignCenter()
                           .Text(numDoc).Bold().FontSize(10).FontColor(ColSecondary);
                        if (n.AuthNumber is { Length: > 0 })
                        {
                            doc.Item().PaddingTop(6).Text("AUTORIZACIÃ“N SRI").Bold().FontSize(7).FontColor(ColMuted);
                            doc.Item().Text(n.AuthNumber).FontSize(7);
                        }
                        doc.Item().PaddingTop(4).Text($"Doc. modificado: {origNum}").FontSize(7);
                        doc.Item().Text($"Fecha doc. orig: {n.OriginalBill.IssueDate:dd/MM/yyyy}").FontSize(7);
                    });
                });
                col.Item().PaddingTop(4).BorderBottom(1.5f).BorderColor(ColSecondary);
                if (esPrueba)
                    col.Item().AlignCenter().Background(ColAlert).Padding(3)
                       .Text("âš  DOCUMENTO DE PRUEBA â€” NO TIENE VALIDEZ TRIBUTARIA âš ")
                       .Bold().FontSize(8).FontColor(ColWhite);
            });

            page.Content().PaddingTop(8).Column(col =>
            {
                // Cliente (desde factura original)
                col.Item().Background(ColLight).Border(1).BorderColor(ColBorder).Padding(6).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Cliente: {buyer?.LegalName ?? "CONSUMIDOR FINAL"}").Bold().FontSize(8);
                        c.Item().Text($"RUC/CI:  {buyer?.Identification.Number ?? "9999999999"}").FontSize(8);
                    });
                    r.ConstantItem(120).Column(c =>
                    {
                        c.Item().Text($"Fecha: {n.IssueDate:dd/MM/yyyy}").FontSize(8).AlignRight();
                        c.Item().Text($"Motivo: {n.Reason}").FontSize(7).AlignRight().FontColor(ColMuted);
                    });
                });

                col.Item().PaddingTop(8);

                // Tabla de lÃ­neas
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4);
                        cols.ConstantColumn(50);
                        cols.ConstantColumn(60);
                        cols.ConstantColumn(50);
                        cols.ConstantColumn(60);
                    });

                    table.Header(h =>
                    {
                        foreach (var (label, right) in new (string, bool)[]
                        {
                            ("DescripciÃ³n", false),
                            ("Cantidad",    true),
                            ("P.Unitario",  true),
                            ("IVA%",        true),
                            ("Total",       true),
                        })
                        {
                            var hc = h.Cell().Background(ColSecondary).Padding(4);
                            (right ? hc.AlignRight() : hc.AlignLeft())
                                .Text(label).Bold().FontSize(7.5f).FontColor(ColWhite);
                        }
                    });

                    var rn = 0;
                    foreach (var l in lines)
                    {
                        var bg    = rn++ % 2 == 0 ? ColWhite : ColRowAlt;
                        var vatPct = l.Subtotal > 0 ? l.VatTotal / l.Subtotal * 100 : 0;
                        CellText(table, bg, l.Description,       false, 7.5f);
                        CellText(table, bg, $"{l.Quantity:N4}",  true,  7.5f);
                        CellText(table, bg, $"{l.UnitPrice:N4}", true,  7.5f);
                        CellText(table, bg, $"{vatPct:N0}%",     true,  7.5f);
                        CellText(table, bg, $"{l.Total:N2}",     true,  7.5f);
                    }
                });

                col.Item().PaddingTop(6).AlignRight().Width(200).Column(tot =>
                {
                    TotalRow(tot, "Subtotal:", "$", $"{n.Subtotal:N2}");
                    TotalRow(tot, "IVA:",      "$", $"{n.VatTotal:N2}");
                    tot.Item().BorderTop(1.5f).BorderColor(ColSecondary);
                    TotalRow(tot, "VALOR MOD.:", "$", $"{n.Total:N2}", bold: true, bg: ColLight);
                });

                col.Item().PaddingTop(10)
                   .Background(ColLight).Border(1).BorderColor(ColBorder).Padding(6).Column(ck =>
                   {
                       ck.Item().Text("CLAVE DE ACCESO:").Bold().FontSize(7).FontColor(ColSecondary);
                       ck.Item().PaddingTop(2)
                         .Text(FormatAccessKey(n.AccessKey))
                         .FontSize(7.5f).FontFamily("Courier New");
                   });

                if (!string.IsNullOrWhiteSpace(cfg.FooterText))
                    col.Item().PaddingTop(6).Text(cfg.FooterText).FontSize(7).FontColor(ColMuted).Italic();
            });

            page.Footer().BorderTop(0.5f).BorderColor(ColBorder).PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text("Documento generado electrÃ³nicamente. Autorizado por el SRI Ecuador.")
                 .FontSize(6.5f).FontColor(ColMuted);
                r.ConstantItem(60).AlignRight().Text(x =>
                {
                    x.Span("PÃ¡g. ").FontSize(6.5f).FontColor(ColMuted);
                    x.CurrentPageNumber().FontSize(6.5f).FontColor(ColMuted);
                    x.Span(" / ").FontSize(6.5f).FontColor(ColMuted);
                    x.TotalPages().FontSize(6.5f).FontColor(ColMuted);
                });
            });
        })).GeneratePdf();
    }

    // â”€â”€ Helpers de layout â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static void CellText(
        TableDescriptor table,
        string          bg,
        string          text,
        bool            rightAlign = false,
        float           fontSize   = 8)
    {
        var cell = table.Cell()
            .Background(bg)
            .Border(0.3f).BorderColor(ColBorder)
            .Padding(3);

        var aligned = rightAlign ? cell.AlignRight() : cell.AlignLeft();
        aligned.Text(text).FontSize(fontSize);
    }

    private static void TotalRow(
        ColumnDescriptor col,
        string           label,
        string           currency,
        string           amount,
        bool             bold  = false,
        string?          bg    = null)
    {
        var item = col.Item().Padding(3);
        if (bg is not null) item = item.Background(bg);
        item.Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8).Bold().FontColor(ColText);
            r.ConstantItem(15).AlignRight().Text(currency).FontSize(8).FontColor(ColMuted);
            var t = r.ConstantItem(65).AlignRight().Text(amount).FontSize(8);
            if (bold) t.Bold();
        });
    }

    /// <summary>
    /// La clave de acceso SRI tiene 49 dÃ­gitos.
    /// El dÃ­gito en la posiciÃ³n 25 (Ã­ndice 24) indica el ambiente: '1'=producciÃ³n, '2'=pruebas.
    /// </summary>
    private static bool IsAmbientePrueba(string? key)
        => string.IsNullOrWhiteSpace(key) || key.Length < 25 || key[24] == '2';

    private static string FormatAccessKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "â€”";
        // Insertar guiÃ³n cada 10 dÃ­gitos para legibilidad
        return string.Join("-", Enumerable.Range(0, (key.Length + 9) / 10)
            .Select(i => key.Substring(i * 10, Math.Min(10, key.Length - i * 10))));
    }

    private async Task<SubscriberBillingProfile> LoadBillingProfile(Guid subscriberId, CancellationToken ct)
    {
        var cfg = await _db.Set<SubscriberBillingProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SubscriberId == subscriberId, ct);

        return cfg ?? SubscriberBillingProfile.CreateDefault(subscriberId, Guid.Empty);
    }

    private static RideFacturaSnapshot ToRideSnapshot(Invoice invoice) =>
        new(
            invoice.PublicId,
            invoice.EstabCode,
            invoice.EmPointCode,
            invoice.Sequential,
            invoice.AccessKey,
            invoice.IssueDate,
            invoice.Electronic?.AuthorizationNumber,
            invoice.Electronic?.AuthorizationDate,
            invoice.Subtotal,
            invoice.TaxTotal,
            invoice.Total,
            invoice.Lines.Select(l => new RideFacturaLine(
                l.ProductId,
                l.DescriptionSnapshot,
                l.Quantity,
                l.UnitPriceSnapshot,
                l.DiscountAmount,
                l.LineSubtotal,
                l.LineTax,
                l.LineTotal)).ToList());

    private sealed record RideFacturaSnapshot(
        Guid PublicId,
        string EstabCode,
        string EmPointCode,
        string Sequential,
        string AccessKey,
        DateTime IssueDate,
        string? AuthNumber,
        DateTime? AuthDate,
        decimal Subtotal,
        decimal VatTotal,
        decimal Total,
        IReadOnlyList<RideFacturaLine> Lines);

    private sealed record RideFacturaLine(
        Guid ProductId,
        string Description,
        decimal Quantity,
        decimal UnitPrice,
        decimal DiscountAmount,
        decimal Subtotal,
        decimal VatTotal,
        decimal Total);
}

