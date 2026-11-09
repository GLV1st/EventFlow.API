# TicketPilot API – Phase 3: Staff management

This package is based on the Phase 2 API source. It adds organisation user invitations, acceptance, listing, role changes and deactivation. Existing tenant/client authentication and ticket endpoints are retained.

## Invitation expiry

Staff invitations deliberately have **no automatic expiry**. They remain pending until accepted or revoked by an Owner/Administrator. The API does not set `ExpiresAt` for invitation records.

## Endpoints

All routes are under `/api`.

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/app/users/invitations` | Staff bearer token | Create invitation |
| GET | `/app/users/invitations` | Owner/Administrator | List pending invitations |
| POST | `/app/users/invitations/accept` | Invitation token | Accept invitation and set password |
| POST | `/app/users/invitations/revoke` | Owner/Administrator | Revoke pending invitation (body: `invitationToken`) |
| GET | `/app/users` | Owner/Administrator | List organisation users |
| PATCH | `/app/users/{userId}/role` | Owner only | Change a user's role (body: `role`) |
| POST | `/app/users/{userId}/deactivate` | Owner/Administrator | Deactivate user |

### Create invitation body

```json
{
  "email": "staff@example.com",
  "displayName": "Alex Staff",
  "role": "SCANNER"
}
```

Roles accepted: `ADMINISTRATOR`, `EVENT_MANAGER`, `SCANNER`. Owner may invite all three. Administrator may invite Event Manager and Scanner only. Only Owner can change roles. Owner accounts cannot be modified or deactivated through these endpoints.

The create-invitation response returns the invitation token **once**. Store/deliver it securely. The current implementation does not send email; email delivery and proof-of-mailbox ownership must be integrated before production. Possession of the high-entropy invitation token is currently the acceptance proof. Do not paste invitation tokens into public channels.

### Accept invitation body

```json
{
  "invitationToken": "TP-INV-...",
  "displayName": "Alex Staff",
  "password": "at-least-12-characters"
}
```

Passwords require at least 12 characters and are stored using PBKDF2-SHA256. The token is stored hashed and can be accepted once. Deactivating a user immediately invalidates their sessions because access-token validation checks the current user status on every request.

## Important deployment notes

- Do not deploy until the local build and endpoint tests pass.
- Existing storage uses the `EventFlowRegistry` Azure Table. New invitation records are added to the same table/partition; no table migration is required.
- All user records remain tenant-scoped. Never accept a tenant ID from the browser for management operations; the API derives it from the authenticated user token.
- Email delivery is intentionally not claimed as implemented. Configure a verified sender and integrate a mail provider before offering automated email invitations.
- The API currently returns the invitation token in the create response for manual delivery/testing. Restrict access to that response and avoid logging it.
