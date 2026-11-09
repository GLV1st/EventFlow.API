# TicketPilot API – Phase 2: First Owner and staff authentication foundation

## What this phase adds

- `POST /api/app/register-owner`: atomically redeems the WordPress-generated app connection code, activates the pending tenant, and creates its first Owner account.
- `POST /api/app/login`: authenticates an existing user for a supplied tenant ID.
- `GET /api/app/me`: validates the current staff bearer token and returns its tenant/user/role identity.
- `POST /api/app/logout`: revokes the current staff bearer token.
- Passwords are stored as PBKDF2-SHA256 hashes with a unique random salt (210,000 iterations); plaintext passwords are never stored.
- Staff access tokens are random opaque bearer tokens, stored as SHA-256 hashes, and expire after 15 minutes.
- Owner creation and connection-code redemption happen in one Azure Table Storage transaction. A code cannot be consumed without creating the Owner, and the first Owner cannot be created twice.

## API contracts

### Create the first Owner and connect the app

`POST /api/app/register-owner`

```json
{
  "code": "TP-APP-your-code-from-wordpress",
  "displayName": "Jane Smith",
  "email": "jane@example.com",
  "password": "a-long-password-of-at-least-12-characters"
}
```

Returns `201` and a staff bearer token, tenant identity and website details. The web app should retain the returned `tenantId` and access token using its chosen secure session approach. Do not store or expose WordPress `clientSecret` in browser code.

Password minimum: 12 characters; maximum: 256 characters. Display name maximum: 120 characters.

### Login

`POST /api/app/login`

```json
{
  "tenantId": "tenant-id-returned-by-owner-registration",
  "email": "jane@example.com",
  "password": "the-password"
}
```

Returns `200` with a 15-minute bearer token. Incorrect credentials return the same generic `401` response.

### Current user

`GET /api/app/me`

Header: `Authorization: Bearer <staff-access-token>`

### Logout

`POST /api/app/logout`

Header: `Authorization: Bearer <staff-access-token>`

Revokes that token. A token expires after 15 minutes even if the user does not log out.

### Legacy connection endpoint

`POST /api/app/connect` now returns `410 Gone`. This prevents the older two-step flow from consuming a connection code before the first Owner has been created. Use `/api/app/register-owner` instead.

## Important implementation notes

1. The older WordPress client-credential authentication and ticket endpoints are intentionally retained. Staff tokens are a separate token type and are not accepted by the existing WordPress client-token validator.
2. This phase creates the first Owner and provides login/session primitives. It does **not** yet implement staff invitations, additional users, role administration, password reset, MFA, or scanner permissions. Those are subsequent phases.
3. The `tenantId` is an identifier, not a secret. The API must always derive tenant authority from a validated bearer token for protected staff endpoints; never trust a tenant ID supplied by the browser for authorization.
4. The API currently has no persistent login rate limiter/lockout in this phase. Before production exposure, add rate limiting for `/app/login` and `/app/register-owner`, plus monitoring/alerting for repeated failures.
5. Configure Azure Functions CORS to allow only the production TicketPilot static web app origin (and localhost only during development). CORS is not authentication; API authorization remains mandatory.
6. Use HTTPS only. Do not commit `local.settings.json` secrets, Function host keys, supplier activation keys, or customer client secrets.
7. Build locally before deployment:

```powershell
dotnet restore
dotnet build .\EventFlow.slnx
```

## Azure Table Storage records

All records remain in the existing `EventFlowRegistry` table and `REGISTRY` partition. New record types/keys:

- `USER_<tenantId>_<emailHash>` – Owner account; stores email, display name, role and password hash.
- `USERTOKEN_<tokenHash>` – staff access token; stores tenant, user, role, status and expiry.

The tenant row moves from `PENDING_OWNER` to `ACTIVE` in the same transaction that marks the app code redeemed and adds the first Owner.

## Local smoke-test sequence (after a successful build)

1. Start the Functions host locally with a development Storage connection configured in `local.settings.json` (never commit this file).
2. Generate a supplier activation key using `POST /api/admin/activation-keys` with a local Function host key.
3. Activate a test WordPress site using `POST /api/auth/activate` with the generated activation key. Save the returned `appConnectionCode` and `tenantId` for the test only.
4. Register the first Owner using `POST /api/app/register-owner`. Confirm a `201` response and save the returned staff access token.
5. Call `GET /api/app/me` with the staff bearer token; confirm `200` and the expected tenant/Owner role.
6. Call `POST /api/app/logout` with the token; then call `/api/app/me` again and confirm `401`.
7. Attempt to register another Owner using the same connection code; confirm it is rejected.
8. Log in using `POST /api/app/login`; confirm a fresh token works with `/api/app/me`.
9. Run the existing ticket-generation and scanner endpoint regression tests using the WordPress client credentials.

Do not use a production activation key or production Azure Storage for these tests.
