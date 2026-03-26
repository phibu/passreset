# PassCore V2

A self-service Active Directory password change portal. Built on **.NET 10 LTS** + **React 19** + **MUI 6** + **Vite**, targeting IIS on Windows Server 2022.

PassCore V2 is a modernised fork of [Unosquare/passcore](https://github.com/unosquare/passcore), rebuilt from the ground up for current LTS stacks with hardened security, email notifications, and a clean modern UI.

---

## Features

| Feature | Details |
|---|---|
| AD password change | `System.DirectoryServices.AccountManagement` — domain-joined or explicit LDAP credentials |
| Password strength meter | zxcvbn score, live feedback |
| Password generator | Crypto-secure, configurable entropy |
| Pwned password check | HaveIBeenPwned k-anonymity API |
| reCAPTCHA v3 | Server-side score validation |
| Password-changed email | MailKit, STARTTLS/SMTPS, Mimecast-compatible |
| Expiry reminder emails | Daily background service, configurable threshold |
| AD group allow/block lists | Restrict which users can self-serve |
| Minimum password age | Enforces AD `minPwdAge` policy |
| Must-change-at-next-logon | Clears `pwdLastSet` flag after successful change |
| Rate limiting | Built-in ASP.NET Core fixed-window limiter |
| Security headers | CSP, HSTS, X-Frame-Options, Referrer-Policy, etc. |
| Debug provider | No AD needed for local dev/UI testing |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 LTS (`net10.0-windows`) |
| Web framework | ASP.NET Core minimal hosting |
| AD integration | `System.DirectoryServices` / `AccountManagement` |
| Email | MailKit 4.x |
| Frontend | React 19 + TypeScript |
| UI components | MUI v6 (Material UI) |
| Build tool | Vite 6 |
| Password scoring | zxcvbn |
| Deployment | IIS 10, Windows Server 2022 |

---

## Project Structure

```
PasscoreV2/
├── src/
│   ├── PassCore.Common/             # Shared interfaces and error types
│   ├── PassCore.PasswordProvider/   # AD password provider (Windows-only)
│   └── PassCore.Web/                # ASP.NET Core app + React frontend
│       ├── ClientApp/               # React 19 + Vite source
│       ├── Controllers/             # API endpoints
│       ├── Models/                  # Config and request models
│       ├── Services/                # Email + background services
│       ├── Helpers/                 # Debug/no-op providers
│       ├── appsettings.json         # Default configuration
│       └── Program.cs               # App entry point + DI wiring
└── deploy/
    ├── Publish-PassCore.ps1         # Build frontend + dotnet publish
    ├── Install-PassCore.ps1         # IIS site/pool/cert/permissions setup
    └── AD-ServiceAccount-Setup.md   # AD delegation guide
```

---

## Quick Start — Development

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)

### Run the backend

```bash
cd src/PassCore.Web
dotnet run
```

### Run the frontend (hot-reload)

```bash
cd src/PassCore.Web/ClientApp
npm install
npm run dev
```

The Vite dev server proxies `/api` to `https://localhost:5001`.

### Debug mode (no AD required)

Set `UseDebugProvider: true` in `src/PassCore.Web/appsettings.Development.json`:

```json
{
  "WebSettings": {
    "UseDebugProvider": true
  }
}
```

Use these test usernames to trigger specific error states:

| Username | Result |
|---|---|
| *(any other)* | Success |
| `error` | Generic error |
| `invalidCredentials` | Wrong current password |
| `userNotFound` | User not found |
| `changeNotPermitted` | Not allowed |
| `pwnedPassword` | Pwned password error |
| `passwordTooYoung` | Too recently changed |

---

## Configuration

All settings live in `appsettings.json` (defaults) and `appsettings.Production.json` (production overrides, never committed).

### Key sections

#### `PasswordChangeOptions`
```json
{
  "PasswordChangeOptions": {
    "UseAutomaticContext": true,
    "IdTypeForUser": "UserPrincipalName",
    "DefaultDomain": "yourdomain.com",
    "ClearMustChangePasswordFlag": true,
    "EnforceMinimumPasswordAge": true,
    "RestrictedAdGroups": ["Domain Admins", "Enterprise Admins"],
    "AllowedAdGroups": [],
    "LdapHostnames": ["dc01.yourdomain.com"],
    "LdapPort": 389,
    "LdapUsername": "",
    "LdapPassword": ""
  }
}
```

- `UseAutomaticContext: true` — domain-joined server, no credentials needed
- `UseAutomaticContext: false` — supply `LdapHostnames` / `LdapUsername` / `LdapPassword`
- `AllowedAdGroups: []` (empty) — all users permitted
- `RestrictedAdGroups` — block list takes priority over allow list

#### `SmtpSettings`
```json
{
  "SmtpSettings": {
    "Host": "smtp-relay.yourdomain.com",
    "Port": 587,
    "UseSsl": true,
    "Username": "",
    "Password": "",
    "FromAddress": "passcore@yourdomain.com",
    "FromName": "PassCore Self-Service"
  }
}
```

Port `587` = STARTTLS. Port `465` = SMTPS. Leave `Host` empty to disable email.

#### `ClientSettings`
Controls all UI strings, feature flags, and reCAPTCHA keys.
The full structure is served to the frontend via `GET /api/password`.

---

## Deployment

### 1. Publish

```powershell
# From repo root
.\deploy\Publish-PassCore.ps1
```

This builds the React frontend and runs `dotnet publish` into `deploy\publish\`.

### 2. Install to IIS

```powershell
# Minimal (ApplicationPoolIdentity, configure HTTPS manually)
.\deploy\Install-PassCore.ps1

# With service account and certificate
.\deploy\Install-PassCore.ps1 `
    -AppPoolIdentity "CORP\svc-passcore" `
    -AppPoolPassword "S3cr3t!" `
    -CertThumbprint "A1B2C3D4..."
```

The installer:
- Verifies .NET 10 Hosting Bundle and required IIS features
- Creates/updates app pool (No Managed Code, AlwaysRunning)
- Copies files with robocopy
- Sets NTFS permissions
- Configures HTTPS binding if a cert thumbprint is supplied
- Writes a starter `appsettings.Production.json`

### 3. Configure

Edit `C:\inetpub\PassCore\appsettings.Production.json` — fill in:
- `DefaultDomain`
- `SmtpSettings.Host`
- `Recaptcha.SiteKey` + `Recaptcha.PrivateKey`
- Set `UseDebugProvider: false`

### AD Service Account

See [`deploy/AD-ServiceAccount-Setup.md`](deploy/AD-ServiceAccount-Setup.md) for step-by-step instructions on creating the service account and delegating the required AD permissions.

---

## API

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/password` | Returns `ClientSettings` for UI initialisation |
| `POST` | `/api/password` | Submit password change request |
| `GET` | `/health` | Health check endpoint |

### POST /api/password

Request:
```json
{
  "username": "user@yourdomain.com",
  "currentPassword": "OldP@ss1",
  "newPassword": "NewP@ss2",
  "newPasswordVerify": "NewP@ss2",
  "recaptcha": "<token>"
}
```

Success `200`:
```json
{ "payload": "Password changed successfully.", "errors": [] }
```

Error `400`:
```json
{ "errors": [{ "errorCode": 4, "message": "..." }] }
```

---

## License

MIT — © 2016–2022 Unosquare LLC. PassCore V2 modifications © 2024–2025.
See [passcore/LICENSE](passcore/LICENSE) for the original licence text.
