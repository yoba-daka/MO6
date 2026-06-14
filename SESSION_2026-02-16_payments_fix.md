# Session Save - Payments / Recurring / Harness (2026-02-18)

## Scope
- Investigated payment webhook failures and recurring cancellation issues.
- Hardened webhook parsing/mapping and recurring processing.
- Added a production-side sandbox harness with HTML visibility.
- Ensured harness logic mirrors production decision flow and differs only at DB mutation points.

## Core Issues Investigated
- Recurring callbacks sometimes arrived in shapes that did not map cleanly.
- Some recurring callbacks lacked standard identifiers (`transactionToken`).
- Recurring member resolution by callback email could fail.
- Cancellation flow could pick a wrong anchor transaction.
- Need to test sandbox behavior on cloud while keeping production alive.
- Need to observe recurring arrivals clearly.

## Documentation and Inputs Used
- Meshulam docs:
  - `https://grow-il.readme.io/llms.txt`
  - `https://grow-il.readme.io/reference/post_api-light-server-1-0-createpaymentprocess`
  - `https://grow-il.readme.io/reference/post_api-light-server-1-0-updatedirectdebit`
  - `https://grow-il.readme.io/reference/post_api-light-server-1-0-gettransactioninfo`
  - `https://grow-il.readme.io/reference/recurring-payment-notification-webhook`
- Captured real payloads:
  - `requests.txt`

## Webhook / Payments Hardening (Existing Fix Track)

### 1) Payload Mapping and Parsing
- Added robust mixed-format parsing for webhook payloads (JSON + form).
- Added support for webhook field aliases (`transactionCode`, `paymentSum`, `allPaymentNum`, etc.).
- Added fallback/synthetic webhook token generation when provider token is missing.

### 2) Recurring Success Path
- Conditional provider validation (`getTransactionInfo`) only when callback has provider-grade identifiers.
- Kept idempotency by token.
- Added fallback member lookup by `DirectDebitId` history when email lookup fails.

### 3) Membership and Transaction Reference Safety
- Membership transaction list now appends `TransactionId` when present, otherwise `TransactionToken`.

### 4) Cancel Direct Debit Reliability
- Cancel now anchors on valid direct-debit transaction criteria.
- `UpdateDirectDebit` request/response handling strengthened.
- Local membership cancellation state updates only after provider-side success.

### 5) Shared Webhook Reader
- Added modular payload reader components:
  - `Services/MeshulamWebhookPayload.cs`
  - `Services/MeshulamWebhookPayloadReader.cs`
- All webhook endpoints use shared reader:
  - `/meshulam-response`
  - `/meshulam-dd-success`
  - `/meshulam-dd-failure`

## Production-Side Harness (New Track)

### Objective
Run sandbox tests while production keeps running, and expose live results in HTML.

### Configuration (KISS: 2 flags)
- `PaymentsHarness:Enabled` (default `false`)
- `PaymentsHarness:Token` (random token)

Configured in:
- `appsettings.json`
- `appsettings.Development.json`

### New Components
- `Services/PaymentsHarnessOptions.cs`
- `Services/PaymentsHarnessStore.cs` (in-memory runs/events, recurring table feed)
- `Services/PaymentsHarnessSandboxClient.cs` (sandbox-only API calls)

### DI
- Registered in `Startup.cs`:
  - options binding
  - singleton harness store
  - sandbox client + `HttpClient`

### Harness Endpoints
In `Controllers/MeshulamController.cs`:
- `GET /payments-harness?token=...` (HTML summary page)
- `POST /payments-harness/start-yearly`
- `POST /payments-harness/start-monthly`
- `POST /payments-harness/cancel-latest`

### Webhook Integration
Harness hooks into existing fixed webhook URLs (no URL changes):
- `/meshulam-response`
- `/meshulam-dd-success`
- `/meshulam-dd-failure`

Harness-classified events are recorded in memory and returned `200`.
Production-classified events continue existing DB/membership behavior.

## Final Parity Rule (Important)
Harness processing now follows the same decision flow as production before persistence:

- `/meshulam-response` harness executes:
  - payload validity checks
  - success-status filtering
  - duplicate/idempotency checks
  - `ApproveTransaction` retry logic

- `/meshulam-dd-success` harness executes:
  - payload validity checks
  - success-status filtering
  - duplicate/idempotency checks
  - conditional provider validation (`getTransactionInfo`)

- `/meshulam-dd-failure` harness mirrors duplicate/idempotency behavior.

Intentional divergence is only at DB mutation points:
- no `_db.Transactions.Add(...)`
- no `_db.SaveChanges(...)`
- no membership create/update
- no temp-member cleanup writes

Additional parity support:
- Harness store tracks seen transaction tokens in-memory for duplicate behavior parity.
- Added sandbox fallback classification (`getTransactionInfo` against sandbox credentials/page codes):
  - if callback is not already linked by harness correlation but validates in sandbox, it is treated as harness.

## Runtime Characteristics
- In-memory harness data persists while the app process runs.
- Data resets on restart/redeploy/instance recycle.
- Production is not replaced; harness runs alongside it when enabled.

## Files Touched Across This Work
- `Controllers/MeshulamController.cs`
- `Controllers/CustomLoginController.cs`
- `Controllers/MembershipsController.cs`
- `Services/MeshulamService.cs`
- `Services/MeshulamTransactionMapper.cs`
- `Services/MeshulamWebhookPayload.cs`
- `Services/MeshulamWebhookPayloadReader.cs`
- `Services/PaymentsHarnessOptions.cs`
- `Services/PaymentsHarnessStore.cs`
- `Services/PaymentsHarnessSandboxClient.cs`
- `Startup.cs`
- `Middleware/CaptureRedirectMiddleware.cs`
- `appsettings.json`
- `appsettings.Development.json`

## 2026-02-20 Production Regression Hotfix

### Symptoms Reported
- Real production callbacks were visible in harness raw capture but transactions/memberships were not updated.
- Cancel direct-debit from backoffice/member area failed with Meshulam update error.

### Root Causes Fixed
1. JSON webhook mapping produced `null` in required DB string columns.
   - `Transactions` table columns are non-null for many string fields.
   - Real callbacks can omit fields like `cardExp`/`cardToken`.
   - Save failed at DB layer, causing callback processing failure.

2. Monthly detection used `PaymentType` before `DirectDebitId`.
   - Older data can have misleading `PaymentType` values.
   - This could route cancel requests with the wrong page code (yearly instead of monthly).

3. Cancel transaction anchor could prefer webhook-synthetic rows.
   - Cancellation now prioritizes records with provider-grade identifiers.

### Code Changes (Hotfix Set)
- `Services/MeshulamTransactionMapper.cs`
  - JSON mapping now always fills required string fields with non-null values (`string.Empty` fallback).
  - Keeps default success status (`"שולם"`) when missing.
  - Adds `cardToken` fallback to non-null string.

- `Services/MeshulamService.cs`
  - `IsMonthlyTransaction` now prioritizes `DirectDebitId > 0` before `PaymentType`.

- `Controllers/MembershipsController.cs`
  - Cancel flow now ranks candidate transactions by provider identifier quality (`transactionId`/real token), then by recency.

- `Controllers/CustomLoginController.cs`
  - Same cancel-anchor ranking logic as backoffice controller.

- `MO6.Tests/ProductionWebhookPayloadRegressionTests.cs`
  - Added guard assertions for non-null required string fields from production-style JSON payloads.

### Additional hardening (same session)
- `Services/MeshulamWebhookPayloadReader.cs`
  - Query-string values are now merged into normalized webhook lookup (in addition to body/form).
- `Services/MeshulamWebhookPayload.cs`
  - Payload value map now accepts extra key/value sources and normalizes them the same way.
  - Query values are fallback-only and do not override body/form values.
- `Controllers/MeshulamController.cs`
  - Success webhook mapping now enriches mapped transactions from payload fallback keys (body/form/query):
    - `directDebitId` / `DirectDebit`
    - `processToken` / `Process`
    - `transactionId` / `transactionToken`
    - payer/email/phone/fullName/status/payment fields when missing
  - This prevents partial payload shapes from skipping DB/membership updates.
- `MO6.Tests/MeshulamWebhookPayloadReaderTests.cs`
  - Added test validating query-string field normalization and lookup.
  - Added test proving query values do not override body values.

### Final cancel hardening
- `Services/MeshulamService.cs`
  - `UpdateDirectDebit` now accepts `asmachta` as a valid identifier path (not only transactionId/token/directDebitId).
- `Controllers/MembershipsController.cs`
  - Cancel candidate selection no longer hard-requires `DirectDebitId` in the base query.
  - Ranking now prefers:
    1) rows with `DirectDebitId`
    2) rows with provider `TransactionId`
    3) rows with non-synthetic provider token
    4) newest timestamp
- `Controllers/CustomLoginController.cs`
  - Same cancel selection/ranking hardening as backoffice flow.

### New DB commit + cleanup integration test
- `MO6.Tests/WebhookDbCommitCleanupIntegrationTests.cs`
  - Replays a curl-style approved webhook payload.
  - Parses request via `MeshulamWebhookPayloadReader` and maps via `MeshulamTransactionMapper`.
  - Commits transaction into an isolated SQLite test DB.
  - Verifies row was persisted.
  - Deletes inserted test row and verifies cleanup completed.
- `MO6.Tests/MO6.Tests.csproj`
  - Added `Microsoft.EntityFrameworkCore.Sqlite` for this isolated persistence integration test.

## Build / Validation Notes
- `dotnet test MO6.Tests/MO6.Tests.csproj -v minimal` passes (`17/17`).
- `dotnet build MO6.csproj -v minimal` passes (warnings only, no errors).
