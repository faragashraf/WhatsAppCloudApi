using Serilog;
using System.Text;
// JWT authentication removed so incoming Facebook tokens can be forwarded to Graph API
using WhatsAppCloudApi.Api.Extensions;
using WhatsAppCloudApi.Api.Middleware;
using WhatsAppCloudApi.Application;
using WhatsAppCloudApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register IHttpContextAccessor so infrastructure handlers can access incoming request headers
builder.Services.AddHttpContextAccessor();

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices();
builder.Services.AddHealthChecks();

// No local JWT validation: accept incoming Bearer token and forward to Graph API. This lets clients send Facebook access tokens directly.
// CORS - allow local testing and Swagger access. In production, tighten this policy.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDev", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatsApp Cloud API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAllDev");
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("fixed");

app.Run();
