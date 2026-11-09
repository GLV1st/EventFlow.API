# TicketPilot API onboarding - Phase 1

This source update adds the first onboarding API layer while retaining the existing EventFlow ticketing endpoints and legacy registration/token routes.

## New endpoints

### 1. Generate a customer activation key (supplier/admin only)
`POST /api/admin/activation-keys`

Requires the Azure Functions host key (`?code=...` or `x-functions-key`).

Request:
```json
{ "label": "Customer name / sales reference" }
```

Response contains `activationKey`. Copy it securely and give it to the customer. The API stores only its SHA-256 hash. The key can be redeemed once.

### 2. Activate the WordPress website
`POST /api/auth/activate`

Request:
```json
{
  "activationKey": "TP-...",
  "siteUrl": "https://customer.example/",
  "siteName": "Customer website"
}
```

Response contains `tenantId`, `clientId`, `clientSecret` and `appConnectionCode`. The WordPress plugin must store the client credentials server-side and display the app connection code to the site administrator. Do not expose the client secret in browser JavaScript or public pages.

### 3. Redeem the code in the TicketPilot web app
`POST /api/app/connect`

Request:
```json
{ "code": "TP-APP-..." }
```

Response contains tenant/site details and `ownerSetupRequired: true`.

The app connection code has **no automatic expiry**. It is single-use and is stored hashed. The code is consumed when redeemed.

## Important implementation boundary

This is Phase 1 of the onboarding API. It creates the tenant, website client credentials and single-use web-app connection code, and allows the web app to claim the pending tenant. It does **not yet create the first Owner account or staff accounts**. Those endpoints and password/session handling are the next API phase; do not expose a live customer onboarding flow until they are implemented and tested.

The old `/api/auth/register`, `/api/auth/token`, ticket generation, check-in, check-out, event and reporting endpoints are intentionally retained for compatibility.

## Storage

New records are stored in the existing `EventFlowRegistry` Azure Table, partition `REGISTRY`:
- `ACTKEY_{hash}` - supplier activation key, single use
- `TENANT_{tenantId}` - tenant starts in `PENDING_OWNER`
- `CLIENT_{clientId}` - WordPress client credentials hash
- `APPCODE_{hash}` - single-use web-app connection code, no time expiry

No new Azure table or NuGet package is required.

## Build and deployment

1. Back up the existing API project.
2. Copy the updated `src` folder and solution from this package into the existing project, preserving the folder structure.
3. Run `dotnet restore` then `dotnet build .\EventFlow.slnx` from the solution folder.
4. Do not deploy until the build succeeds and the endpoints have been tested against a non-production/test activation key.

The build could not be run in the preparation environment because the .NET SDK was not installed. Treat this package as source changes requiring local build verification before deployment.


## Phase 2 update

See `README-TICKETPILOT-PHASE2.md` for first Owner registration, staff login, current-user validation and logout endpoints. The web app must use `POST /api/app/register-owner` to redeem the connection code and create the first Owner atomically. The older `/api/app/connect` endpoint now returns HTTP 410 and must not be used.
