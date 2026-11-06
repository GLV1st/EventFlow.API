# Trader document requirements

The central rules are implemented in `src/EventFlow.Api/Services/TraderDocumentRequirements.cs`.

| Stall category | Risk Assessment | Public Liability | Food Hygiene |
|---|---|---|---|
| Fudge / Sweets | Required | Required | Required |
| Clothing / Fashion | Required | Required | Not required |
| Jewellery / Accessories | Required | Required | Not required |
| Arts & Crafts | Required | Required | Not required |
| Toys / Children | Required | Required | Not required |
| Home / Gifts | Required | Required | Not required |
| Business / Services | Required | Required | Not required |
| Charity | Required | Required | Not required |
| Entertainment / Activities | Required | Required | Not required |
| Food / Drink | Required | Required | Required |
| Other | Required | Required | Not required |

## Endpoints

- `GET /api/traderdocuments/requirements?pitchType=Food%20%2F%20Drink` returns the required-document flags for a category. URL-encode the `pitchType` query value.
- `GET /api/traderdocuments/status/{eventId}/{passNumber}?token=...` now returns `pitchType`, `requiredDocuments`, uploaded `documents`, `missingRequiredDocuments`, and `allRequiredDocumentsUploaded`.

New trader creation rejects an unknown or missing `PitchType`, and trader updates reject an unrecognized non-empty category. Existing records are not migrated or modified by this code; their requirements are calculated from their current `PitchType` when status is requested.

This changes the Azure API only. The WordPress reminder snippet and upload-page UI must still be updated to consume these rules before the full workflow is centralised end-to-end. Deploy to a test Azure Function app and verify the endpoints before deploying to production. A local .NET build could not be run in this environment because the .NET SDK is not installed.
