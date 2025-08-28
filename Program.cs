// Program.cs (ASP.NET Core 8/9 Minimal API)
using System.Data;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/outbound/customapi/WavePriority", async (HttpRequest req) =>
{
    using var reader = new StreamReader(req.Body);
    var raw = await reader.ReadToEndAsync();

    // Try to parse as array first, then as single object
    object? payload;
    try
    {
        payload = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(raw);
    }
    catch
    {
        payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
    }

    var items = new List<Dictionary<string, object>>();
    if (payload is List<Dictionary<string, object>> arr && arr.Count > 0)
        items = arr;
    else if (payload is Dictionary<string, object> obj)
        items = new List<Dictionary<string, object>> { obj };
    else
        return Results.BadRequest(new { success = false, message = "Invalid payload." });

    var results = new List<object>();
    var connStr = builder.Configuration.GetConnectionString("Db")!;
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    foreach (var item in items)
    {
        if (!item.TryGetValue("launchNum", out var launchNum) || !item.TryGetValue("priority", out var priority))
            return Results.BadRequest(new { success = false, message = "Missing required params 'launchNum' and/or 'priority'." });

        // Extract values from JsonElement if needed
        object? launchNumValue = launchNum is System.Text.Json.JsonElement je1 && je1.ValueKind == System.Text.Json.JsonValueKind.Number
    ? je1.GetInt32()
    : launchNum is System.Text.Json.JsonElement je1s && je1s.ValueKind == System.Text.Json.JsonValueKind.String
        ? je1s.GetString()
        : launchNum;

        object? priorityValue = priority is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.Number
            ? je2.GetInt32()
            : priority is System.Text.Json.JsonElement je2s && je2s.ValueKind == System.Text.Json.JsonValueKind.String
                ? je2s.GetString()
                : priority;

        await using var cmd = new SqlCommand("usp_WavePriority", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@launchNum", launchNumValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@priority", priorityValue ?? DBNull.Value);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            results.Add(new { ok = true });
        }
        catch (Exception ex)
        {
            results.Add(new { ok = false, error = ex.Message });
        }
    }

    var anyFail = results.Any(r => (bool?)r?.GetType().GetProperty("ok")?.GetValue(r)! == false);
    return Results.Ok(new
    {
        success = !anyFail,
        message = !anyFail ? "Execution successful." : "One or more items failed.",
        results
    });
});

app.Run();