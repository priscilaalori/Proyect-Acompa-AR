using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

// Read-only verification of the fictitious accounts created through the visible interfaces.
var backend = Path.GetFullPath("..");
var reportPath = Path.GetFullPath(args.Length > 0 ? args[0] : "app/build/visible-e2e/run.json");
using var run = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
int mayor = run.RootElement.GetProperty("mayorId").GetInt32();
int companion = run.RootElement.GetProperty("companionId").GetInt32();
var configuration = new ConfigurationBuilder().SetBasePath(backend)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true);
var project = XDocument.Load(Directory.GetFiles(backend, "*.csproj").Single());
var secrets = project.Descendants("UserSecretsId").FirstOrDefault()?.Value;
if (secrets != null) configuration.AddUserSecrets(secrets);
configuration.AddEnvironmentVariables();
var settings = configuration.Build();
using var connection = new MySqlConnection(settings.GetConnectionString("MySql"));
await connection.OpenAsync();
var checks = new List<object>();
using (var users = new MySqlCommand("SELECT id_usuario, email, tipo_usuario FROM usuario WHERE id_usuario IN (@mayor, @companion)", connection))
{
    users.Parameters.AddWithValue("@mayor", mayor); users.Parameters.AddWithValue("@companion", companion);
    using var reader = await users.ExecuteReaderAsync();
    int count = 0;
    while (await reader.ReadAsync())
    {
        int id = reader.GetInt32(0);
        string expectedEmail = run.RootElement.GetProperty(id == mayor ? "mayorEmail" : "companionEmail").GetString()!;
        if (reader.GetString(1) != expectedEmail || !expectedEmail.EndsWith("@example.invalid")) throw new Exception("Unexpected test account");
        if (reader.GetString(2) != (id == mayor ? "MAYOR" : "ACOMPANANTE")) throw new Exception("Wrong test account role");
        count++;
    }
    if (count != 2) throw new Exception("Both test accounts must exist");
    checks.Add(new { check = "test_accounts_persisted", passed = true });
}
using (var link = new MySqlCommand("SELECT estado FROM vinculacion WHERE id_mayor=@mayor AND id_acompanante=@companion", connection))
{
    link.Parameters.AddWithValue("@mayor", mayor); link.Parameters.AddWithValue("@companion", companion);
    if ((string?)await link.ExecuteScalarAsync() != "ACEPTADA") throw new Exception("Test relationship was not accepted");
    checks.Add(new { check = "relationship_accepted", passed = true });
}
var expected = run.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(
    c => c.GetProperty("name").GetString()!, c => c.GetProperty("done").GetBoolean() ? "CUMPLIDO" : "NO_CUMPLIDO");
var attended = run.RootElement.GetProperty("cases").EnumerateArray()
    .Where(c => c.GetProperty("type").GetString() == "turno" && c.GetProperty("done").GetBoolean())
    .Select(c => c.GetProperty("name").GetString()!).ToHashSet();
var results = new List<object>();
using (var command = new MySqlCommand("""
    SELECT r.id_recordatorio, COALESCE(m.nombre,t.especialidad) nombre,
           c.estado, t.estado,
           (SELECT COUNT(*) FROM cumplimiento total WHERE total.id_recordatorio=r.id_recordatorio) registros
    FROM recordatorio r
    LEFT JOIN medicamento m ON m.id_medicamento=r.id_medicamento
    LEFT JOIN turno_medico t ON t.id_turno=r.id_turno
    LEFT JOIN cumplimiento c ON c.id_recordatorio=r.id_recordatorio
    WHERE COALESCE(m.id_mayor,t.id_mayor)=@mayor AND r.estado <> 'CANCELADO'
    ORDER BY r.id_recordatorio
    """, connection))
{
    command.Parameters.AddWithValue("@mayor", mayor);
    using var reader = await command.ExecuteReaderAsync();
    var seen = new HashSet<string>();
    while (await reader.ReadAsync())
    {
        string name = reader.GetString(1);
        if (!expected.TryGetValue(name, out var state)) continue;
        string? actual = reader.IsDBNull(2) ? null : reader.GetString(2);
        if (actual != state || reader.GetInt64(4) != 1) throw new Exception("Missing, duplicate or wrong fulfillment: " + name);
        if (attended.Contains(name) && reader.GetString(3) != "REALIZADO") throw new Exception("Attended appointment was not marked REALIZADO");
        seen.Add(name);
        results.Add(new { idRecordatorio = reader.GetInt32(0), name, expected = state, actual, passed = true });
    }
    if (seen.Count != expected.Count) throw new Exception("Expected all notification responses in the database");
}
var report = new { verifiedAt = DateTimeOffset.UtcNow, mayor, companion, checks, results, passed = true };
var reportName = Path.GetFileName(reportPath) == "run.json" ? "database-check.json" : Path.GetFileNameWithoutExtension(reportPath) + "-database-check.json";
await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(reportPath)!, reportName), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS: both registered accounts, accepted relationship and all four unique fulfillment rows verified directly in MySQL.");
