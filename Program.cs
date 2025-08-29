// Program.cs (ASP.NET Core 8/9 Minimal API)
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.HttpLogging;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add HTTP logging for diagnostics (optional, can be tuned)
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders |
                            HttpLoggingFields.ResponsePropertiesAndHeaders;
});

// Enforce HTTPS and HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(60);
});

var app = builder.Build();

app.UseHsts();
app.UseHttpsRedirection();
app.UseHttpLogging();

// Global exception handler middleware (production safe)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // Log the error (replace with your logging framework as needed)
        app.Logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = "An unexpected error occurred."
        });
        await context.Response.WriteAsync(errorJson);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/ExecProc", async (HttpRequest req, IConfiguration config) =>
{
    // API Key validation
    if (!req.Headers.TryGetValue("x-api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        return Results.Unauthorized();

    var expectedKey = config["ApiKey"];
    if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
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

    // Limit request body size (e.g., 10 KB)
    if (req.ContentLength is > 10_240)
    {
        return Results.BadRequest(new
        {
            ErrorCode = "PayloadTooLarge",
            ErrorType = 1,
            Message = "Request payload too large.",
            AdditionalErrors = Array.Empty<string>(),
            Data = (object?)null
        });
    }

    using var reader = new StreamReader(req.Body);
    var raw = await reader.ReadToEndAsync();

    List<Dictionary<string, object>> items = [];
    try
    {
        items = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(raw)
            ?? [];
    }
    catch
    {
        try
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
            if (obj != null)
                items.Add(obj);
        }
        catch
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

    var connStr = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connStr))
        return Results.Problem("Connection string not configured.", statusCode: 500);

    await using var conn = new SqlConnection(connStr);
    try
    {
        await conn.OpenAsync();
    }
    catch
    {
        return Results.Problem("Database connection failed.", statusCode: 500);
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
        catch
        {
            return Results.BadRequest(new
            {
                ErrorCode = "SqlError",
                ErrorType = 1,
                Message = "Database operation failed.",
                AdditionalErrors = Array.Empty<string>(),
                Data = new { action, internalID = internalIDValue, changeValue = changeValueValue }
            });
        }
    }

    return Results.Ok(new
    {
        ConfirmationMessageCode = (string?)null,
        ConfirmationMessage = (string?)null,
        MessageCode = "MSG_SUCCESS01",
        Message = "Stored procedure execute successful."
    });
});

app.Run();