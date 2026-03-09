using System.Text.Json;

var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var info = new
{
    BaseUrl = "https://localhost:7118/api",
    Version = "1.0",
    AuthType = "Bearer JWT",
    CompanyId = 1,
    Endpoints = new[]
    {
        new { Method = "GET", Path = "/test", Description = "Test endpoint" }
    },
    CodeSamples = new object[]
    {
        new { Language = "cURL", Code = "curl http://test" }
    }
};

// Simulate ApiResponse<object>.Ok(info) — Data is typed as object?
var wrapper = new { Success = true, Message = (string?)null, Data = (object)info, Error = (object?)null, CorrelationId = (string?)null };

var json = JsonSerializer.Serialize(wrapper, options);
Console.WriteLine("=== Serialized JSON ===");
Console.WriteLine(json);
Console.WriteLine();

using var doc = JsonDocument.Parse(json);
var root = doc.RootElement;
var data = root.GetProperty("data");
Console.WriteLine("=== Property names inside 'data' ===");
foreach (var prop in data.EnumerateObject())
{
    Console.WriteLine($"  {prop.Name}: {prop.Value.ValueKind}");
    if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
    {
        var first = prop.Value[0];
        Console.WriteLine($"    First element properties:");
        foreach (var inner in first.EnumerateObject())
        {
            Console.WriteLine($"      {inner.Name}: {inner.Value}");
        }
    }
}
