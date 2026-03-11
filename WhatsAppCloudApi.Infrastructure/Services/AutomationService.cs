using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class AutomationService : IAutomationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AutomationService> _logger;

    public AutomationService(ApplicationDbContext db, ILogger<AutomationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResponse<List<AutomationRule>>> GetRulesAsync(int companyId, CancellationToken ct)
    {
        var rules = await _db.AutomationRules
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        return ApiResponse<List<AutomationRule>>.Ok(rules);
    }

    public async Task<ApiResponse<AutomationRule>> GetRuleByIdAsync(int companyId, long ruleId, CancellationToken ct)
    {
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.AutomationRuleId == ruleId, ct);
        return rule is null
            ? ApiResponse<AutomationRule>.Fail("Rule not found", HttpStatusCode.NotFound)
            : ApiResponse<AutomationRule>.Ok(rule);
    }

    public async Task<ApiResponse<AutomationRule>> CreateRuleAsync(int companyId, AutomationRuleUpsertRequest request, CancellationToken ct)
    {
        var validation = ValidateAndNormalize(request);
        if (validation is not null)
            return validation;

        var rule = new AutomationRule
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            TriggerType = request.TriggerType,
            TriggerValue = request.TriggerValue ?? string.Empty,
            ResponseType = request.ResponseType,
            ResponseValue = request.ResponseValue ?? string.Empty,
            TemplateName = request.TemplateName,
            LanguageCode = request.LanguageCode,
            Priority = request.Priority,
            IsActive = request.IsActive,
        };

        _db.AutomationRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AutomationRule>.Ok(rule);
    }

    public async Task<ApiResponse<AutomationRule>> UpdateRuleAsync(int companyId, long ruleId, AutomationRuleUpsertRequest request, CancellationToken ct)
    {
        var validation = ValidateAndNormalize(request);
        if (validation is not null)
            return validation;

        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.AutomationRuleId == ruleId, ct);
        if (rule is null)
            return ApiResponse<AutomationRule>.Fail("Rule not found", HttpStatusCode.NotFound);

        rule.Name = request.Name;
        rule.Description = request.Description;
        rule.TriggerType = request.TriggerType;
        rule.TriggerValue = request.TriggerValue ?? string.Empty;
        rule.ResponseType = request.ResponseType;
        rule.ResponseValue = request.ResponseValue ?? string.Empty;
        rule.TemplateName = request.TemplateName;
        rule.LanguageCode = request.LanguageCode;
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<AutomationRule>.Ok(rule);
    }

    public async Task<ApiResponse<bool>> DeleteRuleAsync(int companyId, long ruleId, CancellationToken ct)
    {
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.AutomationRuleId == ruleId, ct);
        if (rule is null)
            return ApiResponse<bool>.Fail("Rule not found", HttpStatusCode.NotFound);

        _db.AutomationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> ToggleRuleAsync(int companyId, long ruleId, CancellationToken ct)
    {
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.AutomationRuleId == ruleId, ct);
        if (rule is null)
            return ApiResponse<bool>.Fail("Rule not found", HttpStatusCode.NotFound);

        rule.IsActive = !rule.IsActive;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(rule.IsActive);
    }

    public async Task<AutomationRule?> FindMatchingRuleAsync(int companyId, string incomingText, CancellationToken ct)
    {
        var rules = await _db.AutomationRules
            .Where(r => r.CompanyId == companyId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        var textLower = (incomingText ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var rule in rules)
        {
            var matched = rule.TriggerType switch
            {
                "any" => true,
                "exact" => textLower.Equals(rule.TriggerValue.ToLowerInvariant()),
                "keyword" => textLower.Contains(rule.TriggerValue.ToLowerInvariant()),
                "contains" => textLower.Contains(rule.TriggerValue.ToLowerInvariant()),
                "regex" => TryRegexMatch(textLower, rule.TriggerValue),
                _ => false,
            };

            if (matched)
            {
                rule.TriggerCount++;
                await _db.SaveChangesAsync(ct);
                return rule;
            }
        }

        return null;
    }

    private static bool TryRegexMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            return false;
        }
    }

    private static ApiResponse<AutomationRule>? ValidateAndNormalize(AutomationRuleUpsertRequest request)
    {
        request.Name = request.Name?.Trim() ?? string.Empty;
        request.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        request.TriggerType = request.TriggerType?.Trim().ToLowerInvariant() ?? string.Empty;
        request.TriggerValue = request.TriggerValue?.Trim() ?? string.Empty;
        request.ResponseType = request.ResponseType?.Trim().ToLowerInvariant() ?? string.Empty;
        request.ResponseValue = request.ResponseValue?.Trim() ?? string.Empty;
        request.TemplateName = string.IsNullOrWhiteSpace(request.TemplateName) ? null : request.TemplateName.Trim();
        request.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? null : request.LanguageCode.Trim();

        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<AutomationRule>.Fail("Rule name is required.", HttpStatusCode.BadRequest);

        if (request.TriggerType == "any")
        {
            request.TriggerValue = "*";
        }

        if (request.TriggerType != "any" && string.IsNullOrWhiteSpace(request.TriggerValue))
            return ApiResponse<AutomationRule>.Fail("Trigger value is required.", HttpStatusCode.BadRequest);

        if (request.TriggerType is not ("any" or "keyword" or "contains" or "exact" or "regex"))
            return ApiResponse<AutomationRule>.Fail("Unsupported trigger type.", HttpStatusCode.BadRequest);

        if (request.ResponseType is not ("text" or "template"))
            return ApiResponse<AutomationRule>.Fail("Unsupported response type.", HttpStatusCode.BadRequest);

        if (request.ResponseType == "text" && string.IsNullOrWhiteSpace(request.ResponseValue))
            return ApiResponse<AutomationRule>.Fail("Response value is required for text responses.", HttpStatusCode.BadRequest);

        if (request.ResponseType == "template")
        {
            if (string.IsNullOrWhiteSpace(request.TemplateName))
                return ApiResponse<AutomationRule>.Fail("Template name is required for template responses.", HttpStatusCode.BadRequest);

            request.LanguageCode ??= "en_US";

            if (string.IsNullOrWhiteSpace(request.ResponseValue))
                request.ResponseValue = $"Template: {request.TemplateName}";
        }
        else
        {
            request.TemplateName = null;
            request.LanguageCode = null;
        }

        return null;
    }
}
