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
        "start", "message", "menu", "capture_text", "assign_agent", "external_link", "end"
    };

    private static readonly Regex VariablePattern = new("{{\\s*([a-zA-Z0-9_]+)\\s*}}", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ConversationFlowService> _logger;
    private readonly ITenantWhatsAppConfigService _configService;
    private readonly IMessageDispatchService _messageDispatchService;
    private readonly IRoutingService _routingService;

    private sealed class FlowExecutionContext
    {
        public bool IsDryRun { get; init; }
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

        flow.IsActive = !flow.IsActive;
        flow.UpdatedAtUtc = DateTime.UtcNow;
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
            return await ContinueSessionAsync(activeSession, conversation, contact, inbound, null, ct);
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
            IsDryRun = inbound.IsDryRun
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
            IsDryRun = inbound.IsDryRun
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
                            variables[node.VariableName] = selected.Value.Label;
                            variables[$"{node.VariableName}_id"] = selected.Value.Id;
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
        var preview = assignMode == "specific"
            ? $"Assign conversation to user #{node.AssignToUserId ?? 0}"
            : "Auto assign conversation";

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
                node.AssignToUserId,
                changedByUserId: null,
                updateContactOwner: node.UpdateContactOwner,
                assignmentMode: "AUTO",
                reason: reason,
                notes: $"Flow session {session.ConversationFlowSessionId}",
                cancellationToken: ct);
            return preview;
        }

        await _routingService.AutoAssignConversationAsync(conversation.CompanyId, conversation, contact, reason, ct);
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

        executionContext.Actions.Add(new ConversationFlowRuntimeAction
        {
            NodeId = nodeId,
            ActionType = conversationMessageType,
            Preview = normalizedPreview,
            MetadataJson = executionContext.IsDryRun ? body : null
        });

        if (executionContext.IsDryRun)
        {
            await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, nodeId, "message_simulated", "outbound", normalizedPreview, body, ct);
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

        await AddLogAsync(flow.ConversationFlowId, session, conversation, contact, nodeId, "message_queued", "outbound", normalizedPreview, null, ct);
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
            case "assign_agent":
            {
                var assignMode = NormalizeKey(node.AssignMode ?? "auto");
                if (assignMode != "auto" && assignMode != "specific")
                {
                    return $"Assign node '{node.Title}' has an invalid assign mode.";
                }

                if (assignMode == "specific" && !node.AssignToUserId.HasValue)
                {
                    return $"Assign node '{node.Title}' requires a target user.";
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
            return variables.TryGetValue(key, out var value) ? value : match.Value;
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
