using System.Globalization;
using System.Net.Mail;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class ConversationFlowService : IConversationFlowService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "any_message", "keyword", "contains", "exact"
    };

    private static readonly HashSet<string> AllowedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "start", "message", "menu", "capture_text", "meta_flow", "assign_agent", "external_link", "end"
    };

    private static readonly HashSet<string> AllowedFormInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "name", "full_name", "phone", "email", "boolean", "yes_no", "whatsapp_support"
    };

    private static readonly string[] LeadNameFieldKeys =
    [
        "full_name", "name", "customer_name", "contact_name", "client_name"
    ];

    private static readonly string[] LeadPhoneFieldKeys =
    [
        "phone", "phone_number", "mobile", "mobile_number", "contact_number", "customer_phone", "whatsapp_number"
    ];

    private static readonly string[] LeadEmailFieldKeys =
    [
        "email", "email_address", "mail"
    ];

    private static readonly string[] LeadWhatsAppSupportKeys =
    [
        "whatsapp_support", "supports_whatsapp", "has_whatsapp", "is_whatsapp", "whatsapp_available"
    ];

    private static readonly string[] LeadDepartmentKeys =
    [
        "department",
        "department_id",
        "department_key",
        "department_label",
        "department_name",
        "department_name_en",
        "department_name_ar",
        "dept",
        "dept_id",
        "lead_department",
        "lead_department_id",
        "section"
    ];

    private static readonly Regex VariablePattern = new("{{\\s*([a-zA-Z0-9_]+)\\s*}}", RegexOptions.Compiled);
    private static readonly Regex FullNameInputPattern = new(@"^[\p{L}\p{M}][\p{L}\p{M}\s'\-\.]{1,98}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex E164InputPattern = new(@"^\+?[1-9]\d{7,14}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DepartmentKeySanitizer = new("[^a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly TimeSpan WaitingInputSessionTimeout = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ConversationFlowService> _logger;
    private readonly ITenantWhatsAppConfigService _configService;
    private readonly IMessageDispatchService _messageDispatchService;
    private readonly IRoutingService _routingService;

    private sealed class FlowExecutionContext
    {
        public bool IsDryRun { get; init; }
        public string? InboundMetaMessageId { get; init; }
        public List<ConversationFlowRuntimeAction> Actions { get; } = [];
    }

    public ConversationFlowService(
        ApplicationDbContext db,
        ILogger<ConversationFlowService> logger,
        ITenantWhatsAppConfigService configService,
        IMessageDispatchService messageDispatchService,
        IRoutingService routingService)
    {
        _db = db;
        _logger = logger;
        _configService = configService;
        _messageDispatchService = messageDispatchService;
        _routingService = routingService;
    }

    public async Task<ApiResponse<List<ConversationFlowDto>>> GetFlowsAsync(int companyId, CancellationToken ct = default)
    {
        var flows = await _db.ConversationFlows
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(ct);

        var activeCounts = await _db.ConversationFlowSessions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && (x.Status == "ACTIVE" || x.Status == "WAITING_INPUT"))
            .GroupBy(x => x.ConversationFlowId)
            .Select(g => new { FlowId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FlowId, x => x.Count, ct);

        return ApiResponse<List<ConversationFlowDto>>.Ok(flows.Select(flow => ToDto(flow, activeCounts)).ToList());
    }

    public async Task<ApiResponse<ConversationFlowDto>> GetFlowByIdAsync(int companyId, long flowId, CancellationToken ct = default)
    {
        var flow = await _db.ConversationFlows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConversationFlowId == flowId, ct);

        if (flow is null)
        {
            return ApiResponse<ConversationFlowDto>.Fail("Flow not found.", HttpStatusCode.NotFound);
        }

        var activeCount = await _db.ConversationFlowSessions
            .AsNoTracking()
            .CountAsync(x => x.CompanyId == companyId
                && x.ConversationFlowId == flowId
                && (x.Status == "ACTIVE" || x.Status == "WAITING_INPUT"), ct);

        return ApiResponse<ConversationFlowDto>.Ok(ToDto(flow, new Dictionary<long, int> { [flowId] = activeCount }));
    }

    public async Task<ApiResponse<ConversationFlowDto>> CreateFlowAsync(int companyId, ConversationFlowUpsertRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateRequestAsync(companyId, request, null, ct);
        if (validation is not null)
        {
            return validation;
        }

        var flow = new ConversationFlow
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            Description = NormalizeNullable(request.Description),
            EntryTriggerType = request.EntryTriggerType.Trim().ToLowerInvariant(),
            EntryTriggerValue = NormalizeTriggerValue(request.EntryTriggerValue),
            DraftDefinitionJson = JsonSerializer.Serialize(request.Definition, JsonOpts),
            DraftVersion = 1,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ConversationFlows.Add(flow);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ConversationFlowDto>.Ok(ToDto(flow, new Dictionary<long, int>()));
    }

    public async Task<ApiResponse<ConversationFlowDto>> UpdateFlowAsync(int companyId, long flowId, ConversationFlowUpsertRequest request, CancellationToken ct = default)
    {
        var flow = await _db.ConversationFlows.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConversationFlowId == flowId, ct);
        if (flow is null)
        {
            return ApiResponse<ConversationFlowDto>.Fail("Flow not found.", HttpStatusCode.NotFound);
        }

        var validation = await ValidateRequestAsync(companyId, request, flowId, ct);
        if (validation is not null)
        {
            return validation;
        }

        flow.Name = request.Name.Trim();
        flow.Description = NormalizeNullable(request.Description);
        flow.EntryTriggerType = request.EntryTriggerType.Trim().ToLowerInvariant();
        flow.EntryTriggerValue = NormalizeTriggerValue(request.EntryTriggerValue);
        flow.DraftDefinitionJson = JsonSerializer.Serialize(request.Definition, JsonOpts);
        flow.DraftVersion = Math.Max(1, flow.DraftVersion + 1);
        flow.IsActive = request.IsActive;
        flow.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ConversationFlowDto>.Ok(ToDto(flow, new Dictionary<long, int>()));
    }

    public async Task<ApiResponse<ConversationFlowDto>> PublishFlowAsync(int companyId, long flowId, CancellationToken ct = default)
    {
        var flow = await _db.ConversationFlows.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConversationFlowId == flowId, ct);
        if (flow is null)
        {
            return ApiResponse<ConversationFlowDto>.Fail("Flow not found.", HttpStatusCode.NotFound);
        }

        var definition = DeserializeGraph(flow.DraftDefinitionJson);
        if (definition is null)
        {
            return ApiResponse<ConversationFlowDto>.Fail("Draft definition is invalid.", HttpStatusCode.BadRequest);
        }

        var validationError = ValidateGraph(definition);
        if (validationError is not null)
        {
            return ApiResponse<ConversationFlowDto>.Fail(validationError, HttpStatusCode.BadRequest);
        }

        flow.PublishedDefinitionJson = flow.DraftDefinitionJson;
        flow.PublishedVersion = flow.DraftVersion;
        flow.IsPublished = true;
        flow.PublishedAtUtc = DateTime.UtcNow;
        flow.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ConversationFlowDto>.Ok(ToDto(flow, new Dictionary<long, int>()), "Flow published.");
    }

    public async Task<ApiResponse<bool>> DeleteFlowAsync(int companyId, long flowId, CancellationToken ct = default)
    {
        var flow = await _db.ConversationFlows.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConversationFlowId == flowId, ct);
        if (flow is null)
        {
            return ApiResponse<bool>.Fail("Flow not found.", HttpStatusCode.NotFound);
        }

        _db.ConversationFlows.Remove(flow);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> ToggleFlowAsync(int companyId, long flowId, CancellationToken ct = default)
    {
        var flow = await _db.ConversationFlows.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConversationFlowId == flowId, ct);
        if (flow is null)
        {
            return ApiResponse<bool>.Fail("Flow not found.", HttpStatusCode.NotFound);
        }

        var now = DateTime.UtcNow;
        flow.IsActive = !flow.IsActive;
        flow.UpdatedAtUtc = now;

        if (!flow.IsActive)
        {
            // When a flow is paused, close all running sessions so old WAITING_INPUT states
            // do not consume the next inbound message before trigger matching.
            var runningSessions = await _db.ConversationFlowSessions
                .Where(x => x.CompanyId == companyId
                    && x.ConversationFlowId == flowId
                    && (x.Status == "ACTIVE" || x.Status == "WAITING_INPUT"))
                .ToListAsync(ct);

            foreach (var session in runningSessions)
            {
                session.Status = "COMPLETED";
                session.CompletedAtUtc ??= now;
                session.LastInteractionAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(flow.IsActive);
    }

    public async Task<ConversationFlowRuntimeResult> TryProcessInboundAsync(int companyId, Conversation conversation, Contact contact, FlowInboundMessage inbound, CancellationToken ct = default)
    {
        var activeSession = await _db.ConversationFlowSessions
            .Include(x => x.ConversationFlow)
            .Where(x => x.CompanyId == companyId
                && x.ConversationId == conversation.ConversationId
                && (x.Status == "ACTIVE" || x.Status == "WAITING_INPUT"))
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (activeSession is not null)
        {
            if (await ShouldAutoCloseSessionBeforeContinuationAsync(activeSession, ct))
            {
                var now = DateTime.UtcNow;
                activeSession.Status = "COMPLETED";
                activeSession.CompletedAtUtc = now;
                activeSession.LastInteractionAtUtc = now;
                await _db.SaveChangesAsync(ct);
                activeSession = null;
            }
        }

        if (activeSession is not null && ShouldRestartMetaFlowSessionOnTrigger(activeSession, inbound))
        {
            var now = DateTime.UtcNow;
            activeSession.Status = "COMPLETED";
            activeSession.CompletedAtUtc = now;
            activeSession.LastInteractionAtUtc = now;
            await _db.SaveChangesAsync(ct);
            activeSession = null;
        }

        if (activeSession is not null)
        {
            var continuation = await ContinueSessionAsync(activeSession, conversation, contact, inbound, null, ct);
            if (continuation.Handled)
            {
                return continuation;
            }
        }

        var candidateFlows = await _db.ConversationFlows
            .Where(x => x.CompanyId == companyId && x.IsActive && x.IsPublished && x.PublishedDefinitionJson != null)
            .ToListAsync(ct);

        var flow = candidateFlows
            .OrderBy(x => GetTriggerWeight(x.EntryTriggerType))
            .ThenBy(x => x.ConversationFlowId)
            .FirstOrDefault(x => MatchesTrigger(x, inbound));

        if (flow is null || string.IsNullOrWhiteSpace(flow.PublishedDefinitionJson))
        {
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        var graph = DeserializeGraph(flow.PublishedDefinitionJson);
        if (graph is null)
        {
            _logger.LogWarning("Skipping flow {FlowId} because the published definition could not be deserialized.", flow.ConversationFlowId);
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        return await StartFlowAsync(flow, conversation, contact, inbound, graph, flow.PublishedVersion ?? flow.DraftVersion, incrementTriggerCount: true, ct: ct);
    }

    public async Task<ConversationFlowRuntimeResult> SimulateFlowAsync(
        int companyId,
        long flowId,
        Conversation conversation,
        Contact contact,
        FlowInboundMessage inbound,
        bool usePublishedVersion,
        CancellationToken ct = default)
    {
        var activeSession = await _db.ConversationFlowSessions
            .Include(x => x.ConversationFlow)
            .Where(x => x.CompanyId == companyId
                && x.ConversationId == conversation.ConversationId
                && x.ConversationFlowId == flowId
                && (x.Status == "ACTIVE" || x.Status == "WAITING_INPUT"))
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        var flow = activeSession?.ConversationFlow
            ?? await _db.ConversationFlows.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConversationFlowId == flowId, ct);

        if (flow is null)
        {
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        var graph = GetGraphForExecution(flow, usePublishedVersion);
        if (graph is null)
        {
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        var flowVersion = usePublishedVersion && flow.PublishedVersion.HasValue
            ? flow.PublishedVersion.Value
            : flow.DraftVersion;

        if (activeSession is not null)
        {
            activeSession.FlowVersion = flowVersion;
            return await ContinueSessionAsync(activeSession, conversation, contact, inbound, graph, ct);
        }

        return await StartFlowAsync(flow, conversation, contact, inbound, graph, flowVersion, incrementTriggerCount: false, ct: ct);
    }

    private async Task<ConversationFlowRuntimeResult> ContinueSessionAsync(
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        FlowInboundMessage inbound,
        ConversationFlowGraphDto? graphOverride,
        CancellationToken ct)
    {
        var flow = session.ConversationFlow;
        var requiresPublishedFlow = graphOverride is null;
        if (flow is null
            || (graphOverride is null && !flow.IsActive)
            || (requiresPublishedFlow && (!flow.IsPublished || string.IsNullOrWhiteSpace(flow.PublishedDefinitionJson))))
        {
            session.Status = "COMPLETED";
            session.CompletedAtUtc = DateTime.UtcNow;
            session.LastInteractionAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        var graph = graphOverride ?? DeserializeGraph(flow.PublishedDefinitionJson);
        if (graph is null)
        {
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        var executionContext = new FlowExecutionContext
        {
            IsDryRun = inbound.IsDryRun,
            InboundMetaMessageId = NormalizeNullable(inbound.MetaMessageId)
        };

        return await ExecuteGraphAsync(flow, session, graph, conversation, contact, inbound, executionContext, ct, startedNewSession: false);
    }

    private async Task<ConversationFlowRuntimeResult> StartFlowAsync(
        ConversationFlow flow,
        Conversation conversation,
        Contact contact,
        FlowInboundMessage inbound,
        ConversationFlowGraphDto graph,
        int flowVersion,
        bool incrementTriggerCount,
        CancellationToken ct)
    {
        var startNode = graph.Nodes.FirstOrDefault(x => string.Equals(x.Type, "start", StringComparison.OrdinalIgnoreCase));
        if (startNode is null)
        {
            _logger.LogWarning("Skipping flow {FlowId} because it has no start node.", flow.ConversationFlowId);
            return new ConversationFlowRuntimeResult { Handled = false };
        }

        if (incrementTriggerCount)
        {
            flow.TriggerCount += 1;
        }

        var session = new ConversationFlowSession
        {
            CompanyId = conversation.CompanyId,
            ConversationFlowId = flow.ConversationFlowId,
            ConversationId = conversation.ConversationId,
            ContactId = contact.ContactId,
            FlowVersion = flowVersion,
            CurrentNodeId = startNode.Id,
            VariablesJson = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), JsonOpts),
            Status = "ACTIVE",
            StartedAtUtc = DateTime.UtcNow,
            LastInteractionAtUtc = DateTime.UtcNow
        };

        _db.ConversationFlowSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, startNode.Id, "session_started", "system", "Flow session started.", null, ct);

        var executionContext = new FlowExecutionContext
        {
            IsDryRun = inbound.IsDryRun,
            InboundMetaMessageId = NormalizeNullable(inbound.MetaMessageId)
        };

        return await ExecuteGraphAsync(flow, session, graph, conversation, contact, inbound, executionContext, ct, startedNewSession: true);
    }

    private async Task<ConversationFlowRuntimeResult> ExecuteGraphAsync(
        ConversationFlow flow,
        ConversationFlowSession session,
        ConversationFlowGraphDto graph,
        Conversation conversation,
        Contact contact,
        FlowInboundMessage inbound,
        FlowExecutionContext executionContext,
        CancellationToken ct,
        bool startedNewSession)
    {
        var nodes = graph.Nodes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var edges = graph.Edges.ToLookup(x => x.SourceNodeId, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var waitingForInput = string.Equals(session.Status, "WAITING_INPUT", StringComparison.OrdinalIgnoreCase);
        var currentNodeId = session.CurrentNodeId;

        for (var guard = 0; guard < 24; guard++)
        {
            if (!nodes.TryGetValue(currentNodeId, out var node))
            {
                await CompleteSessionAsync(session, "COMPLETED", ct);
                return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
            }

            var variables = LoadVariables(session, conversation, contact);
            var nodeType = NormalizeKey(node.Type);

            switch (nodeType)
            {
                case "start":
                {
                    var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                    if (nextNodeId is null)
                    {
                        await CompleteSessionAsync(session, "COMPLETED", ct);
                        return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                    }

                    session.CurrentNodeId = nextNodeId;
                    session.Status = "ACTIVE";
                    session.LastInteractionAtUtc = now;
                    currentNodeId = nextNodeId;
                    continue;
                }
                case "message":
                {
                    var bodyText = Render(node.BodyText, variables);
                    if (!string.IsNullOrWhiteSpace(bodyText))
                    {
                        await SendTextNodeAsync(flow, session, conversation, contact, node.Id, bodyText, executionContext, ct);
                    }

                    var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                    if (nextNodeId is null)
                    {
                        await CompleteSessionAsync(session, "COMPLETED", ct);
                        return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                    }

                    session.CurrentNodeId = nextNodeId;
                    session.Status = "ACTIVE";
                    session.LastInteractionAtUtc = now;
                    currentNodeId = nextNodeId;
                    waitingForInput = false;
                    continue;
                }
                case "menu":
                {
                    if (waitingForInput)
                    {
                        var selected = TryResolveMenuSelection(node, inbound);
                        if (selected is null)
                        {
                            session.InvalidReplyCount += 1;
                            session.Status = "WAITING_INPUT";
                            session.LastInteractionAtUtc = now;

                            var invalidMessage = Render(
                                string.IsNullOrWhiteSpace(node.InvalidInputMessage)
                                    ? "Please choose one of the available options."
                                    : node.InvalidInputMessage,
                                variables);

                            await SendTextNodeAsync(flow, session, conversation, contact, node.Id, invalidMessage, executionContext, ct);
                            await _db.SaveChangesAsync(ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        if (!string.IsNullOrWhiteSpace(node.VariableName))
                        {
                            var variableName = node.VariableName.Trim();
                            variables[variableName] = selected.Value.Label;
                            variables[$"{variableName}_id"] = selected.Value.Id;

                            // Keep a normalized label alias so templates can use {{variable_label}} consistently.
                            if (!variableName.EndsWith("_label", StringComparison.OrdinalIgnoreCase))
                            {
                                variables[$"{variableName}_label"] = selected.Value.Label;
                            }

                            session.VariablesJson = JsonSerializer.Serialize(variables, JsonOpts);
                        }

                        session.InvalidReplyCount = 0;
                        session.Status = "ACTIVE";
                        session.LastInteractionAtUtc = now;
                        await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, node.Id, "selection_received", "inbound", selected.Value.Label, JsonSerializer.Serialize(new { selected.Value.Id, selected.Value.Label }, JsonOpts), ct);

                        var nextNodeId = ResolveNextNodeId(edges[node.Id], selected.Value.Id);
                        if (nextNodeId is null)
                        {
                            await CompleteSessionAsync(session, "COMPLETED", ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        session.CurrentNodeId = nextNodeId;
                        currentNodeId = nextNodeId;
                        waitingForInput = false;
                        continue;
                    }

                    await SendMenuNodeAsync(flow, session, conversation, contact, node, variables, executionContext, ct);
                    session.CurrentNodeId = node.Id;
                    session.Status = "WAITING_INPUT";
                    session.LastInteractionAtUtc = now;
                    await _db.SaveChangesAsync(ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
                case "capture_text":
                {
                    if (waitingForInput)
                    {
                        var text = (inbound.Text ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            session.InvalidReplyCount += 1;
                            session.Status = "WAITING_INPUT";
                            session.LastInteractionAtUtc = now;
                            var invalidMessage = Render(
                                string.IsNullOrWhiteSpace(node.InvalidInputMessage)
                                    ? "Please reply with the requested text."
                                    : node.InvalidInputMessage,
                                variables);
                            await SendTextNodeAsync(flow, session, conversation, contact, node.Id, invalidMessage, executionContext, ct);
                            await _db.SaveChangesAsync(ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        if (!string.IsNullOrWhiteSpace(node.VariableName))
                        {
                            variables[node.VariableName] = text;
                            session.VariablesJson = JsonSerializer.Serialize(variables, JsonOpts);
                        }

                        session.InvalidReplyCount = 0;
                        session.Status = "ACTIVE";
                        session.LastInteractionAtUtc = now;
                        await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, node.Id, "text_captured", "inbound", text, null, ct);

                        var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                        if (nextNodeId is null)
                        {
                            await CompleteSessionAsync(session, "COMPLETED", ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        session.CurrentNodeId = nextNodeId;
                        currentNodeId = nextNodeId;
                        waitingForInput = false;
                        continue;
                    }

                    var prompt = Render(node.BodyText, variables);
                    if (!string.IsNullOrWhiteSpace(prompt))
                    {
                        await SendTextNodeAsync(flow, session, conversation, contact, node.Id, prompt, executionContext, ct);
                    }

                    session.CurrentNodeId = node.Id;
                    session.Status = "WAITING_INPUT";
                    session.LastInteractionAtUtc = now;
                    await _db.SaveChangesAsync(ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
                case "form":
                {
                    var fields = node.Options
                        .Where(option => !string.IsNullOrWhiteSpace(option.Id) && !string.IsNullOrWhiteSpace(option.Label))
                        .ToList();

                    if (fields.Count == 0)
                    {
                        await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, node.Id, "form_skipped", "system", "Form has no fields configured.", null, ct);

                        var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                        if (nextNodeId is null)
                        {
                            await CompleteSessionAsync(session, "COMPLETED", ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        session.CurrentNodeId = nextNodeId;
                        session.Status = "ACTIVE";
                        session.LastInteractionAtUtc = now;
                        currentNodeId = nextNodeId;
                        waitingForInput = false;
                        continue;
                    }

                    var formStepKey = BuildFormStepKey(node.Id);
                    if (waitingForInput)
                    {
                        var fieldIndex = ResolveFormFieldIndex(variables, formStepKey, fields.Count);
                        var field = fields[fieldIndex];
                        var inputValue = ResolveInboundText(inbound);
                        if (string.IsNullOrWhiteSpace(inputValue))
                        {
                            session.InvalidReplyCount += 1;
                            session.Status = "WAITING_INPUT";
                            session.LastInteractionAtUtc = now;

                            var invalidMessage = Render(
                                BuildFieldValidationMessage(
                                    field,
                                    node,
                                    "Please reply with the requested information."),
                                variables);

                            await SendTextNodeAsync(flow, session, conversation, contact, node.Id, invalidMessage, executionContext, ct);
                            await _db.SaveChangesAsync(ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        var normalizedInputValue = NormalizeAndValidateFieldInput(field, inputValue!, out var validationFailed);
                        if (validationFailed)
                        {
                            session.InvalidReplyCount += 1;
                            session.Status = "WAITING_INPUT";
                            session.LastInteractionAtUtc = now;

                            var invalidMessage = Render(
                                BuildFieldValidationMessage(
                                    field,
                                    node,
                                    "The value format is invalid. Please try again."),
                                variables);

                            await SendTextNodeAsync(flow, session, conversation, contact, node.Id, invalidMessage, executionContext, ct);
                            await _db.SaveChangesAsync(ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        var variableName = NormalizeFormVariableName(field.Id, fieldIndex);
                        variables[variableName] = normalizedInputValue;
                        variables.Remove(formStepKey);

                        await AddLogAsync(
                            flow.ConversationFlowId,
                            session,
                            conversation,
                            contact,
                            node.Id,
                            "form_field_captured",
                            "inbound",
                            normalizedInputValue,
                            JsonSerializer.Serialize(new { variableName, fieldIndex }, JsonOpts),
                            ct);

                        fieldIndex += 1;
                        if (fieldIndex >= fields.Count)
                        {
                            if (!executionContext.IsDryRun)
                            {
                                var submittedValues = BuildFormSubmissionValues(fields, variables);
                                await AddFormSubmissionAsync(
                                    flow,
                                    session,
                                    conversation,
                                    contact,
                                    node.Id,
                                    source: "form",
                                    inboundMessageType: inbound.MessageType,
                                    metaMessageId: inbound.MetaMessageId,
                                    payloadJson: JsonSerializer.Serialize(submittedValues, JsonOpts),
                                    extractedValues: submittedValues,
                                    ct);
                            }

                            session.VariablesJson = JsonSerializer.Serialize(variables, JsonOpts);
                            session.InvalidReplyCount = 0;
                            session.Status = "ACTIVE";
                            session.LastInteractionAtUtc = now;

                            var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                            if (nextNodeId is null)
                            {
                                await CompleteSessionAsync(session, "COMPLETED", ct);
                                return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                            }

                            session.CurrentNodeId = nextNodeId;
                            currentNodeId = nextNodeId;
                            waitingForInput = false;
                            continue;
                        }

                        variables[formStepKey] = fieldIndex.ToString(CultureInfo.InvariantCulture);
                        session.VariablesJson = JsonSerializer.Serialize(variables, JsonOpts);
                        session.InvalidReplyCount = 0;
                        session.Status = "WAITING_INPUT";
                        session.LastInteractionAtUtc = now;

                        var nextPrompt = BuildFormFieldPrompt(fields[fieldIndex], variables);
                        if (!string.IsNullOrWhiteSpace(nextPrompt))
                        {
                            await SendTextNodeAsync(flow, session, conversation, contact, node.Id, nextPrompt, executionContext, ct);
                        }

                        await _db.SaveChangesAsync(ct);
                        return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                    }

                    var introMessage = Render(node.BodyText, variables);
                    if (!string.IsNullOrWhiteSpace(introMessage))
                    {
                        await SendTextNodeAsync(flow, session, conversation, contact, node.Id, introMessage, executionContext, ct);
                    }

                    variables[formStepKey] = "0";
                    session.VariablesJson = JsonSerializer.Serialize(variables, JsonOpts);
                    session.CurrentNodeId = node.Id;
                    session.Status = "WAITING_INPUT";
                    session.LastInteractionAtUtc = now;

                    var firstPrompt = BuildFormFieldPrompt(fields[0], variables);
                    if (!string.IsNullOrWhiteSpace(firstPrompt))
                    {
                        await SendTextNodeAsync(flow, session, conversation, contact, node.Id, firstPrompt, executionContext, ct);
                    }

                    await _db.SaveChangesAsync(ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
                case "meta_flow":
                {
                    if (waitingForInput)
                    {
                        var structuredValues = ParseInboundStructuredValues(inbound);
                        var userStructuredValues = ExtractUserStructuredValues(structuredValues);
                        var hasTechnicalValues = structuredValues.Keys.Any(IsMetaFlowTechnicalField);
                        if (userStructuredValues.Count == 0 && !hasTechnicalValues)
                        {
                            session.InvalidReplyCount += 1;
                            session.Status = "WAITING_INPUT";
                            session.LastInteractionAtUtc = now;

                            var invalidMessage = Render(
                                string.IsNullOrWhiteSpace(node.InvalidInputMessage)
                                    ? "Please complete the flow form to continue."
                                    : node.InvalidInputMessage,
                                variables);

                            await SendTextNodeAsync(flow, session, conversation, contact, node.Id, invalidMessage, executionContext, ct);
                            await _db.SaveChangesAsync(ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        foreach (var entry in userStructuredValues)
                        {
                            variables[entry.Key] = entry.Value;
                        }

                        if (!string.IsNullOrWhiteSpace(node.VariableName))
                        {
                            variables[node.VariableName] = JsonSerializer.Serialize(userStructuredValues, JsonOpts);
                        }

                        session.VariablesJson = JsonSerializer.Serialize(variables, JsonOpts);
                        session.InvalidReplyCount = 0;
                        session.Status = "ACTIVE";
                        session.LastInteractionAtUtc = now;

                        if (!executionContext.IsDryRun)
                        {
                            await AddFormSubmissionAsync(
                                flow,
                                session,
                                conversation,
                                contact,
                                node.Id,
                                source: "meta_flow",
                                inboundMessageType: inbound.InteractiveType ?? inbound.MessageType,
                                metaMessageId: inbound.MetaMessageId,
                                payloadJson: inbound.StructuredDataJson,
                                extractedValues: structuredValues,
                                ct);
                        }

                        await AddLogAsync(
                            flow.ConversationFlowId,
                            session,
                            conversation,
                            contact,
                            node.Id,
                            "meta_flow_submitted",
                            "inbound",
                            JsonSerializer.Serialize(structuredValues, JsonOpts),
                            JsonSerializer.Serialize(new
                            {
                                interactiveType = inbound.InteractiveType,
                                userFieldCount = userStructuredValues.Count,
                                technicalFieldCount = Math.Max(0, structuredValues.Count - userStructuredValues.Count)
                            }, JsonOpts),
                            ct);

                        var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                        if (nextNodeId is null)
                        {
                            await CompleteSessionAsync(session, "COMPLETED", ct);
                            return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                        }

                        session.CurrentNodeId = nextNodeId;
                        currentNodeId = nextNodeId;
                        waitingForInput = false;
                        continue;
                    }

                    var metaFlowPayload = BuildMetaFlowPayload(conversation.ContactNumber, node, variables, session.ConversationFlowSessionId);
                    var preview = Render(node.BodyText, variables);
                    if (string.IsNullOrWhiteSpace(preview))
                    {
                        preview = $"Meta Flow: {node.MetaFlowId ?? node.MetaFlowName ?? node.Title}";
                    }

                    await QueueInteractiveAsync(flow, session, conversation, contact, node.Id, preview, metaFlowPayload, executionContext, ct);
                    session.CurrentNodeId = node.Id;
                    session.Status = "WAITING_INPUT";
                    session.LastInteractionAtUtc = now;
                    await _db.SaveChangesAsync(ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
                case "assign_agent":
                {
                    var handoffPreview = await ExecuteAssignmentNodeAsync(session, conversation, contact, node, executionContext, ct);
                    await AddLogAsync(
                        flow.ConversationFlowId,
                        session,
                        conversation,
                        contact,
                        node.Id,
                        executionContext.IsDryRun ? "handoff_simulated" : "handoff_completed",
                        "system",
                        handoffPreview,
                        null,
                        ct);
                    await CompleteSessionAsync(session, "HANDED_OFF", ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
                case "external_link":
                {
                    var message = BuildExternalLinkMessage(node, variables);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        await SendTextNodeAsync(flow, session, conversation, contact, node.Id, message, executionContext, ct);
                    }

                    var nextNodeId = ResolveNextNodeId(edges[node.Id], null);
                    if (nextNodeId is null)
                    {
                        await CompleteSessionAsync(session, "COMPLETED", ct);
                        return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                    }

                    session.CurrentNodeId = nextNodeId;
                    session.Status = "ACTIVE";
                    session.LastInteractionAtUtc = now;
                    currentNodeId = nextNodeId;
                    waitingForInput = false;
                    continue;
                }
                case "end":
                {
                    var finalMessage = Render(node.BodyText, variables);
                    if (!string.IsNullOrWhiteSpace(finalMessage))
                    {
                        await SendTextNodeAsync(flow, session, conversation, contact, node.Id, finalMessage, executionContext, ct);
                    }

                    await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, node.Id, "flow_completed", "system", node.Title, null, ct);
                    await CompleteSessionAsync(session, "COMPLETED", ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
                default:
                {
                    _logger.LogWarning("Unsupported flow node type {NodeType} in flow {FlowId}.", node.Type, flow.ConversationFlowId);
                    await CompleteSessionAsync(session, "COMPLETED", ct);
                    return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
                }
            }
        }

        await CompleteSessionAsync(session, "COMPLETED", ct);
        return BuildRuntimeResult(session, conversation, contact, startedNewSession, executionContext);
    }

    private async Task<string> ExecuteAssignmentNodeAsync(
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        ConversationFlowNodeDto node,
        FlowExecutionContext executionContext,
        CancellationToken ct)
    {
        var assignMode = NormalizeKey(node.AssignMode ?? "auto");
        var reason = string.IsNullOrWhiteSpace(node.AssignReason) ? "FLOW_HANDOFF" : node.AssignReason.Trim().ToUpperInvariant();
        var preview = assignMode switch
        {
            "specific" => $"Assign conversation to user #{node.AssignToUserId ?? 0}",
            "specific_team" => $"Assign conversation to team #{node.AssignToTeamId ?? 0}",
            "team_auto" => $"Auto assign within team #{node.AssignToTeamId ?? 0}",
            _ => "Auto assign conversation"
        };

        executionContext.Actions.Add(new ConversationFlowRuntimeAction
        {
            NodeId = node.Id,
            ActionType = "assign_agent",
            Preview = preview
        });

        if (executionContext.IsDryRun)
        {
            return preview;
        }

        if (assignMode == "specific")
        {
            if (!node.AssignToUserId.HasValue)
            {
                throw new InvalidOperationException("Assign-to-user node requires a target user.");
            }

            await _routingService.AssignConversationAsync(
                conversation.CompanyId,
                conversation,
                newAssignedTeamId: null,
                newAssignedUserId: node.AssignToUserId,
                changedByUserId: null,
                updateContactOwner: node.UpdateContactOwner,
                assignmentMode: "AUTO",
                reason: reason,
                notes: $"Flow session {session.ConversationFlowSessionId}",
                cancellationToken: ct);
            return preview;
        }

        if (assignMode == "specific_team")
        {
            if (!node.AssignToTeamId.HasValue)
            {
                throw new InvalidOperationException("Assign-to-team node requires a target team.");
            }

            await _routingService.AssignConversationAsync(
                conversation.CompanyId,
                conversation,
                newAssignedTeamId: node.AssignToTeamId,
                newAssignedUserId: null,
                changedByUserId: null,
                updateContactOwner: false,
                assignmentMode: "AUTO",
                reason: reason,
                notes: $"Flow session {session.ConversationFlowSessionId}",
                cancellationToken: ct);
            return preview;
        }

        if (assignMode == "team_auto")
        {
            if (!node.AssignToTeamId.HasValue)
            {
                throw new InvalidOperationException("Team auto-assign node requires a target team.");
            }

            await _routingService.AutoAssignConversationAsync(
                conversation.CompanyId,
                conversation,
                contact,
                reason,
                preferredTeamId: node.AssignToTeamId,
                cancellationToken: ct);
            return preview;
        }

        await _routingService.AutoAssignConversationAsync(
            conversation.CompanyId,
            conversation,
            contact,
            reason,
            preferredTeamId: null,
            cancellationToken: ct);
        return preview;
    }

    private async Task SendMenuNodeAsync(
        ConversationFlow flow,
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        ConversationFlowNodeDto node,
        Dictionary<string, string> variables,
        FlowExecutionContext executionContext,
        CancellationToken ct)
    {
        var presentation = NormalizeKey(node.MenuPresentation ?? "buttons");
        if (presentation == "list")
        {
            await QueueInteractiveAsync(
                flow,
                session,
                conversation,
                contact,
                node.Id,
                Render(node.BodyText, variables),
                BuildListPayload(conversation.ContactNumber, node, variables),
                executionContext,
                ct);
            return;
        }

        await QueueInteractiveAsync(
            flow,
            session,
            conversation,
            contact,
            node.Id,
            Render(node.BodyText, variables),
            BuildButtonsPayload(conversation.ContactNumber, node, variables),
            executionContext,
            ct);
    }

    private async Task SendTextNodeAsync(
        ConversationFlow flow,
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        string nodeId,
        string bodyText,
        FlowExecutionContext executionContext,
        CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = conversation.ContactNumber,
            type = "text",
            text = new { body = bodyText }
        };

        await QueueMessageAsync(flow, session, conversation, contact, nodeId, "TEXT", "text", bodyText, payload, executionContext, ct);
    }

    private async Task QueueInteractiveAsync(
        ConversationFlow flow,
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        string nodeId,
        string preview,
        object payload,
        FlowExecutionContext executionContext,
        CancellationToken ct)
    {
        await QueueMessageAsync(flow, session, conversation, contact, nodeId, "INTERACTIVE", "interactive", preview, payload, executionContext, ct);
    }

    private async Task QueueMessageAsync(
        ConversationFlow flow,
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        string nodeId,
        string messageType,
        string conversationMessageType,
        string preview,
        object payload,
        FlowExecutionContext executionContext,
        CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(payload, JsonOpts);
        var normalizedPreview = preview.Length > 1000 ? preview[..1000] : preview;
        var logMetadataJson = BuildOutboundLogMetadata(executionContext.InboundMetaMessageId, conversationMessageType);

        // Prevent duplicate automated replies when the same inbound WhatsApp message is redelivered.
        if (await HasOutboundAlreadyBeenLoggedAsync(session, nodeId, normalizedPreview, logMetadataJson, ct))
        {
            _logger.LogInformation(
                "Skipping duplicate flow message for session {SessionId}, node {NodeId}, inbound {MetaMessageId}.",
                session.ConversationFlowSessionId,
                nodeId,
                executionContext.InboundMetaMessageId);
            return;
        }

        executionContext.Actions.Add(new ConversationFlowRuntimeAction
        {
            NodeId = nodeId,
            ActionType = conversationMessageType,
            Preview = normalizedPreview,
            MetadataJson = executionContext.IsDryRun ? body : null
        });

        if (executionContext.IsDryRun)
        {
            await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, nodeId, "message_simulated", "outbound", normalizedPreview, logMetadataJson, ct);
            return;
        }

        var config = await ResolveConfigAsync(conversation.CompanyId, conversation.WhatsAppPhoneNumberId, ct);
        var now = DateTime.UtcNow;

        await _messageDispatchService.QueueLinkedMessageAsync(new QueueLinkedMessageRequest
        {
            CompanyId = conversation.CompanyId,
            WhatsAppPhoneNumberId = conversation.WhatsAppPhoneNumberId,
            ContactId = contact.ContactId,
            ConversationId = conversation.ConversationId,
            ToNumber = conversation.ContactNumber,
            MessageType = messageType,
            MessageBody = body,
            Source = "FLOW",
            ConversationMessageType = conversationMessageType,
            ConversationContent = normalizedPreview,
            ConversationMessageStatus = "sending",
            CreatedAtUtc = now,
            Payload = new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = body
            }
        }, ct);

        conversation.LastMessageContent = normalizedPreview;
        conversation.LastMessageType = conversationMessageType;
        conversation.LastMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;

        contact.LastSeenAtUtc = now;
        contact.LastOutboundMessageAtUtc = now;
        contact.UpdatedAtUtc = now;

        await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, nodeId, "message_queued", "outbound", normalizedPreview, logMetadataJson, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task AddLogAsync(long flowId, ConversationFlowSession session, Conversation conversation, Contact contact, string nodeId, string eventType, string direction, string? message, string? metadataJson, CancellationToken ct)
    {
        _db.ConversationFlowExecutionLogs.Add(new ConversationFlowExecutionLog
        {
            CompanyId = conversation.CompanyId,
            ConversationFlowId = flowId,
            ConversationFlowSessionId = session.ConversationFlowSessionId,
            ConversationId = conversation.ConversationId,
            ContactId = contact.ContactId,
            NodeId = nodeId,
            EventType = eventType,
            Direction = direction,
            Message = message,
            MetadataJson = metadataJson,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task AddFormSubmissionAsync(
        ConversationFlow flow,
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        string nodeId,
        string source,
        string? inboundMessageType,
        string? metaMessageId,
        string? payloadJson,
        IReadOnlyDictionary<string, string> extractedValues,
        CancellationToken ct)
    {
        var normalizedSource = NormalizeLeadSource(source);
        var normalizedNodeId = NormalizeNullable(nodeId) ?? string.Empty;
        var normalizedMetaMessageId = NormalizeNullable(metaMessageId);
        if (!string.IsNullOrWhiteSpace(normalizedMetaMessageId))
        {
            var isDuplicate = await IsDuplicateSubmissionAsync(
                conversation.CompanyId,
                flow.ConversationFlowId,
                normalizedNodeId,
                normalizedMetaMessageId,
                ct);
            if (isDuplicate)
            {
                return;
            }
        }

        var normalizedValues = BuildNormalizedSubmissionValues(extractedValues);
        var extractedValuesJson = JsonSerializer.Serialize(normalizedValues, JsonOpts);

        var submission = new ConversationFlowFormSubmission
        {
            CompanyId = conversation.CompanyId,
            ConversationFlowId = flow.ConversationFlowId,
            ConversationFlowSessionId = session.ConversationFlowSessionId,
            ConversationId = conversation.ConversationId,
            ContactId = contact.ContactId,
            NodeId = normalizedNodeId,
            Source = normalizedSource,
            InboundMessageType = NormalizeNullable(inboundMessageType),
            MetaMessageId = normalizedMetaMessageId,
            PayloadJson = NormalizeNullable(payloadJson),
            ExtractedValuesJson = extractedValuesJson,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ConversationFlowFormSubmissions.Add(submission);

        if (normalizedValues.Count == 0)
        {
            return;
        }

        MergeDepartmentHintsFromSessionVariables(normalizedValues, session);
        submission.ExtractedValuesJson = JsonSerializer.Serialize(normalizedValues, JsonOpts);

        var leadDepartment = await ResolveLeadDepartmentAsync(conversation.CompanyId, normalizedValues, ct);
        var now = DateTime.UtcNow;

        MergeSubmissionValuesIntoContact(contact, conversation, normalizedValues, leadDepartment, normalizedSource, now);

        var leadValues = BuildLeadValueMap(normalizedValues, leadDepartment);
        _db.LeadRecords.Add(new LeadRecord
        {
            CompanyId = conversation.CompanyId,
            ConversationFlowFormSubmission = submission,
            ConversationFlowId = flow.ConversationFlowId,
            ConversationFlowSessionId = session.ConversationFlowSessionId,
            ConversationId = conversation.ConversationId,
            ContactId = contact.ContactId,
            LeadDepartmentId = leadDepartment?.LeadDepartmentId,
            Source = normalizedSource,
            Status = "NEW",
            ExtractedValuesJson = JsonSerializer.Serialize(leadValues, JsonOpts),
            CreatedAtUtc = now
        });

        if (leadDepartment?.RoutingTeamId is int routingTeamId)
        {
            await RouteLeadToDepartmentTeamAsync(conversation, contact, routingTeamId, ct);
        }
    }

    private async Task<bool> IsDuplicateSubmissionAsync(
        int companyId,
        long flowId,
        string nodeId,
        string metaMessageId,
        CancellationToken ct)
    {
        var existsInDatabase = await _db.ConversationFlowFormSubmissions
            .AsNoTracking()
            .AnyAsync(x =>
                x.CompanyId == companyId
                && x.ConversationFlowId == flowId
                && x.NodeId == nodeId
                && x.MetaMessageId == metaMessageId, ct);
        if (existsInDatabase)
        {
            return true;
        }

        return _db.ChangeTracker
            .Entries<ConversationFlowFormSubmission>()
            .Any(x =>
                x.State == EntityState.Added
                && x.Entity.CompanyId == companyId
                && x.Entity.ConversationFlowId == flowId
                && string.Equals(x.Entity.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Entity.MetaMessageId, metaMessageId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RouteLeadToDepartmentTeamAsync(
        Conversation conversation,
        Contact contact,
        int routingTeamId,
        CancellationToken ct)
    {
        if (conversation.AssignedUserId.HasValue)
        {
            return;
        }

        if (conversation.AssignedTeamId.HasValue && conversation.AssignedTeamId.Value != routingTeamId)
        {
            return;
        }

        var autoResult = await _routingService.AutoAssignConversationAsync(
            conversation.CompanyId,
            conversation,
            contact,
            reason: "LEAD_DEPARTMENT_AUTO_ASSIGN",
            preferredTeamId: routingTeamId,
            cancellationToken: ct);

        if (!autoResult.Changed && autoResult.NoAvailableAgent && conversation.AssignedTeamId != routingTeamId)
        {
            await _routingService.AssignConversationAsync(
                conversation.CompanyId,
                conversation,
                newAssignedTeamId: routingTeamId,
                newAssignedUserId: null,
                changedByUserId: null,
                updateContactOwner: false,
                assignmentMode: "AUTO",
                reason: "LEAD_DEPARTMENT_TEAM",
                notes: "Lead routed to department team without user because no eligible member was available.",
                cancellationToken: ct);
        }
    }

    private static Dictionary<string, string> BuildNormalizedSubmissionValues(IReadOnlyDictionary<string, string> extractedValues)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in extractedValues)
        {
            var key = NormalizeFormVariableName(entry.Key, 0);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (IsMetaFlowTechnicalField(key))
            {
                continue;
            }

            var value = NormalizeNullable(entry.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized[key] = value;
        }

        return normalized;
    }

    private static Dictionary<string, string> BuildLeadValueMap(
        IReadOnlyDictionary<string, string> normalizedValues,
        LeadDepartment? leadDepartment)
    {
        var leadValues = new Dictionary<string, string>(normalizedValues, StringComparer.OrdinalIgnoreCase);

        var fullName = FindFirstValue(normalizedValues, LeadNameFieldKeys);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            leadValues["full_name"] = fullName;
        }

        var email = FindFirstValue(normalizedValues, LeadEmailFieldKeys);
        if (!string.IsNullOrWhiteSpace(email))
        {
            leadValues["email"] = email.ToLowerInvariant();
        }

        var phone = FindFirstValue(normalizedValues, LeadPhoneFieldKeys);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            leadValues["phone"] = phone;
        }

        var whatsappSupport = FindFirstValue(normalizedValues, LeadWhatsAppSupportKeys);
        if (!string.IsNullOrWhiteSpace(whatsappSupport))
        {
            leadValues["whatsapp_support"] = TryNormalizeBooleanValue(whatsappSupport, out var normalizedWhatsAppSupport)
                ? normalizedWhatsAppSupport
                : whatsappSupport;
        }

        if (leadDepartment is not null)
        {
            leadValues["department_key"] = leadDepartment.DepartmentKey;
            leadValues["department_name_ar"] = leadDepartment.NameAr;
            leadValues["department_name_en"] = leadDepartment.NameEn;
        }

        return leadValues;
    }

    private static void MergeSubmissionValuesIntoContact(
        Contact contact,
        Conversation conversation,
        IReadOnlyDictionary<string, string> normalizedValues,
        LeadDepartment? leadDepartment,
        string source,
        DateTime now)
    {
        var changed = false;

        var fullName = FindFirstValue(normalizedValues, LeadNameFieldKeys);
        if (!string.IsNullOrWhiteSpace(fullName)
            && !string.Equals(contact.Name, fullName, StringComparison.Ordinal))
        {
            contact.Name = fullName;
            conversation.ContactName = fullName;
            changed = true;
        }

        var email = FindFirstValue(normalizedValues, LeadEmailFieldKeys);
        if (!string.IsNullOrWhiteSpace(email) && IsValidEmail(email))
        {
            var normalizedEmail = email.ToLowerInvariant();
            if (!string.Equals(contact.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                contact.Email = normalizedEmail;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(contact.Source))
        {
            contact.Source = source;
            changed = true;
        }

        var customFields = ParseCustomFieldDictionary(contact.CustomFields);
        var phone = FindFirstValue(normalizedValues, LeadPhoneFieldKeys);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            customFields["lead_full_name"] = fullName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            customFields["lead_email"] = email.ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            customFields["lead_phone_number"] = phone;
        }

        var whatsappSupport = FindFirstValue(normalizedValues, LeadWhatsAppSupportKeys);
        if (!string.IsNullOrWhiteSpace(whatsappSupport))
        {
            customFields["lead_whatsapp_support"] = TryNormalizeBooleanValue(whatsappSupport, out var normalizedWhatsAppSupport)
                ? normalizedWhatsAppSupport
                : whatsappSupport;
        }

        if (leadDepartment is not null)
        {
            customFields["lead_department_key"] = leadDepartment.DepartmentKey;
            customFields["lead_department_name_ar"] = leadDepartment.NameAr;
            customFields["lead_department_name_en"] = leadDepartment.NameEn;
        }

        customFields["lead_source"] = source;
        contact.CustomFields = JsonSerializer.Serialize(customFields, JsonOpts);
        changed = true;

        if (changed)
        {
            contact.UpdatedAtUtc = now;
        }
    }

    private static Dictionary<string, string> ParseCustomFieldDictionary(string? json)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.Object or JsonValueKind.Array => property.Value.GetRawText(),
                    _ => ConvertJsonValueToString(property.Value)
                };
            }
        }
        catch
        {
            // Ignore malformed contact custom fields and rebuild from normalized values.
        }

        return values;
    }

    private static string? FindFirstValue(IReadOnlyDictionary<string, string> values, IReadOnlyList<string> candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private async Task<LeadDepartment?> ResolveLeadDepartmentAsync(
        int companyId,
        IReadOnlyDictionary<string, string> normalizedValues,
        CancellationToken ct)
    {
        var candidates = BuildDepartmentCandidates(normalizedValues);
        if (candidates.Count == 0)
        {
            return null;
        }

        var departments = await _db.LeadDepartments
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LeadDepartmentId)
            .ToListAsync(ct);
        if (departments.Count == 0)
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
            {
                var byId = departments.FirstOrDefault(x => x.LeadDepartmentId == numericId);
                if (byId is not null)
                {
                    return byId;
                }
            }

            var normalizedCandidateKey = NormalizeDepartmentKey(candidate);
            if (!string.IsNullOrWhiteSpace(normalizedCandidateKey))
            {
                var byKey = departments.FirstOrDefault(x =>
                    string.Equals(NormalizeDepartmentKey(x.DepartmentKey), normalizedCandidateKey, StringComparison.OrdinalIgnoreCase));
                if (byKey is not null)
                {
                    return byKey;
                }
            }

            var normalizedCandidateLabel = NormalizeDepartmentLabel(candidate);
            if (!string.IsNullOrWhiteSpace(normalizedCandidateLabel))
            {
                var byLabel = departments.FirstOrDefault(x =>
                    string.Equals(NormalizeDepartmentLabel(x.NameEn), normalizedCandidateLabel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizeDepartmentLabel(x.NameAr), normalizedCandidateLabel, StringComparison.OrdinalIgnoreCase));
                if (byLabel is not null)
                {
                    return byLabel;
                }
            }
        }

        return null;
    }

    private static List<string> BuildDepartmentCandidates(IReadOnlyDictionary<string, string> normalizedValues)
    {
        var candidates = new List<string>();
        foreach (var entry in normalizedValues)
        {
            if (!IsDepartmentCandidateKey(entry.Key))
            {
                continue;
            }

            var value = NormalizeNullable(entry.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            candidates.Add(value);
        }

        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsDepartmentCandidateKey(string key)
    {
        var normalizedKey = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return false;
        }

        foreach (var candidateKey in LeadDepartmentKeys)
        {
            var normalizedCandidate = NormalizeKey(candidateKey);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                continue;
            }

            if (string.Equals(normalizedKey, normalizedCandidate, StringComparison.OrdinalIgnoreCase)
                || normalizedKey.EndsWith($"_{normalizedCandidate}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void MergeDepartmentHintsFromSessionVariables(
        IDictionary<string, string> normalizedValues,
        ConversationFlowSession session)
    {
        var sessionVariables = ParseSessionVariableDictionary(session.VariablesJson);
        if (sessionVariables.Count == 0)
        {
            return;
        }

        foreach (var entry in sessionVariables)
        {
            var key = NormalizeFormVariableName(entry.Key, 0);
            if (!IsDepartmentCandidateKey(key))
            {
                continue;
            }

            var value = NormalizeNullable(entry.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!normalizedValues.ContainsKey(key))
            {
                normalizedValues[key] = value;
            }
        }
    }

    private static Dictionary<string, string> ParseSessionVariableDictionary(string? json)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.Object or JsonValueKind.Array => property.Value.GetRawText(),
                    _ => ConvertJsonValueToString(property.Value)
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[property.Name] = value;
                }
            }
        }
        catch
        {
            // Ignore malformed session variables JSON.
        }

        return values;
    }

    private static string NormalizeDepartmentKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeWhitespace(value).ToLowerInvariant();
        normalized = normalized.Replace(' ', '_').Replace('-', '_');
        normalized = DepartmentKeySanitizer.Replace(normalized, string.Empty);
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized.Trim('_');
    }

    private static string NormalizeDepartmentLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeWhitespace(value)
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();
        normalized = Regex.Replace(normalized, "\\s+", " ");
        return normalized.ToLowerInvariant();
    }

    private static string NormalizeLeadSource(string source)
    {
        var normalized = NormalizeKey(source);
        return string.IsNullOrWhiteSpace(normalized) ? "flow" : normalized;
    }

    private async Task<bool> HasOutboundAlreadyBeenLoggedAsync(
        ConversationFlowSession session,
        string nodeId,
        string message,
        string? metadataJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        return await _db.ConversationFlowExecutionLogs
            .AsNoTracking()
            .AnyAsync(x =>
                x.ConversationFlowSessionId == session.ConversationFlowSessionId
                && x.NodeId == nodeId
                && x.Direction == "outbound"
                && x.Message == message
                && x.MetadataJson == metadataJson
                && (x.EventType == "message_queued" || x.EventType == "message_simulated"), ct);
    }

    private async Task CompleteSessionAsync(ConversationFlowSession session, string status, CancellationToken ct)
    {
        session.Status = status;
        session.CompletedAtUtc = DateTime.UtcNow;
        session.LastInteractionAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<TenantWhatsAppConfig> ResolveConfigAsync(int companyId, int? whatsAppPhoneNumberId, CancellationToken ct)
    {
        if (whatsAppPhoneNumberId.HasValue)
        {
            var config = await _configService.GetConfigByWhatsAppPhoneNumberIdAsync(whatsAppPhoneNumberId.Value, ct);
            if (config is not null)
            {
                return config;
            }
        }

        return await _configService.GetRequiredConfigAsync(companyId, ct);
    }

    private async Task<ApiResponse<ConversationFlowDto>?> ValidateRequestAsync(int companyId, ConversationFlowUpsertRequest request, long? currentFlowId, CancellationToken ct)
    {
        var triggerType = request.EntryTriggerType.Trim().ToLowerInvariant();
        if (!AllowedTriggerTypes.Contains(triggerType))
        {
            return ApiResponse<ConversationFlowDto>.Fail("Invalid entry trigger type.", HttpStatusCode.BadRequest);
        }

        if (triggerType != "any_message" && string.IsNullOrWhiteSpace(request.EntryTriggerValue))
        {
            return ApiResponse<ConversationFlowDto>.Fail("Entry trigger value is required.", HttpStatusCode.BadRequest);
        }

        if (request.Definition is null)
        {
            return ApiResponse<ConversationFlowDto>.Fail("Flow definition is required.", HttpStatusCode.BadRequest);
        }

        var graphError = ValidateGraph(request.Definition);
        if (graphError is not null)
        {
            return ApiResponse<ConversationFlowDto>.Fail(graphError, HttpStatusCode.BadRequest);
        }

        var normalizedName = request.Name.Trim().ToLowerInvariant();
        var exists = await _db.ConversationFlows
            .AnyAsync(x => x.CompanyId == companyId && x.ConversationFlowId != currentFlowId && x.Name.ToLower() == normalizedName, ct);

        if (exists)
        {
            return ApiResponse<ConversationFlowDto>.Fail("Flow name already exists.", HttpStatusCode.Conflict);
        }

        return null;
    }

    private async Task<bool> ShouldAutoCloseSessionBeforeContinuationAsync(ConversationFlowSession session, CancellationToken ct)
    {
        if (!string.Equals(session.Status, "WAITING_INPUT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (session.LastInteractionAtUtc <= DateTime.UtcNow - WaitingInputSessionTimeout)
        {
            return true;
        }

        return await HasFailedMetaFlowDispatchAsync(session, ct);
    }

    private static bool ShouldRestartMetaFlowSessionOnTrigger(ConversationFlowSession session, FlowInboundMessage inbound)
    {
        if (!string.Equals(session.Status, "WAITING_INPUT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(NormalizeKey(inbound.InteractiveType ?? string.Empty), "nfm_reply", StringComparison.OrdinalIgnoreCase))
        {
            var flow = session.ConversationFlow;
            if (flow is null || !flow.IsActive || !flow.IsPublished || string.IsNullOrWhiteSpace(flow.PublishedDefinitionJson))
            {
                return false;
            }

            var graph = DeserializeGraph(flow.PublishedDefinitionJson);
            if (graph is null)
            {
                return false;
            }

            var currentNode = graph.Nodes.FirstOrDefault(x => string.Equals(x.Id, session.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
            if (currentNode is null || !string.Equals(NormalizeKey(currentNode.Type), "meta_flow", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return MatchesTrigger(flow, inbound);
        }

        return false;
    }

    private async Task<bool> HasFailedMetaFlowDispatchAsync(ConversationFlowSession session, CancellationToken ct)
    {
        var flow = session.ConversationFlow;
        if (flow is null || !flow.IsPublished || string.IsNullOrWhiteSpace(flow.PublishedDefinitionJson))
        {
            return false;
        }

        var graph = DeserializeGraph(flow.PublishedDefinitionJson);
        if (graph is null)
        {
            return false;
        }

        var currentNode = graph.Nodes.FirstOrDefault(x => string.Equals(x.Id, session.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
        if (currentNode is null || !string.Equals(NormalizeKey(currentNode.Type), "meta_flow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lastQueuedMessage = await _db.ConversationFlowExecutionLogs
            .AsNoTracking()
            .Where(x => x.ConversationFlowSessionId == session.ConversationFlowSessionId
                && x.NodeId == currentNode.Id
                && x.Direction == "outbound"
                && x.EventType == "message_queued")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Message, x.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (lastQueuedMessage is null || string.IsNullOrWhiteSpace(lastQueuedMessage.Message))
        {
            return false;
        }

        var lowerBound = lastQueuedMessage.CreatedAtUtc.AddMinutes(-2);
        var upperBound = lastQueuedMessage.CreatedAtUtc.AddMinutes(10);

        return await _db.ConversationMessages
            .AsNoTracking()
            .AnyAsync(x =>
                x.ConversationId == session.ConversationId
                && x.Direction == "outbound"
                && x.MessageType == "interactive"
                && (x.Status == "failed" || x.Status == "FAILED")
                && x.Content == lastQueuedMessage.Message
                && x.TimestampUtc >= lowerBound
                && x.TimestampUtc <= upperBound, ct);
    }

    private static string? ValidateGraph(ConversationFlowGraphDto graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return "Flow definition must contain at least one node.";
        }

        var startNodes = graph.Nodes.Count(x => string.Equals(x.Type, "start", StringComparison.OrdinalIgnoreCase));
        if (startNodes != 1)
        {
            return "Flow definition must contain exactly one start node.";
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.Nodes)
        {
            if (!ids.Add(node.Id))
            {
                return $"Duplicate node id '{node.Id}'.";
            }

            if (!AllowedNodeTypes.Contains(node.Type))
            {
                return $"Unsupported node type '{node.Type}'.";
            }

            var nodeError = ValidateNode(node);
            if (nodeError is not null)
            {
                return nodeError;
            }
        }

        foreach (var edge in graph.Edges)
        {
            if (!ids.Contains(edge.SourceNodeId) || !ids.Contains(edge.TargetNodeId))
            {
                return $"Edge '{edge.Id}' points to a missing node.";
            }
        }

        return null;
    }

    private static string? ValidateNode(ConversationFlowNodeDto node)
    {
        var nodeType = NormalizeKey(node.Type);
        switch (nodeType)
        {
            case "message":
            case "capture_text":
                if (string.IsNullOrWhiteSpace(node.BodyText))
                {
                    return $"Node '{node.Title}' requires body text.";
                }
                break;
            case "menu":
            {
                var presentation = NormalizeKey(node.MenuPresentation ?? "buttons");
                if (presentation == "list")
                {
                    if (node.Sections.Count == 0 && node.Options.Count == 0)
                    {
                        return $"Menu node '{node.Title}' requires options.";
                    }
                }
                else if (node.Options.Count == 0)
                {
                    return $"Menu node '{node.Title}' requires options.";
                }

                break;
            }
            case "form":
                if (node.Options.Count == 0)
                {
                    return $"Form node '{node.Title}' requires at least one field.";
                }

                if (node.Options.Any(option => string.IsNullOrWhiteSpace(option.Id)))
                {
                    return $"Form node '{node.Title}' has a field with missing variable name.";
                }

                if (node.Options.Any(option => string.IsNullOrWhiteSpace(option.Label)))
                {
                    return $"Form node '{node.Title}' has a field with missing prompt.";
                }

                foreach (var option in node.Options)
                {
                    var inputType = NormalizeKey(option.InputType ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(inputType)
                        && !AllowedFormInputTypes.Contains(inputType))
                    {
                        return $"Form node '{node.Title}' has field '{option.Id}' with unsupported input type '{option.InputType}'.";
                    }

                    if (!string.IsNullOrWhiteSpace(option.ValidationPattern))
                    {
                        try
                        {
                            _ = new Regex(option.ValidationPattern, RegexOptions.CultureInvariant | RegexOptions.Singleline);
                        }
                        catch (ArgumentException ex)
                        {
                            return $"Form node '{node.Title}' has invalid regex on field '{option.Id}': {ex.Message}";
                        }
                    }
                }
                break;
            case "meta_flow":
            {
                if (string.IsNullOrWhiteSpace(node.MetaFlowId) && string.IsNullOrWhiteSpace(node.MetaFlowName))
                {
                    return $"Meta flow node '{node.Title}' requires flow id or flow name.";
                }

                if (string.IsNullOrWhiteSpace(node.MetaFlowCta))
                {
                    return $"Meta flow node '{node.Title}' requires CTA text.";
                }

                var mode = NormalizeKey(node.MetaFlowMode ?? "published");
                if (mode is not ("published" or "draft"))
                {
                    return $"Meta flow node '{node.Title}' has invalid mode.";
                }

                var action = NormalizeKey(node.MetaFlowAction ?? "navigate");
                if (action is not ("navigate" or "data_exchange"))
                {
                    return $"Meta flow node '{node.Title}' has invalid action.";
                }

                break;
            }
            case "assign_agent":
            {
                var assignMode = NormalizeKey(node.AssignMode ?? "auto");
                if (assignMode != "auto" && assignMode != "specific" && assignMode != "specific_team" && assignMode != "team_auto")
                {
                    return $"Assign node '{node.Title}' has an invalid assign mode.";
                }

                if (assignMode == "specific" && !node.AssignToUserId.HasValue)
                {
                    return $"Assign node '{node.Title}' requires a target user.";
                }

                if ((assignMode == "specific_team" || assignMode == "team_auto") && !node.AssignToTeamId.HasValue)
                {
                    return $"Assign node '{node.Title}' requires a target team.";
                }

                break;
            }
            case "external_link":
                if (string.IsNullOrWhiteSpace(node.Url))
                {
                    return $"External link node '{node.Title}' requires a URL.";
                }
                break;
        }

        return null;
    }

    private static ConversationFlowDto ToDto(ConversationFlow flow, IReadOnlyDictionary<long, int> activeCounts)
    {
        return new ConversationFlowDto
        {
            ConversationFlowId = flow.ConversationFlowId,
            CompanyId = flow.CompanyId,
            Name = flow.Name,
            Description = flow.Description,
            EntryTriggerType = flow.EntryTriggerType,
            EntryTriggerValue = flow.EntryTriggerValue,
            IsActive = flow.IsActive,
            IsPublished = flow.IsPublished,
            DraftVersion = flow.DraftVersion,
            PublishedVersion = flow.PublishedVersion,
            TriggerCount = flow.TriggerCount,
            CreatedAtUtc = flow.CreatedAtUtc,
            UpdatedAtUtc = flow.UpdatedAtUtc,
            PublishedAtUtc = flow.PublishedAtUtc,
            Definition = DeserializeGraph(flow.DraftDefinitionJson) ?? new ConversationFlowGraphDto(),
            ActiveSessionCount = activeCounts.TryGetValue(flow.ConversationFlowId, out var activeCount) ? activeCount : 0
        };
    }

    private static bool MatchesTrigger(ConversationFlow flow, FlowInboundMessage inbound)
    {
        var triggerType = NormalizeKey(flow.EntryTriggerType);
        var value = NormalizeTriggerValue(flow.EntryTriggerValue) ?? string.Empty;
        var inboundValue = NormalizeInboundValue(inbound);

        if (triggerType == "any_message")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(inboundValue))
        {
            return false;
        }

        return triggerType switch
        {
            "keyword" => inboundValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => string.Equals(token, value, StringComparison.OrdinalIgnoreCase)),
            "contains" => inboundValue.Contains(value, StringComparison.OrdinalIgnoreCase),
            "exact" => string.Equals(inboundValue, value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static int GetTriggerWeight(string triggerType)
        => NormalizeKey(triggerType) switch
        {
            "exact" => 0,
            "keyword" => 1,
            "contains" => 2,
            "any_message" => 3,
            _ => 10
        };

    private static string NormalizeInboundValue(FlowInboundMessage inbound)
    {
        return string.Join(' ', new[] { inbound.SelectionId, inbound.SelectionTitle, inbound.Text }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())).Trim();
    }

    private static string NormalizeKey(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeTriggerValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildOutboundLogMetadata(string? inboundMetaMessageId, string conversationMessageType)
    {
        if (string.IsNullOrWhiteSpace(inboundMetaMessageId))
        {
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            inboundMetaMessageId = inboundMetaMessageId.Trim(),
            conversationMessageType
        }, JsonOpts);
    }

    private static ConversationFlowGraphDto? GetGraphForExecution(ConversationFlow flow, bool usePublishedVersion)
    {
        if (usePublishedVersion)
        {
            return DeserializeGraph(flow.PublishedDefinitionJson);
        }

        return DeserializeGraph(flow.DraftDefinitionJson)
            ?? DeserializeGraph(flow.PublishedDefinitionJson);
    }

    private static ConversationFlowGraphDto? DeserializeGraph(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConversationFlowGraphDto>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> LoadVariables(ConversationFlowSession session, Conversation conversation, Contact contact)
    {
        Dictionary<string, string>? variables = null;
        try
        {
            variables = JsonSerializer.Deserialize<Dictionary<string, string>>(session.VariablesJson, JsonOpts);
        }
        catch
        {
        }

        variables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        variables["customer_name"] = string.IsNullOrWhiteSpace(contact.Name) ? (conversation.ContactName ?? conversation.ContactNumber) : contact.Name;
        variables["contact_name"] = variables["customer_name"];
        variables["contact_number"] = conversation.ContactNumber;
        return variables;
    }

    private static string Render(string? template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return VariablePattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value))
            {
                return value;
            }

            if (key.EndsWith("_label", StringComparison.OrdinalIgnoreCase))
            {
                var baseKey = key[..^"_label".Length];
                if (variables.TryGetValue(baseKey, out var baseValue))
                {
                    return baseValue;
                }
            }

            return match.Value;
        }).Trim();
    }

    private static string? ResolveNextNodeId(IEnumerable<ConversationFlowEdgeDto> edges, string? sourceHandle)
    {
        if (!string.IsNullOrWhiteSpace(sourceHandle))
        {
            var exact = edges.FirstOrDefault(x => string.Equals(NormalizeKey(x.SourceHandle ?? string.Empty), NormalizeKey(sourceHandle), StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact.TargetNodeId;
            }
        }

        var defaultEdge = edges.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.SourceHandle) || string.Equals(NormalizeKey(x.SourceHandle ?? string.Empty), "default", StringComparison.OrdinalIgnoreCase));
        return defaultEdge?.TargetNodeId;
    }

    private static (string Id, string Label)? TryResolveMenuSelection(ConversationFlowNodeDto node, FlowInboundMessage inbound)
    {
        var candidates = FlattenNodeOptions(node).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var tokens = new[]
        {
            NormalizeKey(inbound.SelectionId ?? string.Empty),
            NormalizeKey(inbound.SelectionTitle ?? string.Empty),
            NormalizeKey(inbound.Text ?? string.Empty)
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

        foreach (var token in tokens)
        {
            var exact = candidates.FirstOrDefault(x => string.Equals(NormalizeKey(x.Id), token, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return (exact.Id, exact.Label);
            }

            exact = candidates.FirstOrDefault(x => string.Equals(NormalizeKey(x.Label), token, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return (exact.Id, exact.Label);
            }
        }

        return null;
    }

    private static IEnumerable<ConversationFlowOptionDto> FlattenNodeOptions(ConversationFlowNodeDto node)
    {
        if (node.Sections.Count > 0)
        {
            foreach (var section in node.Sections)
            {
                foreach (var option in section.Options)
                {
                    yield return option;
                }
            }
        }

        foreach (var option in node.Options)
        {
            yield return option;
        }
    }

    private static object BuildButtonsPayload(string toNumber, ConversationFlowNodeDto node, IReadOnlyDictionary<string, string> variables)
    {
        var buttons = node.Options.Take(3).Select(option => new
        {
            type = "reply",
            reply = new
            {
                id = option.Id,
                title = Render(option.Label, variables)
            }
        }).ToList<object>();

        var interactive = new Dictionary<string, object?>
        {
            ["type"] = "button",
            ["body"] = new { text = Render(node.BodyText, variables) },
            ["action"] = new { buttons }
        };

        var footerText = Render(node.FooterText, variables);
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            interactive["footer"] = new { text = footerText };
        }

        return new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toNumber,
            ["type"] = "interactive",
            ["interactive"] = interactive
        };
    }

    private static object BuildListPayload(string toNumber, ConversationFlowNodeDto node, IReadOnlyDictionary<string, string> variables)
    {
        var sections = node.Sections.Count > 0
            ? node.Sections.Select(section => new
            {
                title = Render(section.Title, variables),
                rows = section.Options.Select(option => new
                {
                    id = option.Id,
                    title = Render(option.Label, variables),
                    description = string.IsNullOrWhiteSpace(option.Description) ? null : Render(option.Description, variables)
                }).ToList<object>()
            }).ToList<object>()
            : new List<object>
            {
                new
                {
                    title = "Options",
                    rows = node.Options.Select(option => new
                    {
                        id = option.Id,
                        title = Render(option.Label, variables),
                        description = string.IsNullOrWhiteSpace(option.Description) ? null : Render(option.Description, variables)
                    }).ToList<object>()
                }
            };

        var interactive = new Dictionary<string, object?>
        {
            ["type"] = "list",
            ["body"] = new { text = Render(node.BodyText, variables) },
            ["action"] = new
            {
                button = string.IsNullOrWhiteSpace(node.ButtonText) ? "Choose" : Render(node.ButtonText, variables),
                sections
            }
        };

        var footerText = Render(node.FooterText, variables);
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            interactive["footer"] = new { text = footerText };
        }

        return new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toNumber,
            ["type"] = "interactive",
            ["interactive"] = interactive
        };
    }

    private static object BuildMetaFlowPayload(
        string toNumber,
        ConversationFlowNodeDto node,
        IReadOnlyDictionary<string, string> variables,
        long sessionId)
    {
        var flowId = Render(node.MetaFlowId, variables);
        var flowName = Render(node.MetaFlowName, variables);
        var cta = Render(node.MetaFlowCta, variables);
        var mode = NormalizeKey(node.MetaFlowMode ?? "published");
        var action = NormalizeKey(node.MetaFlowAction ?? "navigate");

        if (string.IsNullOrWhiteSpace(cta))
        {
            cta = "Open";
        }

        var parameters = new Dictionary<string, object?>
        {
            ["flow_message_version"] = "3",
            ["flow_token"] = BuildMetaFlowToken(sessionId, node.Id),
            ["flow_cta"] = cta,
            ["mode"] = mode,
            ["flow_action"] = action
        };

        if (!string.IsNullOrWhiteSpace(flowId))
        {
            parameters["flow_id"] = flowId;
        }
        else if (!string.IsNullOrWhiteSpace(flowName))
        {
            parameters["flow_name"] = flowName;
        }

        var flowActionPayload = new Dictionary<string, object?>();
        var screen = Render(node.MetaFlowScreen, variables);
        if (!string.IsNullOrWhiteSpace(screen))
        {
            flowActionPayload["screen"] = screen;
        }

        var dataJson = Render(node.MetaFlowDataJson, variables);
        if (!string.IsNullOrWhiteSpace(dataJson))
        {
            try
            {
                using var dataDoc = JsonDocument.Parse(dataJson);
                if (dataDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    flowActionPayload["data"] = JsonSerializer.Deserialize<Dictionary<string, object?>>(dataDoc.RootElement.GetRawText(), JsonOpts);
                }
            }
            catch
            {
                // Keep dispatch resilient when data JSON is malformed after variable rendering.
            }
        }

        if (flowActionPayload.Count > 0)
        {
            parameters["flow_action_payload"] = flowActionPayload;
        }

        var interactive = new Dictionary<string, object?>
        {
            ["type"] = "flow",
            ["action"] = new
            {
                name = "flow",
                parameters
            }
        };

        var bodyText = Render(node.BodyText, variables);
        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            interactive["body"] = new { text = bodyText };
        }

        var footerText = Render(node.FooterText, variables);
        if (!string.IsNullOrWhiteSpace(footerText))
        {
            interactive["footer"] = new { text = footerText };
        }

        return new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toNumber,
            ["type"] = "interactive",
            ["interactive"] = interactive
        };
    }

    private static string BuildExternalLinkMessage(ConversationFlowNodeDto node, IReadOnlyDictionary<string, string> variables)
    {
        var parts = new List<string>();
        var bodyText = Render(node.BodyText, variables);
        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            parts.Add(bodyText);
        }

        var url = Render(node.Url, variables);
        if (!string.IsNullOrWhiteSpace(url))
        {
            parts.Add(url);
        }

        return string.Join(Environment.NewLine, parts).Trim();
    }

    private static string BuildFormFieldPrompt(ConversationFlowOptionDto field, IReadOnlyDictionary<string, string> variables)
    {
        var prompt = Render(field.Label, variables);
        var hint = Render(field.Description, variables);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = NormalizeFormVariableName(field.Id, 0);
        }

        if (string.IsNullOrWhiteSpace(hint))
        {
            return prompt;
        }

        return $"{prompt}{Environment.NewLine}{hint}".Trim();
    }

    private static string BuildMetaFlowToken(long sessionId, string nodeId)
        => $"flow_{sessionId}_{NormalizeKey(nodeId)}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

    private static Dictionary<string, string> ParseInboundStructuredValues(FlowInboundMessage inbound)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var json = NormalizeNullable(inbound.StructuredDataJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            var maybeJson = NormalizeNullable(inbound.Text);
            if (!string.IsNullOrWhiteSpace(maybeJson)
                && (maybeJson.TrimStart().StartsWith("{", StringComparison.Ordinal) || maybeJson.TrimStart().StartsWith("[", StringComparison.Ordinal)))
            {
                json = maybeJson;
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                var encodedJson = NormalizeNullable(root.GetString());
                if (!string.IsNullOrWhiteSpace(encodedJson)
                    && (encodedJson.TrimStart().StartsWith("{", StringComparison.Ordinal) || encodedJson.TrimStart().StartsWith("[", StringComparison.Ordinal)))
                {
                    using var nestedDocument = JsonDocument.Parse(encodedJson);
                    if (nestedDocument.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        FlattenStructuredElement(nestedDocument.RootElement, null, values);
                    }
                }

                return values;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            FlattenStructuredElement(root, null, values);
        }
        catch
        {
            // Ignore malformed inbound structured payload and let node-level invalid input handling run.
        }

        return values;
    }

    private static Dictionary<string, string> ExtractUserStructuredValues(IReadOnlyDictionary<string, string> structuredValues)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in structuredValues)
        {
            if (IsMetaFlowTechnicalField(entry.Key))
            {
                continue;
            }

            values[entry.Key] = entry.Value;
        }

        return values;
    }

    private static bool IsMetaFlowTechnicalField(string key)
    {
        var normalized = NormalizeKey(key);
        return string.Equals(normalized, "flow_token", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("_flow_token", StringComparison.OrdinalIgnoreCase);
    }

    private static void FlattenStructuredElement(JsonElement element, string? prefix, IDictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    var normalizedPropertyName = NormalizeFormVariableName(property.Name, 0);
                    var nextPrefix = string.IsNullOrWhiteSpace(prefix)
                        ? normalizedPropertyName
                        : $"{prefix}_{normalizedPropertyName}";
                    FlattenStructuredElement(property.Value, nextPrefix, values);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                var items = element.EnumerateArray().ToList();
                if (items.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        values[prefix] = string.Empty;
                    }
                    break;
                }

                var simpleValues = items.All(item => item.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined);
                if (simpleValues && !string.IsNullOrWhiteSpace(prefix))
                {
                    values[prefix] = string.Join(", ", items.Select(ConvertJsonValueToString));
                    break;
                }

                var index = 0;
                foreach (var item in items)
                {
                    var nextPrefix = string.IsNullOrWhiteSpace(prefix) ? $"item_{index}" : $"{prefix}_{index}";
                    FlattenStructuredElement(item, nextPrefix, values);
                    index++;
                }

                break;
            }
            default:
            {
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    values[prefix] = ConvertJsonValueToString(element);
                }
                break;
            }
        }
    }

    private static string ConvertJsonValueToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static string BuildFormStepKey(string nodeId)
        => $"__form_step_{NormalizeKey(nodeId)}";

    private static int ResolveFormFieldIndex(Dictionary<string, string> variables, string formStepKey, int maxExclusive)
    {
        if (variables.TryGetValue(formStepKey, out var storedIndex)
            && int.TryParse(storedIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 0
            && parsed < maxExclusive)
        {
            return parsed;
        }

        return 0;
    }

    private static string ResolveInboundText(FlowInboundMessage inbound)
    {
        if (!string.IsNullOrWhiteSpace(inbound.Text))
        {
            return inbound.Text.Trim();
        }

        if (!string.IsNullOrWhiteSpace(inbound.SelectionTitle))
        {
            return inbound.SelectionTitle.Trim();
        }

        return string.IsNullOrWhiteSpace(inbound.SelectionId) ? string.Empty : inbound.SelectionId.Trim();
    }

    private static string BuildFieldValidationMessage(
        ConversationFlowOptionDto field,
        ConversationFlowNodeDto node,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(field.ValidationMessage))
        {
            return field.ValidationMessage!;
        }

        if (!string.IsNullOrWhiteSpace(node.InvalidInputMessage))
        {
            return node.InvalidInputMessage!;
        }

        return fallback;
    }

    private static string NormalizeAndValidateFieldInput(ConversationFlowOptionDto field, string rawValue, out bool validationFailed)
    {
        var normalized = NormalizeWhitespace(rawValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            validationFailed = true;
            return string.Empty;
        }

        var inputType = NormalizeKey(field.InputType ?? "text");
        switch (inputType)
        {
            case "":
            case "text":
                break;
            case "name":
            case "full_name":
            {
                if (!FullNameInputPattern.IsMatch(normalized))
                {
                    validationFailed = true;
                    return string.Empty;
                }
                break;
            }
            case "phone":
            {
                normalized = NormalizePhoneValue(normalized);
                if (!E164InputPattern.IsMatch(normalized))
                {
                    validationFailed = true;
                    return string.Empty;
                }
                break;
            }
            case "email":
            {
                normalized = normalized.ToLowerInvariant();
                if (!IsValidEmail(normalized))
                {
                    validationFailed = true;
                    return string.Empty;
                }
                break;
            }
            case "boolean":
            case "yes_no":
            case "whatsapp_support":
            {
                if (!TryNormalizeBooleanValue(normalized, out var boolValue))
                {
                    validationFailed = true;
                    return string.Empty;
                }

                normalized = boolValue;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
        {
            try
            {
                if (!Regex.IsMatch(normalized, field.ValidationPattern, RegexOptions.CultureInvariant | RegexOptions.Singleline))
                {
                    validationFailed = true;
                    return string.Empty;
                }
            }
            catch
            {
                validationFailed = true;
                return string.Empty;
            }
        }

        validationFailed = false;
        return normalized;
    }

    private static string NormalizeWhitespace(string value)
    {
        var compact = Regex.Replace(value.Trim(), "\\s+", " ");
        return compact.Trim();
    }

    private static string NormalizePhoneValue(string value)
    {
        var compact = value.Trim();
        compact = compact.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);
        return compact;
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var mail = new MailAddress(value);
            return string.Equals(mail.Address, value, StringComparison.OrdinalIgnoreCase)
                && mail.Address.Contains('@', StringComparison.Ordinal)
                && mail.Host.Contains('.', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeBooleanValue(string value, out string normalized)
    {
        var lowered = NormalizeKey(value);
        if (lowered is "yes" or "y" or "true" or "1" or "\u0646\u0639\u0645" or "\u0627\u064A\u0648\u0647" or "\u0627\u064A\u0648\u0627")
        {
            normalized = "yes";
            return true;
        }

        if (lowered is "no" or "n" or "false" or "0" or "\u0644\u0627")
        {
            normalized = "no";
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static Dictionary<string, string> BuildFormSubmissionValues(
        IReadOnlyList<ConversationFlowOptionDto> fields,
        IReadOnlyDictionary<string, string> variables)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fields.Count; i++)
        {
            var key = NormalizeFormVariableName(fields[i].Id, i);
            values[key] = variables.TryGetValue(key, out var value) ? value : string.Empty;
        }

        return values;
    }

    private static string NormalizeFormVariableName(string? raw, int fallbackIndex)
    {
        var baseKey = NormalizeKey(raw ?? string.Empty);
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            return $"form_field_{fallbackIndex + 1}";
        }

        var chars = baseKey.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? $"form_field_{fallbackIndex + 1}" : normalized;
    }

    private static ConversationFlowRuntimeResult BuildRuntimeResult(
        ConversationFlowSession session,
        Conversation conversation,
        Contact contact,
        bool startedNewSession,
        FlowExecutionContext executionContext)
    {
        var variables = LoadVariables(session, conversation, contact);
        return new ConversationFlowRuntimeResult
        {
            Handled = true,
            StartedNewSession = startedNewSession,
            SessionId = session.ConversationFlowSessionId,
            SessionStatus = session.Status,
            CurrentNodeId = session.CurrentNodeId,
            Variables = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase),
            Actions = executionContext.Actions
                .Select(action => new ConversationFlowRuntimeAction
                {
                    NodeId = action.NodeId,
                    ActionType = action.ActionType,
                    Preview = action.Preview,
                    MetadataJson = action.MetadataJson
                })
                .ToArray()
        };
    }
}
