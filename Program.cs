// Program.cs (ASP.NET Core 8/9 Minimal API)
using System.Data;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Global exception handler middleware (for debugging only)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = ex.Message,
            stackTrace = ex.StackTrace
        });
        await context.Response.WriteAsync(errorJson);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/WavePriority", async (HttpRequest req) =>
{
    try
    {
        // API Key validation
        if (!req.Headers.TryGetValue("x-api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return Results.Unauthorized();

        var expectedKey = builder.Configuration["ApiKey"];
        if (!string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
            return Results.Unauthorized();

        var contentType = req.ContentType?.ToLowerInvariant();
        List<Dictionary<string, object>> items = new();

        if (contentType != null && contentType.Contains("application/x-www-form-urlencoded"))
        {
            var form = await req.ReadFormAsync();
            var dict = new Dictionary<string, object>();
            foreach (var kvp in form)
            {
                dict[kvp.Key] = kvp.Value.ToString();
            }
            items.Add(dict);
        }
        else
        {
            using var reader = new StreamReader(req.Body);
            var raw = await reader.ReadToEndAsync();
            object? payload;
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(raw);
            }
            catch
            {
                try
                {
                    payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
                }
                catch (Exception ex2)
                {
                    return Results.BadRequest(new
                    {
                        ErrorCode = "InvalidPayload",
                        ErrorType = 1,
                        Message = "Invalid payload.",
                        AdditionalErrors = new[] { ex2.Message },
                        Data = (object?)null
                    });
                }
            }

            if (payload is List<Dictionary<string, object>> arr && arr.Count > 0)
                items = arr;
            else if (payload is Dictionary<string, object> obj)
                items = new List<Dictionary<string, object>> { obj };
            else
                return Results.BadRequest(new
                {
                    ErrorCode = "InvalidPayload",
                    ErrorType = 1,
                    Message = "Invalid payload.",
                    AdditionalErrors = Array.Empty<string>(),
                    Data = (object?)null
                });
        }

        string? connStr = null;
        try
        {
            connStr = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connStr))
                throw new Exception("Connection string 'DefaultConnection' not found or empty.");
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error getting connection string: {ex.Message}", statusCode: 500);
        }

        await using var conn = new SqlConnection(connStr);
        try
        {
            await conn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error opening SQL connection: {ex.Message}", statusCode: 500);
        }

        foreach (var item in items)
        {
            if (!item.TryGetValue("launchNum", out var launchNum) || !item.TryGetValue("priority", out var priority))
            {
                return Results.BadRequest(new
                {
                    ErrorCode = "MissingParams",
                    ErrorType = 1,
                    Message = "Missing required params 'launchNum' and/or 'priority'.",
                    AdditionalErrors = Array.Empty<string>(),
                    Data = (object?)null
                });
            }

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
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new
                {
                    ErrorCode = "SqlError",
                    ErrorType = 1,
                    Message = ex.Message,
                    AdditionalErrors = Array.Empty<string>(),
                    Data = new { launchNum = launchNumValue, priority = priorityValue }
                });
            }
        }

        // Success response
        return Results.Ok(new
        {
            ConfirmationMessageCode = (string?)null,
            ConfirmationMessage = (string?)null,
            MessageCode = "MSG_CHANGEPRIORITY01",
            Message = "Change priority successful."
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unhandled error: {ex.Message}\n{ex.StackTrace}", statusCode: 500);
    }
});

app.Run();