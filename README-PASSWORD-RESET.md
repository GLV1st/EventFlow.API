# TicketPilot account setup and password recovery

## Owner setup

1. Activate the WooCommerce website using a supplier-issued TicketPilot activation key.
2. Copy the one-time app connection code shown by the plugin.
3. In TicketPilot, choose **Set up organisation** and enter the connection code, organisation name, short code, Owner name, email and password.
4. The API atomically redeems the connection code, activates the tenant and creates its first `OWNER` user. The Owner role is treated as an administrator by the web app and API.

Additional accounts are invited from the Owner-only user management page. The current web UI creates `SCANNER` accounts.

## Password recovery

- `POST /api/app/password/forgot` accepts `{ "email": "person@example.com" }`. It always returns a generic success message to avoid disclosing whether an account exists.
- A matching active account receives a one-time reset link by email. Reset tokens are stored as SHA-256 hashes and expire after 30 minutes.
- `POST /api/app/password/reset` accepts `{ "resetToken": "...", "password": "at least 12 characters" }`.
- A successful reset consumes the token and revokes existing access sessions for that user.
- Email delivery requires `CommunicationServicesConnectionString`, `TicketPilotEmailSender` and `TicketPilotAppUrl` in Function App settings.

## Local configuration

Copy `src/EventFlow.Api/local.settings.example.json` to `local.settings.json` and fill in your own values. Never commit `local.settings.json`. The real local settings file supplied in the uploaded archive is deliberately not included in the corrected package.

## Validation

The web JavaScript and WordPress PHP files were syntax-checked. The .NET SDK is not installed in this working environment, so run `dotnet publish src/EventFlow.Api/EventFlow.Api.csproj -c Release` (or use the included GitHub Actions deployment workflow) before deploying. Test the full reset email flow against the configured Azure Communication Services sender.
