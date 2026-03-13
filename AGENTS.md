# Project Guardrails (Always Apply)

These rules are mandatory for every task in this repository.

## 1) Language Support (Arabic + English)
- Do not ship UI work unless affected screens are valid in both `ar` and `en`.
- Keep translation keys synchronized between:
  - `frontend/public/i18n/ar.json`
  - `frontend/public/i18n/en.json`
- Preserve correct RTL/LTR behavior after any layout or component change.

## 2) Theme Support (Light + Dark)
- Do not ship UI work unless affected screens are usable in both light and dark themes.
- Do not add styling that only works in one theme.
- Keep global theme initialization working from app startup.

## 3) Database Changes (Backend)
- Any EF model/schema change must include a migration in:
  - `WhatsAppCloudApi.Infrastructure/Data/Migrations`
- Database changes must be auto-applied on backend startup (no manual DB step required in deployment).
- Do not mark backend data tasks complete unless startup migration flow is still intact.

## 4) Required Verification Before Finalizing
Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-delivery-guardrails.ps1
```

If this command fails, fix all issues before closing the task.
