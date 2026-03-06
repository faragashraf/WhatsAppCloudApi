using System.Security.Claims;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class ApiLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ApiLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<ApiLoggingMiddleware> logger)
    {
        var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        var originalBodyStream = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            var responseBodyText = await ReadResponseBodyAsync(context.Response);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            try
            {
                var companyId = TryParseInt(context.User.FindFirstValue("CompanyId"));
                var userId = TryParseInt(context.User.FindFirstValue("UserId"));

                var log = new ApiLog
                {
                    CompanyId = companyId,
                    CompanyUserId = userId,
                    Endpoint = endpoint,
                    HttpMethod = context.Request.Method,
                    RequestBody = Truncate(requestBody, 8000),
                    ResponseBody = Truncate(responseBodyText, 8000),
                    StatusCode = context.Response.StatusCode,
                    IpAddress = ipAddress,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await PersistLogAsync(context, log, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist API log entry.");
            }
        }
    }

    private static async Task PersistLogAsync(HttpContext context, ApiLog log, ILogger logger)
    {
        await using var scope = context.RequestServices.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            await InsertApiLogFallbackAsync(dbContext, log, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist API log entry using fallback insert.");
        }
    }

    private static async Task InsertApiLogFallbackAsync(ApplicationDbContext dbContext, ApiLog log, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var tableSchema = await GetTableSchemaAsync(connection, "ApiLogs", cancellationToken);
        if (tableSchema is null)
        {
            return;
        }

        var columns = await GetColumnsWithTypesAsync(connection, "ApiLogs", tableSchema, cancellationToken);
        if (columns.Count == 0)
        {
            return;
        }

        var mapped = new List<(string Column, object Value)>();

        var companyIdColumn = PickColumn(columns, "CompanyId");
        if (companyIdColumn is not null && log.CompanyId.HasValue)
        {
            mapped.Add((companyIdColumn, log.CompanyId.Value));
        }

        var companyUserIdColumn = PickColumn(columns, "CompanyUserId", "UserId");
        if (companyUserIdColumn is not null && log.CompanyUserId.HasValue)
        {
            mapped.Add((companyUserIdColumn, log.CompanyUserId.Value));
        }

        var endpointColumn = PickColumn(columns, "Endpoint", "Path");
        if (endpointColumn is not null)
        {
            mapped.Add((endpointColumn, log.Endpoint));
        }

        var httpMethodColumn = PickColumn(columns, "HttpMethod", "Method", "Verb");
        if (httpMethodColumn is not null)
        {
            mapped.Add((httpMethodColumn, log.HttpMethod));
        }

        var requestBodyColumn = PickColumn(columns, "RequestBody", "Request");
        if (requestBodyColumn is not null && log.RequestBody is not null)
        {
            mapped.Add((requestBodyColumn, log.RequestBody));
        }

        var responseBodyColumn = PickColumn(columns, "ResponseBody", "Response");
        if (responseBodyColumn is not null && log.ResponseBody is not null)
        {
            mapped.Add((responseBodyColumn, log.ResponseBody));
        }

        var statusCodeColumn = PickColumn(columns, "StatusCode");
        if (statusCodeColumn is not null)
        {
            mapped.Add((statusCodeColumn, log.StatusCode));
        }

        var ipAddressColumn = PickColumn(columns, "IpAddress", "ClientIp");
        if (ipAddressColumn is not null && log.IpAddress is not null)
        {
            mapped.Add((ipAddressColumn, log.IpAddress));
        }

        var createdAtColumn = PickColumn(columns, "CreatedAtUtc", "CreatedAt");
        if (createdAtColumn is not null)
        {
            mapped.Add((createdAtColumn, log.CreatedAtUtc));
        }

        if (mapped.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var columnList = string.Join(", ", mapped.Select(x => $"[{x.Column}]"));
        var parameterList = new List<string>();

        for (var i = 0; i < mapped.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            parameter.Value = mapped[i].Value;
            command.Parameters.Add(parameter);
            parameterList.Add(parameter.ParameterName);
        }

        command.CommandText = $"INSERT INTO [{tableSchema}].[ApiLogs] ({columnList}) VALUES ({string.Join(", ", parameterList)})";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or <= 0 || request.Body.CanRead == false)
        {
            return null;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static async Task<string?> ReadResponseBodyAsync(HttpResponse response)
    {
        if (!response.Body.CanRead)
        {
            return null;
        }

        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static int? TryParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static async Task<string?> GetTableSchemaAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) TABLE_SCHEMA
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @tableName
            ORDER BY CASE WHEN TABLE_SCHEMA = 'dbo' THEN 0 ELSE 1 END
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var schema = await command.ExecuteScalarAsync(cancellationToken);
        return schema?.ToString();
    }

    private static async Task<Dictionary<string, string>> GetColumnsWithTypesAsync(DbConnection connection, string tableName, string tableSchema, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
              AND TABLE_SCHEMA = @tableSchema
            """;

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@tableSchema";
        schemaParameter.Value = tableSchema;
        command.Parameters.Add(schemaParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }

    private static string? PickColumn(IReadOnlyDictionary<string, string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsSchemaMismatch(Exception ex)
    {
        var message = GetInnermostMessage(ex);
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not be bound", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInnermostMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
