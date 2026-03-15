# WhatsApp Cloud API Endpoint Matrix

This matrix maps Postman collection areas to API routes in this project.

## Core typed endpoints

| Collection Area | Controller Route | Method | Request Structure | Response Structure |
|---|---|---|---|---|
| Send Text Message | `/api/whatsapp/messages/text` | POST | `SendTextMessageRequest` | `ApiResponse<GenericGraphResponse>` |
| Send Template Message | `/api/whatsapp/messages/template` | POST | `SendTemplateMessageRequest` | `ApiResponse<GenericGraphResponse>` |
| Send Media Message | `/api/whatsapp/messages/media` | POST | `SendMediaMessageRequest` | `ApiResponse<GenericGraphResponse>` |
| Upload Media | `/api/whatsapp/media/upload` | POST | `UploadMediaRequest` (base64 input) | `ApiResponse<GenericGraphResponse>` |
| Get Media URL | `/api/whatsapp/media/{mediaId}` | GET | route param | `ApiResponse<GenericGraphResponse>` |
| Delete Media | `/api/whatsapp/media/{mediaId}` | DELETE | route param | `ApiResponse<GenericGraphResponse>` |
| Mark Message as Read | `/api/whatsapp/messages/read` | POST | `MarkAsReadRequest` | `ApiResponse<GenericGraphResponse>` |
| Register Phone | `/api/whatsapp/phone-number/register` | POST | `RegisterPhoneNumberRequest` | `ApiResponse<GenericGraphResponse>` |
| Deregister Phone | `/api/whatsapp/phone-number/deregister` | POST | none | `ApiResponse<GenericGraphResponse>` |
| Request Verification Code | `/api/whatsapp/phone-number/request-code` | POST | `RequestVerificationCodeRequest` | `ApiResponse<GenericGraphResponse>` |
| Verify Code | `/api/whatsapp/phone-number/verify-code` | POST | `VerifyCodeRequest` | `ApiResponse<GenericGraphResponse>` |
| Phone Number Details | `/api/whatsapp/phone-number` | GET | none | `ApiResponse<GenericGraphResponse>` |
| Get Templates | `/api/whatsapp/templates` | GET | none | `ApiResponse<GenericGraphResponse>` |
| Create Template | `/api/whatsapp/templates` | POST | `CreateTemplateRequest` | `ApiResponse<GenericGraphResponse>` |
| Delete Template | `/api/whatsapp/templates/{templateId}` | DELETE | route param | `ApiResponse<GenericGraphResponse>` |
| Get Business Profile | `/api/whatsapp/business-profile` | GET | none | `ApiResponse<GenericGraphResponse>` |
| Update Business Profile | `/api/whatsapp/business-profile` | POST | `UpdateBusinessProfileRequest` | `ApiResponse<GenericGraphResponse>` |

## Explicit collection coverage endpoints (JSON passthrough)

| Collection Area | Controller Route | Method |
|---|---|---|
| Raw Messages endpoint | `/api/whatsapp/messages` | POST |
| WABA | `/api/whatsapp/waba` | GET |
| Owned WABAs | `/api/whatsapp/waba/owned` | GET |
| Shared WABAs | `/api/whatsapp/waba/shared` | GET |
| Webhook subscriptions | `/api/whatsapp/waba/subscriptions` | POST/GET/DELETE |
| Phone numbers list | `/api/whatsapp/phone-numbers` | GET |
| Phone number by ID | `/api/whatsapp/phone-number/{phoneNumberId}` | GET |
| Two-step verification | `/api/whatsapp/phone-number/two-step` | POST |
| Commerce settings | `/api/whatsapp/commerce-settings` | GET/POST |
| Block users | `/api/whatsapp/block-users` | GET/POST/DELETE |
| QR codes | `/api/whatsapp/qr-codes` | GET/POST |
| QR code by ID | `/api/whatsapp/qr-codes/{qrCodeId}` | GET/DELETE |
| Analytics | `/api/whatsapp/analytics` | GET |
| Template by ID | `/api/whatsapp/templates/{templateId}` | GET |
| Template search by name | `/api/whatsapp/templates/search` | GET |
| Template namespace | `/api/whatsapp/templates/namespace` | GET |
| Template edit | `/api/whatsapp/templates/{templateId}/edit` | POST |
| Advanced template delete | `/api/whatsapp/templates` | DELETE |
| Media download passthrough | `/api/whatsapp/media/download/{*mediaPath}` | GET |
| Media upload (local file) | `/api/whatsapp/media/upload/file` | POST |
| Flows | `/api/whatsapp/flows` | POST/GET |
| Flows migrate | `/api/whatsapp/flows/migrate` | POST |
| Flow by ID | `/api/whatsapp/flows/{flowId}` | GET/POST/DELETE |
| Publish flow | `/api/whatsapp/flows/{flowId}/publish` | POST |
| Deprecate flow | `/api/whatsapp/flows/{flowId}/deprecate` | POST |
| Flow assets | `/api/whatsapp/flows/{flowId}/assets` | GET |
| Encryption key | `/api/whatsapp/phone-number/encryption` | POST/GET |
| Business portfolio | `/api/whatsapp/business-portfolio` | GET |
| Billing extended credits | `/api/whatsapp/billing/extendedcredits` | GET |
| On-Prem migration | `/api/whatsapp/onprem/migrate` | POST |
| Business compliance | `/api/whatsapp/business-compliance` | GET/POST |
| Typing indicator | `/api/whatsapp/typing-indicator` | POST |

## CRM & Business Platform Endpoints

| Module | Controller Route | Method | Request Structure | Response Structure |
|---|---|---|---|---|
| Contacts | `/api/contacts` | GET | query: search, page, pageSize | `ApiResponse<PagedResult<Contact>>` |
| Contacts | `/api/contacts/{id}` | GET | route param | `ApiResponse<Contact>` |
| Contacts | `/api/contacts` | POST | `ContactUpsertRequest` | `ApiResponse<Contact>` |
| Contacts | `/api/contacts/{id}` | PUT | `ContactUpsertRequest` | `ApiResponse<Contact>` |
| Contacts | `/api/contacts/{id}` | DELETE | route param | `ApiResponse<bool>` |
| Contacts | `/api/contacts/import` | POST | `ContactImportRequest` | `ApiResponse<object>` |
| Conversations | `/api/conversations` | GET | query: search, pageSize | `ApiResponse<PagedResult<Conversation>>` |
| Conversations | `/api/conversations/{id}/messages` | GET | query: page, pageSize | `ApiResponse<PagedResult<ConversationMessage>>` |
| Conversations | `/api/conversations/{id}/messages` | POST | `SendConversationMessageRequest` | `ApiResponse<ConversationMessage>` |
| Conversations | `/api/conversations/{id}/read` | POST | none | `ApiResponse<bool>` |
| Campaigns | `/api/campaigns` | GET | query: page, pageSize | `ApiResponse<PagedResult<Campaign>>` |
| Campaigns | `/api/campaigns` | POST | `CampaignCreateRequest` | `ApiResponse<Campaign>` |
| Campaigns | `/api/campaigns/{id}` | PUT | `CampaignUpdateRequest` | `ApiResponse<Campaign>` |
| Campaigns | `/api/campaigns/{id}/launch` | POST | none | `ApiResponse<Campaign>` |
| Campaigns | `/api/campaigns/{id}/cancel` | POST | none | `ApiResponse<Campaign>` |
| Campaigns | `/api/campaigns/{id}/contacts` | GET | none | `ApiResponse<List<CampaignContact>>` |
| Automation | `/api/automation` | GET | none | `ApiResponse<List<AutomationRule>>` |
| Automation | `/api/automation/{id}` | GET | route param | `ApiResponse<AutomationRule>` |
| Automation | `/api/automation` | POST | `AutomationRuleUpsertRequest` | `ApiResponse<AutomationRule>` |
| Automation | `/api/automation/{id}` | PUT | `AutomationRuleUpsertRequest` | `ApiResponse<AutomationRule>` |
| Automation | `/api/automation/{id}` | DELETE | route param | `ApiResponse<bool>` |
| Automation | `/api/automation/{id}/toggle` | POST | none | `ApiResponse<AutomationRule>` |
| Notifications | `/api/notifications` | GET | query: page, pageSize | `ApiResponse<PagedResult<Notification>>` |
| Notifications | `/api/notifications/unread-count` | GET | none | `ApiResponse<int>` |
| Notifications | `/api/notifications/{id}/read` | POST | none | `ApiResponse<bool>` |
| Notifications | `/api/notifications/read-all` | POST | none | `ApiResponse<bool>` |
| Notifications | `/api/notifications/{id}` | DELETE | route param | `ApiResponse<bool>` |
| Developer | `/api/developer/info` | GET | none | `ApiResponse<object>` |

## Generic Graph fallback endpoints

| Route | Method | Purpose |
|---|---|---|
| `/api/whatsapp/graph` | POST | Structured generic request with `method`, `path`, `query`, `body` |
| `/api/whatsapp/graph/{*path}` | GET/POST/PUT/DELETE | Direct passthrough for any unmapped Graph endpoint |

## Request/Response structure notes

- All routes return `ApiResponse<GenericGraphResponse>`.
- `GenericGraphResponse` supports:
  - known fields (`id`, `success`, `data`, `paging`, etc.)
  - unknown JSON fields via `AdditionalData`
  - non-JSON/opaque payloads via `RawContent`
- This preserves collection response structures without breaking typed endpoints.
