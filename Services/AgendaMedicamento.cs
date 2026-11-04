namespace AcompañAR.Services;

public static class AgendaMedicamento
{
    public static TimeSpan? Intervalo(string frecuencia)
    {
        var texto = frecuencia.Trim().ToLowerInvariant();
        if (texto is "toma unica" or "toma única" or "una vez por dia" or "una vez por día")
            return TimeSpan.FromDays(1);
        var match = System.Text.RegularExpressions.Regex.Match(texto, @"^cada (\d+) (horas?|d[ií]as?)$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var cantidad) || cantidad < 1)
            return null;
        if (match.Groups[2].Value.StartsWith("hora") && cantidad <= 168) return TimeSpan.FromHours(cantidad);
        if (!match.Groups[2].Value.StartsWith("hora") && cantidad <= 365) return TimeSpan.FromDays(cantidad);
        return null;
    }

    public static IEnumerable<DateTime> Tomas(DateTime inicio, DateTime? fin, string frecuencia,
        DateTime desde, DateTime hasta)
    {
        var intervalo = Intervalo(frecuencia);
        if (intervalo is null || hasta <= desde) yield break;
        var unica = frecuencia.Trim().Equals("Toma unica", StringComparison.OrdinalIgnoreCase)
            || frecuencia.Trim().Equals("Toma única", StringComparison.OrdinalIgnoreCase);
        if (unica)
        {
            if (inicio >= desde && inicio < hasta && (fin is null || inicio.Date <= fin.Value.Date)) yield return inicio;
            yield break;
        }
        // Conserva el horario original: cada 8 h puede cruzar la medianoche.
        var pasos = desde > inicio ? (desde.Ticks - inicio.Ticks) / intervalo.Value.Ticks : 0;
        var toma = inicio.AddTicks(pasos * intervalo.Value.Ticks);
        if (toma < desde) toma += intervalo.Value;
        for (; toma < hasta && (fin is null || toma.Date <= fin.Value.Date); toma += intervalo.Value)
            yield return toma;
    }
}
