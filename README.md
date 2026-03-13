# WhatsApp Cloud API – ASP.NET Core Web API

Production-ready ASP.NET Core 8 Web API using Clean Architecture to integrate with WhatsApp Cloud API.

## Solution Structure

- `WhatsAppCloudApi.Api` → Controllers, middleware, startup, Swagger
- `WhatsAppCloudApi.Application` → Service contracts
- `WhatsAppCloudApi.Infrastructure` → WhatsApp Cloud API HTTP client implementation
- `WhatsAppCloudApi.Domain` → DTOs and options models
- `WhatsAppCloudApi.Shared` → Shared responses, constants, helpers

## Prerequisites

- .NET SDK 8.0+
- WhatsApp Cloud API credentials
- (For IIS) ASP.NET Core Hosting Bundle
- (For local webhook testing) ngrok

## Configuration

Set values in `WhatsAppCloudApi.Api/appsettings.Development.json` or environment variables:

```json
"WhatsApp": {
  "BaseUrl": "https://graph.facebook.com/v18.0/",
  "AccessToken": "...",
  "PhoneNumberId": "...",
  "BusinessAccountId": "...",
  "AppSecret": "...",
  "VerifyToken": "..."
}
```

## Run Locally

1. Restore & build:
   - `dotnet restore`
   - `dotnet build`
2. Run API:
   - `dotnet run --project WhatsAppCloudApi.Api`
3. Open Swagger:
   - `https://localhost:<port>/swagger`

## API Endpoints

### Messaging
- `POST /api/whatsapp/messages/text`
- `POST /api/whatsapp/messages/template`
- `POST /api/whatsapp/messages/media`
- `POST /api/whatsapp/messages/read`

### Media
- `POST /api/whatsapp/media/upload`
- `GET /api/whatsapp/media/{mediaId}`
- `DELETE /api/whatsapp/media/{mediaId}`

### Phone Number
- `GET /api/whatsapp/phone-number`
- `POST /api/whatsapp/phone-number/register`

### Templates
- `GET /api/whatsapp/templates`
- `POST /api/whatsapp/templates`
- `DELETE /api/whatsapp/templates/{templateId}`

### Business Profile
- `GET /api/whatsapp/business-profile`
- `POST /api/whatsapp/business-profile`

### Webhook
- `GET /api/webhook` (verification)
- `POST /api/webhook` (events)

## Security and Reliability

- Strongly typed options + startup validation
- Global exception middleware
- DTO validation via data annotations
- Rate limiting (fixed window)
- Retry policy with Polly for transient failures and `429`
- Request/response logging with token masking helper
- Webhook signature validation (`X-Hub-Signature-256`)

## IIS Deployment Guide (Local Server)

1. Publish release:
   - `dotnet publish -c Release -o ./publish`
2. Install ASP.NET Core Hosting Bundle on the IIS server.
3. In IIS:
   - Create new Site
   - Point physical path to `publish`
   - App Pool:
     - **No Managed Code**
     - **Integrated** pipeline
4. Ensure HTTPS binding if possible.
5. Confirm `web.config` exists in publish output.
6. Browse Swagger:
   - `https://<host>/swagger`

## Webhook Setup Instructions

1. Configure webhook callback URL to:
   - `https://<public-host>/api/webhook`
2. Set verify token in Meta and app config (`WhatsApp:VerifyToken`).
3. For local testing using ngrok:
   - Expose local HTTPS port with ngrok.
   - Use ngrok URL in Meta webhook callback.
4. Subscribe to message/status events in Meta app settings.

## Health Check

- `GET /health`

## Delivery Guardrails

Before closing any task, run:

- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-delivery-guardrails.ps1`

This verifies:

- Arabic/English translation key parity
- Theme wiring (light/dark) at app startup
- Automatic EF migration execution on backend startup

## Docker (Optional)

- Build image from `WhatsAppCloudApi.Api/Dockerfile`.
- Run container with required `WhatsApp__*` environment variables.
