using ClosedXML.Excel;

namespace Arrendia.Web.Services;

public sealed record ColumnaInforme(string Titulo, bool Dinero = false, bool Numero = false)
{
    public bool Derecha => Dinero || Numero;
}

public sealed class Informe
{
    public required string Titulo { get; init; }
    public required string Subtitulo { get; init; }
    public required IReadOnlyList<ColumnaInforme> Columnas { get; init; }
    public required IReadOnlyList<object?[]> Filas { get; init; }
    public object?[]? Totales { get; init; }

    public string Texto(int columna, object? valor) => valor switch
    {
        null => "",
        decimal d when Columnas[columna].Dinero => Models.Formato.Dinero(d),
        decimal d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        DateTime f => Models.Formato.Fecha(f),
        _ => valor.ToString() ?? "",
    };

    public IEnumerable<string[]> FilasTexto(bool conTotales)
    {
        var filas = conTotales && Totales is not null ? Filas.Append(Totales) : Filas;
        return filas.Select(f => f.Select((v, i) => Texto(i, v)).ToArray());
    }
}

public static class Exportacion
{
    public static byte[] Pdf(Informe informe) =>
        PlantillasPdf.Tabla(informe.Titulo, informe.Subtitulo, [.. informe.Columnas.Select(c => c.Titulo)], informe.FilasTexto(true),
            [.. informe.Columnas.Select(c => c.Derecha)]);

    public static byte[] Excel(Informe informe)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Informe");
        hoja.Cell(1, 1).Value = informe.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        hoja.Cell(2, 1).Value = informe.Subtitulo;
        for (var c = 0; c < informe.Columnas.Count; c++)
        {
            var celda = hoja.Cell(4, c + 1);
            celda.Value = informe.Columnas[c].Titulo;
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E7E3");
        }

        var fila = 5;
        foreach (var valores in informe.Totales is null ? informe.Filas : informe.Filas.Append(informe.Totales))
        {
            for (var c = 0; c < valores.Length; c++)
            {
                var celda = hoja.Cell(fila, c + 1);
                switch (valores[c])
                {
                    case decimal d:
                        celda.Value = d;
                        celda.Style.NumberFormat.Format = informe.Columnas[c].Dinero ? "$#,##0.00" : "0.##";
                        break;
                    case int i:
                        celda.Value = i;
                        break;
                    case DateTime f:
                        celda.Value = f;
                        celda.Style.DateFormat.Format = "dd/mm/yyyy";
                        break;
                    case null:
                        break;
                    default:
                        celda.Value = valores[c]!.ToString();
                        break;
                }
            }

            if (ReferenceEquals(valores, informe.Totales))
            {
                hoja.Row(fila).Style.Font.Bold = true;
            }

            fila++;
        }

        hoja.Columns().AdjustToContents(4, fila);
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
