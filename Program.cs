// Program.cs (ASP.NET Core 8/9 Minimal API)
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using static System.Collections.Specialized.BitVector32;

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

app.MapPost("/ExecProc", async (HttpRequest req) =>
{
    try
    {
        // API Key validation
        if (!req.Headers.TryGetValue("x-api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return Results.Unauthorized();

        var expectedKey = builder.Configuration["ApiKey"];
        if (!string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
            return Results.Unauthorized();

        // Get 'action' from query string
        var action = req.Query["action"].ToString();
        if (string.IsNullOrWhiteSpace(action))
        {
            return Results.BadRequest(new
            {
                ErrorCode = "MissingAction",
                ErrorType = 1,
                Message = "Missing required query parameter 'action'.",
                AdditionalErrors = Array.Empty<string>(),
                Data = (object?)null
            });
        }

        // Read JSON body for internalID and changeValue
        using var reader = new StreamReader(req.Body);
        var raw = await reader.ReadToEndAsync();

        List<Dictionary<string, object>> items = new();
        try
        {
            // Try to parse as array first
            items = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(raw);
        }
        catch
        {
            try
            {
                // Try to parse as single object
                var obj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
                if (obj != null)
                    items.Add(obj);
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

        if (items.Count == 0)
        {
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

        foreach (var body in items)
        {
            if (!body.TryGetValue("internalID", out var internalID) ||
                !body.TryGetValue("changeValue", out var changeValue))
            {
                return Results.BadRequest(new
                {
                    ErrorCode = "MissingParams",
                    ErrorType = 1,
                    Message = "Missing required params 'internalID' and/or 'changeValue'.",
                    AdditionalErrors = Array.Empty<string>(),
                    Data = (object?)null
                });
            }

            // Extract values from JsonElement if needed
            object? internalIDValue = internalID is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.Number
                ? je2.GetInt32()
                : internalID is System.Text.Json.JsonElement je2s && je2s.ValueKind == System.Text.Json.JsonValueKind.String
                    ? je2s.GetString()
                    : internalID;

            object? changeValueValue = changeValue is System.Text.Json.JsonElement je3 && je3.ValueKind == System.Text.Json.JsonValueKind.Number
                ? je3.GetInt32()
                : changeValue is System.Text.Json.JsonElement je3s && je3s.ValueKind == System.Text.Json.JsonValueKind.String
                    ? je3s.GetString()
                    : changeValue;

            await using var cmd = new SqlCommand("usp_CustomAPI", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@action", action ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@internalID", internalIDValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@changeValue", changeValueValue ?? DBNull.Value);

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
                    Data = new { action, internalID = internalIDValue, changeValue = changeValueValue }
                });
            }
        }

        // Success response
        return Results.Ok(new
        {
            ConfirmationMessageCode = (string?)null,
            ConfirmationMessage = (string?)null,
            MessageCode = "MSG_SUCCESS01",
            Message = "Stored procedure execute successful."
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unhandled error: {ex.Message}\n{ex.StackTrace}", statusCode: 500);
    }
});

app.Run();