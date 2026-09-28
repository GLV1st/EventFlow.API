# GateKeeper Apple Wallet + Google Wallet setup

## Apple Wallet

Create an Apple Pass Type ID and Pass Type ID certificate in Apple Developer. Export the certificate + private key as a password-protected `.p12/.pfx`, then base64 encode it. Also provide Apple's current WWDR intermediate certificate as base64.

Required Function App settings:
- APPLE_PASS_TYPE_IDENTIFIER
- APPLE_TEAM_IDENTIFIER
- APPLE_CERTIFICATE_BASE64
- APPLE_CERTIFICATE_PASSWORD
- APPLE_WWDR_CERTIFICATE_BASE64
- APPLE_LOGO_URL
- APPLE_ICON_URL
- APPLE_ORGANIZATION_NAME
- APPLE_BACKGROUND_COLOR
- APPLE_FOREGROUND_COLOR
- APPLE_LABEL_COLOR
- APPLE_WEB_SERVICE_URL (optional for future pass updates)
- APPLE_PASS_AUTH_SECRET (optional; used to derive per-pass authentication tokens)

Apple requires passes to contain a valid pass type identifier and be signed with the corresponding Pass Type ID certificate. The final archive is served as `application/vnd.apple.pkpass`.

## Google Wallet

Create/enable a Google Wallet API project, create a service account, download its JSON key, and authorise that service account as a Developer in the Google Pay & Wallet console.

Required settings:
- GOOGLE_WALLET_ISSUER_ID
- GOOGLE_WALLET_SERVICE_ACCOUNT_JSON
- GOOGLE_WALLET_ORIGIN (normally https://copcarnival.org.uk)
- GOOGLE_WALLET_LOGO_URL

The service account JSON is sensitive and must only be stored in Azure Function App configuration/Key Vault, never in GitHub.
