using AcompañAR.Services;
using System.Text.Json;

// Exercise the production scheduler with explicit dates, without changing any system clock.
var results = new List<object>();
void Check(string name, bool passed)
{
    if (!passed) throw new Exception("FAIL: " + name);
    results.Add(new { name, passed });
    Console.WriteLine("PASS: " + name);
}
DateTime[] Schedule(DateTime start, string frequency, DateTime from, DateTime until, DateTime? end = null)
    => AgendaMedicamento.Tomas(start, end, frequency, from, until).ToArray();
var start = new DateTime(2026, 10, 30, 20, 0, 0);
var day = start.Date;
var first = Schedule(start, "Cada 8 horas", day, day.AddDays(3));
Check("8-hour cadence crosses midnight: 20:00, 04:00, 12:00, 20:00",
    first.Take(4).SequenceEqual(new[] { start, day.AddDays(1).AddHours(4), day.AddDays(1).AddHours(12), day.AddDays(1).AddHours(20) }));

// Independent hour-by-hour oracle, including intervals that do not divide 24.
foreach (int hours in new[] { 1, 5, 8, 12, 24, 36, 168 })
{
    var until = day.AddDays(45);
    var expected = Enumerable.Range(0, 45 * 24).Select(h => day.AddHours(h))
        .Where(t => t >= start && (t - start).TotalHours % hours == 0).ToArray();
    var actual = Schedule(start, $"Cada {hours} horas", day, until);
    Check($"Every {hours} hours over 45 days matches independent hourly calendar", actual.SequenceEqual(expected));

    // Simulate today's/tomorrow's downloads every day. Compare each window,
    // rather than merely deduplicating, so omitted or repeated occurrences fail.
    bool windowsMatch = true;
    for (int offset = 0; offset < 44; offset++)
    {
        var today = day.AddDays(offset);
        windowsMatch &= Schedule(start, $"Cada {hours} horas", today, today.AddDays(2))
            .SequenceEqual(expected.Where(t => t >= today && t < today.AddDays(2)));
    }
    Check($"Every {hours} hours retains original cadence across 44 daily download windows", windowsMatch);
}
Check("Treatment continues beyond initial 30-day horizon",
    Schedule(start, "Cada 8 horas", day.AddDays(40), day.AddDays(42))
        .SequenceEqual(new[] { 4, 12, 20, 28, 36, 44 }.Select(h => day.AddDays(40).AddHours(h))));
Check("End date includes its last day's doses and excludes following days",
    Schedule(start, "Cada 8 horas", day, day.AddDays(5), day.AddDays(1))
        .SequenceEqual(new[] { start, day.AddDays(1).AddHours(4), day.AddDays(1).AddHours(12), day.AddDays(1).AddHours(20) }));
Check("Future start does not produce early doses",
    Schedule(start.AddDays(7), "Cada 8 horas", day, day.AddDays(2)).Length == 0);
Check("Exact window boundary is included once",
    Schedule(start, "Cada 8 horas", start.AddHours(8), start.AddHours(16))
        .SequenceEqual(new[] { start.AddHours(8) }));
Check("Every two days preserves the original hour across month boundary",
    Schedule(start, "Cada 2 dias", day, day.AddDays(7))
        .SequenceEqual(new[] { start, start.AddDays(2), start.AddDays(4), start.AddDays(6) }));
Check("Single dose never recurs on following days",
    Schedule(start, "Toma unica", day.AddDays(1), day.AddDays(45)).Length == 0);
foreach (var invalid in new[] { "Cada 0 horas", "Cada 169 horas", "sin pauta" })
    Check("Reject invalid interval: " + invalid, Schedule(start, invalid, day, day.AddDays(3)).Length == 0);

var output = Path.GetFullPath("app/build/medication-schedule/results.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
    verifiedAt = DateTimeOffset.UtcNow, passed = true, checks = results,
    scope = "Production calendar calculation and rolling date windows; excludes SQL, Android background jobs and real notifications."
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{results.Count} checks passed. Report: {output}");
