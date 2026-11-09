# TicketPilot API – Email integration update

This update adds server-side TicketPilot email delivery using Azure Communication Services Email.

## What changed

- Added `Services/TicketPilotEmailService.cs`.
- Registered the email service in dependency injection.
- `POST /api/app/users/invitations` now sends a branded invitation email.
- The email contains a link to `https://ticketpilot.uk/accept-invitation?token=...`.
- The raw invitation token is no longer returned by the create-invitation endpoint.
- If invitation email delivery fails, the newly-created invitation is revoked and the API returns `503`; no unusable pending invitation is left behind.
- `POST /api/app/register-owner` sends a welcome email after creating the first Owner. Account creation is not rolled back if email delivery is temporarily unavailable; the response includes `welcomeEmailSent`.

## Required Azure Function App settings

Configure these under Function App → Settings → Environment variables / Application settings:

| Setting | Value |
|---|---|
| `CommunicationServicesConnectionString` | Connection string for your Azure Communication Services resource |
| `TicketPilotEmailSender` | A verified sender address, e.g. `noreply@ticketpilot.uk` |
| `TicketPilotAppUrl` | `https://ticketpilot.uk` |

Do not put the Communication Services connection string in the web app, WordPress plugin, source control, or browser code.

The sender domain/address must first be configured and verified in Azure Communication Services Email. The sender address must be one that ACS authorises for that resource.

## Invitation flow

1. An Owner/Administrator calls `POST /api/app/users/invitations` with a staff bearer token.
2. API creates a high-entropy invitation token and stores only its SHA-256 hash.
3. API emails the raw token inside the acceptance URL.
4. Invitee opens `https://ticketpilot.uk/accept-invitation?token=...`.
5. The web app posts the token, display name and chosen password to `POST /api/app/users/invitations/accept`.
6. Invitation remains valid until accepted or revoked; there is no automatic expiry.

Possession of the emailed invitation link demonstrates access to that mailbox when the invitation is accepted. This update does not add a separate email-verification workflow for Owner registration.

## Build and deployment

- Open `EventFlow.slnx` in VS Code / Visual Studio.
- Run `dotnet build EventFlow.slnx`.
- Configure the three Function App settings above before testing real email delivery.
- Do not deploy until the local build and endpoint tests pass.

The source project already references `Azure.Communication.Email`; no additional NuGet package is required by this update.
