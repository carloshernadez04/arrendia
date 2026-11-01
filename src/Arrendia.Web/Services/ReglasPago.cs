namespace Arrendia.Web.Services;

public sealed record CargoPendiente(int Id, string Descripcion, decimal Saldo, DateTime FechaBase, bool EsMora);

public sealed record Aplicacion(int CargoId, string Descripcion, decimal Saldo, decimal Aplicado, int Orden)
{
    public bool EsParcial => Aplicado > 0 && Aplicado < Saldo;
}

public static class ReglasPago
{
    public static IReadOnlyList<Aplicacion> Repartir(IEnumerable<CargoPendiente> cargos, decimal monto)
    {
        var resultado = new List<Aplicacion>();
        var acumulado = 0m;
        var orden = 0;
        foreach (var c in cargos.Where(c => c.Saldo > 0).OrderBy(c => c.FechaBase).ThenBy(c => c.EsMora ? 0 : 1).ThenBy(c => c.Id))
        {
            orden++;
            var antes = acumulado;
            acumulado += c.Saldo;
            var aplicado = acumulado <= monto ? c.Saldo : antes < monto ? monto - antes : 0m;
            resultado.Add(new Aplicacion(c.Id, c.Descripcion, c.Saldo, aplicado, orden));
        }

        return resultado;
    }

    public static decimal Deuda(IEnumerable<CargoPendiente> cargos) => cargos.Where(c => c.Saldo > 0).Sum(c => c.Saldo);

    public static string? ValidarMonto(decimal? monto, decimal deuda)
    {
        if (monto is null || monto <= 0)
        {
            return "El monto debe ser mayor a $0.00.";
        }

        if (decimal.Round(monto.Value, 2) != monto.Value)
        {
            return "El monto solo puede tener dos decimales.";
        }

        if (deuda <= 0)
        {
            return "Este contrato no tiene saldo pendiente.";
        }

        return monto > deuda
            ? $"El monto ({Models.Formato.Dinero(monto)}) supera lo que se debe ({Models.Formato.Dinero(deuda)}). En Arrendia no hay saldo a favor."
            : null;
    }

    public static string? ValidarParciales(IReadOnlyList<Aplicacion> aplicaciones, bool permiteParciales) =>
        !permiteParciales && aplicaciones.Any(a => a.EsParcial)
            ? "Este contrato no permite pagos parciales: el monto debe cubrir cargos completos."
            : null;

    public static string? Validar(decimal? monto, IReadOnlyCollection<CargoPendiente> cargos, bool permiteParciales)
    {
        var error = ValidarMonto(monto, Deuda(cargos));
        return error ?? ValidarParciales(Repartir(cargos, monto!.Value), permiteParciales);
    }
}
