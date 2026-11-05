using Arrendia.Web.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Arrendia.Web.Services;

public sealed record Emision(string Folio, string Codigo, string UrlVerificacion, string Empresa, string NitEmpresa);

public sealed class DatosContratoPdf
{
    public string Numero { get; set; } = "";
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public decimal MontoDeposito { get; set; }
    public string Inquilino { get; set; } = "";
    public string TipoPersona { get; set; } = "N";
    public string? Dui { get; set; }
    public string? Nit { get; set; }
    public string? Nrc { get; set; }
    public string? RepresentanteLegal { get; set; }
    public string? DireccionFiscal { get; set; }
    public string? Telefono { get; set; }
    public string Unidad { get; set; } = "";
    public string TipoUnidad { get; set; } = "";
    public string? Nivel { get; set; }
    public decimal? AreaM2 { get; set; }
    public bool AplicaIva { get; set; }
    public string Propiedad { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Departamento { get; set; } = "";
    public decimal Renta { get; set; }
    public int DiaPago { get; set; }
    public int DiasGracia { get; set; }
    public bool AplicaMora { get; set; }
    public decimal PorcentajeMoraDiario { get; set; }
    public bool PermitePagosParciales { get; set; }
    public decimal PorcentajeIva { get; set; }
}

public sealed class LineaPdf
{
    public string Descripcion { get; set; } = "";
    public decimal Monto { get; set; }
    public decimal? Saldo { get; set; }
}

public sealed class DatosComprobantePdf
{
    public string Numero { get; set; } = "";
    public DateTime FechaPago { get; set; }
    public string Metodo { get; set; } = "";
    public string? Referencia { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "";
    public string Contrato { get; set; } = "";
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public IReadOnlyList<LineaPdf> Aplicaciones { get; set; } = [];
}

public sealed class DatosAvisoPdf
{
    public string Descripcion { get; set; } = "";
    public string? Periodo { get; set; }
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal MontoBase { get; set; }
    public decimal MontoIva { get; set; }
    public decimal Monto { get; set; }
    public decimal Saldo { get; set; }
    public string Contrato { get; set; } = "";
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public decimal DeudaTotal { get; set; }
}

public sealed class MovimientoCuenta
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal Cargo { get; set; }
    public decimal Abono { get; set; }
    public decimal Saldo { get; set; }
}

public sealed class EstadoCuenta
{
    public decimal SaldoInicial { get; set; }
    public decimal SaldoFinal { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public string Contrato { get; set; } = "";
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public IReadOnlyList<MovimientoCuenta> Movimientos { get; set; } = [];
}

public static class PlantillasPdf
{
    private const string Tinta = "#1F1F1E";
    private const string Suave = "#5E5E5A";
    private const string Borde = "#D4D4CF";
    private const string Acento = "#2F5BD3";
    private static readonly string[] ColumnasEstado = ["Fecha", "Documento", "Descripción", "Cargo", "Abono", "Saldo"];

    public static void Configurar() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Imagenes(IEnumerable<byte[]> imagenes) => Document.Create(d =>
    {
        foreach (var imagen in imagenes)
        {
            d.Page(p =>
            {
                p.Size(PageSizes.Letter);
                p.Margin(24);
                p.Content().AlignCenter().AlignMiddle().Image(imagen).FitArea()
                    .WithRasterDpi(150).WithCompressionQuality(ImageCompressionQuality.Medium);
            });
        }
    }).GeneratePdf();

    public static byte[] Qr(string texto)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }

    public static byte[] Contrato(DatosContratoPdf d, Emision e) => Documento(e, "Contrato de arrendamiento", d.Numero, c =>
    {
        c.Item().Text(t =>
        {
            t.Span("Entre ");
            t.Span(e.Empresa).SemiBold();
            t.Span($", con NIT {e.NitEmpresa}, en adelante «el arrendante», y ");
            t.Span(d.Inquilino).SemiBold();
            t.Span(d.TipoPersona == "J"
                ? $", persona jurídica con NIT {d.Nit}{(d.Nrc is null ? "" : $" y NRC {d.Nrc}")}, representada por {d.RepresentanteLegal ?? "su representante legal"}"
                : $", persona natural con DUI {d.Dui}");
            t.Span(", en adelante «el arrendatario», se celebra el presente contrato de arrendamiento con las cláusulas siguientes.");
        });
        Clausula(c, "Primera. Objeto", $"El arrendante da en arrendamiento la unidad {d.Unidad} ({d.TipoUnidad.ToLowerInvariant()}{(d.Nivel is null ? "" : $", {d.Nivel.ToLowerInvariant()}")}{(d.AreaM2 is null ? "" : $", {Formato.Area(d.AreaM2)}")}) ubicada en {d.Propiedad}, {d.Direccion}, {d.Municipio}, {d.Departamento}.");
        Clausula(c, "Segunda. Plazo", $"El contrato inicia el {Formato.FechaLarga(d.FechaInicio)} y termina el {Formato.FechaLarga(d.FechaFin)}.");
        Clausula(c, "Tercera. Renta", $"La renta mensual es de {Formato.Dinero(d.Renta)}{(d.AplicaIva ? $" más IVA ({Formato.Porcentaje(d.PorcentajeIva)})" : "")}, pagadera a más tardar el día {d.DiaPago} de cada mes. Si ese día es domingo o feriado, el pago vence el siguiente día hábil.");
        Clausula(c, "Cuarta. Depósito", d.MontoDeposito > 0
            ? $"El arrendatario entrega un depósito de garantía de {Formato.Dinero(d.MontoDeposito)}."
            : "No se pacta depósito de garantía.");
        Clausula(c, "Quinta. Mora", d.AplicaMora && d.PorcentajeMoraDiario > 0
            ? $"Después de {d.DiasGracia} días de gracia, el saldo vencido genera un recargo de {Formato.Porcentaje(d.PorcentajeMoraDiario)} diario."
            : "Los pagos atrasados no generan recargo por mora.");
        Clausula(c, "Sexta. Pagos", (d.PermitePagosParciales ? "Se aceptan pagos parciales. " : "No se aceptan pagos parciales: cada pago debe cubrir cargos completos. ") +
            "Los pagos se aplican primero a los cargos más antiguos. Se aceptan pagos en efectivo, transferencia o con tarjeta desde el portal del inquilino.");
        Clausula(c, "Séptima. Mantenimiento", "El arrendatario reportará los problemas de la unidad desde el portal. Las reparaciones causadas por mal uso podrán cobrarse al arrendatario.");
        c.Item().PaddingTop(46).Row(r =>
        {
            Firma(r.RelativeItem(), e.Empresa, "Arrendante");
            r.ConstantItem(40);
            Firma(r.RelativeItem(), d.TipoPersona == "J" ? d.RepresentanteLegal ?? d.Inquilino : d.Inquilino, "Arrendatario");
        });
    });

    public static byte[] Comprobante(DatosComprobantePdf d, Emision e) => Documento(e, "Comprobante de pago", d.Numero, c =>
    {
        Datos(c, [
            ("Fecha", Formato.FechaHora(d.FechaPago)),
            ("Método de pago", d.Metodo),
            ("Referencia", d.Referencia ?? "—"),
            ("Contrato", d.Contrato),
            ("Inquilino", d.Inquilino),
            ("Unidad", $"{d.Unidad} · {d.Propiedad}"),
        ]);
        if (d.Estado == "Anulado")
        {
            c.Item().PaddingVertical(6).Background("#F7E0E0").Padding(8).Text("Este pago fue anulado.").FontColor("#8A2B2B").SemiBold();
        }

        c.Item().PaddingTop(8).Text("Cargos pagados").SemiBold();
        Lineas(c, d.Aplicaciones, "Aplicado", "Saldo restante");
        Total(c, "Total pagado", d.Monto);
    });

    public static byte[] AvisoCobro(DatosAvisoPdf d, Emision e) => Documento(e, "Aviso de cobro", d.Contrato, c =>
    {
        Datos(c, [
            ("Inquilino", d.Inquilino),
            ("Unidad", $"{d.Unidad} · {d.Propiedad}"),
            ("Concepto", d.Descripcion),
            ("Periodo", Formato.Periodo(d.Periodo)),
            ("Emitido", Formato.Fecha(d.FechaEmision)),
            ("Vence", Formato.Fecha(d.FechaVencimiento)),
        ]);
        c.Item().PaddingTop(8).Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(120); });
            Fila(t, "Monto", Formato.Dinero(d.MontoBase));
            Fila(t, "IVA", Formato.Dinero(d.MontoIva));
            Fila(t, "Total del cargo", Formato.Dinero(d.Monto));
        });
        Total(c, "Saldo de este cargo", d.Saldo);
        c.Item().PaddingTop(6).Text($"Deuda total del contrato a la fecha: {Formato.Dinero(d.DeudaTotal)}").FontColor(Suave);
        c.Item().PaddingTop(10).Text("Puedes pagar en el portal del inquilino con tarjeta, o en efectivo o transferencia con la administración.").FontColor(Suave);
    });

    public static byte[] EstadoCuenta(EstadoCuenta d, Emision e) => Documento(e, "Estado de cuenta", d.Contrato, c =>
    {
        Datos(c, [
            ("Inquilino", d.Inquilino),
            ("Unidad", $"{d.Unidad} · {d.Propiedad}"),
            ("Periodo", $"{Formato.Fecha(d.Desde)} al {Formato.Fecha(d.Hasta)}"),
            ("Saldo inicial", Formato.Dinero(d.SaldoInicial)),
        ]);
        c.Item().PaddingTop(8).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(62);
                cd.ConstantColumn(80);
                cd.RelativeColumn();
                cd.ConstantColumn(62);
                cd.ConstantColumn(62);
                cd.ConstantColumn(66);
            });
            t.Header(h =>
            {
                foreach (var titulo in ColumnasEstado)
                {
                    h.Cell().BorderBottom(1).BorderColor(Tinta).PaddingVertical(4).Text(titulo).SemiBold().FontSize(8.5f);
                }
            });
            foreach (var m in d.Movimientos)
            {
                Celda(t, Formato.Fecha(m.Fecha));
                Celda(t, m.Documento);
                Celda(t, m.Descripcion);
                Celda(t, m.Cargo == 0 ? "" : Formato.Dinero(m.Cargo), true);
                Celda(t, m.Abono == 0 ? "" : Formato.Dinero(m.Abono), true);
                Celda(t, Formato.Dinero(m.Saldo), true);
            }
        });
        if (d.Movimientos.Count == 0)
        {
            c.Item().PaddingTop(6).Text("Sin movimientos en este periodo.").FontColor(Suave);
        }

        Total(c, "Saldo al final del periodo", d.SaldoFinal);
    });

    public static byte[] Tabla(string titulo, string subtitulo, string[] columnas, IEnumerable<string[]> filas, bool[] numericas) =>
        Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.Letter.Landscape());
            p.Margin(32);
            p.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Tinta));
            p.Header().PaddingBottom(10).Column(c =>
            {
                c.Item().Text(titulo).FontSize(15).SemiBold();
                c.Item().Text(subtitulo).FontColor(Suave);
            });
            p.Content().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    foreach (var _ in columnas)
                    {
                        cd.RelativeColumn();
                    }
                });
                t.Header(h =>
                {
                    for (var i = 0; i < columnas.Length; i++)
                    {
                        var celda = h.Cell().BorderBottom(1).BorderColor(Tinta).PaddingVertical(4);
                        (numericas[i] ? celda.AlignRight() : celda).Text(columnas[i]).SemiBold();
                    }
                });
                foreach (var fila in filas)
                {
                    for (var i = 0; i < fila.Length; i++)
                    {
                        Celda(t, fila[i], numericas[i]);
                    }
                }
            });
            p.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Generado el {Formato.FechaHora(DateTime.UtcNow)}").FontColor(Suave);
                r.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Página ").FontColor(Suave);
                    x.CurrentPageNumber().FontColor(Suave);
                    x.Span(" de ").FontColor(Suave);
                    x.TotalPages().FontColor(Suave);
                });
            });
        })).GeneratePdf();

    private static byte[] Documento(Emision e, string titulo, string referencia, Action<ColumnDescriptor> contenido)
    {
        var qr = Qr(e.UrlVerificacion);
        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.Letter);
            p.Margin(42);
            p.DefaultTextStyle(x => x.FontSize(10).FontColor(Tinta).LineHeight(1.35f));
            p.Header().PaddingBottom(14).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text(e.Empresa).FontSize(12).SemiBold();
                    c.Item().Text($"NIT {e.NitEmpresa}").FontColor(Suave).FontSize(9);
                });
                r.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text(titulo).FontSize(15).SemiBold().FontColor(Acento);
                    c.Item().AlignRight().Text(referencia).FontSize(10);
                    c.Item().AlignRight().Text($"Folio {e.Folio}").FontColor(Suave).FontSize(9);
                });
            });
            p.Content().Column(c =>
            {
                c.Spacing(6);
                contenido(c);
            });
            p.Footer().BorderTop(1).BorderColor(Borde).PaddingTop(8).Row(r =>
            {
                r.ConstantItem(64).Image(qr);
                r.RelativeItem().PaddingLeft(10).Column(c =>
                {
                    c.Item().Text("Verifica este documento escaneando el código o en:").FontSize(8).FontColor(Suave);
                    c.Item().Text(e.UrlVerificacion).FontSize(8);
                    c.Item().Text($"Código de verificación: {e.Codigo}").FontSize(8).SemiBold();
                });
                r.ConstantItem(70).AlignRight().AlignBottom().Text(x =>
                {
                    x.CurrentPageNumber().FontSize(8).FontColor(Suave);
                    x.Span(" / ").FontSize(8).FontColor(Suave);
                    x.TotalPages().FontSize(8).FontColor(Suave);
                });
            });
        })).GeneratePdf();
    }

    private static void Clausula(ColumnDescriptor c, string titulo, string texto) =>
        c.Item().PaddingTop(4).Text(t =>
        {
            t.Span(titulo + ". ").SemiBold();
            t.Span(texto);
        });

    private static void Firma(IContainer contenedor, string nombre, string rol) =>
        contenedor.Column(c =>
        {
            c.Item().BorderTop(1).BorderColor(Tinta).PaddingTop(4).AlignCenter().Text(nombre).SemiBold();
            c.Item().AlignCenter().Text(rol).FontColor(Suave).FontSize(9);
        });

    private static void Datos(ColumnDescriptor c, (string Etiqueta, string Valor)[] datos) =>
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.ConstantColumn(110); cd.RelativeColumn(); });
            foreach (var (etiqueta, valor) in datos)
            {
                t.Cell().PaddingVertical(2).Text(etiqueta).FontColor(Suave);
                t.Cell().PaddingVertical(2).Text(valor);
            }
        });

    private static void Lineas(ColumnDescriptor c, IReadOnlyList<LineaPdf> lineas, string columnaMonto, string columnaSaldo) =>
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(90); cd.ConstantColumn(90); });
            t.Header(h =>
            {
                h.Cell().BorderBottom(1).BorderColor(Tinta).PaddingVertical(4).Text("Descripción").SemiBold();
                h.Cell().BorderBottom(1).BorderColor(Tinta).PaddingVertical(4).AlignRight().Text(columnaMonto).SemiBold();
                h.Cell().BorderBottom(1).BorderColor(Tinta).PaddingVertical(4).AlignRight().Text(columnaSaldo).SemiBold();
            });
            foreach (var l in lineas)
            {
                Celda(t, l.Descripcion);
                Celda(t, Formato.Dinero(l.Monto), true);
                Celda(t, l.Saldo is null ? "" : Formato.Dinero(l.Saldo), true);
            }
        });

    private static void Total(ColumnDescriptor c, string etiqueta, decimal monto) =>
        c.Item().PaddingTop(8).AlignRight().Text(t =>
        {
            t.Span(etiqueta + ": ").FontColor(Suave);
            t.Span(Formato.Dinero(monto)).FontSize(13).SemiBold();
        });

    private static void Fila(TableDescriptor t, string etiqueta, string valor)
    {
        t.Cell().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(3).Text(etiqueta);
        t.Cell().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(3).AlignRight().Text(valor);
    }

    private static void Celda(TableDescriptor t, string texto, bool derecha = false)
    {
        var celda = t.Cell().BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(3).PaddingRight(4);
        (derecha ? celda.AlignRight() : celda).Text(texto);
    }
}
