# MO6

Umbraco/.NET 8 web application with Meshulam payment integration (regular + recurring), including a sandbox payments harness for safe cloud-side verification.

## Main Payment Webhooks
Provider posts to these fixed endpoints:

- Regular payment: `POST /meshulam-response`
- Recurring success: `POST /meshulam-dd-success`
- Recurring failure: `POST /meshulam-dd-failure`

The webhook reader supports both JSON and form payload shapes.

## Payments Harness (Sandbox)
Harness runs **alongside production flow** and is controlled by two flags:

- `PaymentsHarness:Enabled` (default: `false`)
- `PaymentsHarness:Token` (required for harness UI/actions)

Configured in:

- `appsettings.json`
- `appsettings.Development.json`

Harness UI:

- `GET /payments-harness?token=<token>`

Harness actions:

- `POST /payments-harness/start-yearly`
- `POST /payments-harness/start-monthly`
- `POST /payments-harness/cancel-latest`

Behavior:

- Uses sandbox Meshulam credentials/page codes.
- Sandbox credential keys:
  - `Meshulam:ForceSandboxMode` (set `true` to force full site payment flow to sandbox endpoint + sandbox credentials)
  - `Meshulam:SandboxAddress`
  - `Meshulam:SandboxUserID`
  - `Meshulam:SandboxYearlyPageCode`
  - `Meshulam:SandboxMonthlyPageCode`
- Uses the same decision logic as production webhook handling.
- Does **not** mutate DB data for harness-classified callbacks.
- Stores harness runs/events in-memory (reset on app restart).
- Recurring monthly sandbox callbacks are captured when they hit the fixed webhook paths.
  - Harness classification uses `cField1`/process/direct-debit/token correlation and sandbox validation fallback.
  - In addition, a raw callback accumulator records **every** webhook request on:
    - `/meshulam-response`
    - `/meshulam-dd-success`
    - `/meshulam-dd-failure`
    regardless of classification result.

### Raw Callback Viewer and Replay
`/payments-harness` includes a **Raw Webhook Capture (All)** table:

- One line per request.
- Click a line to open full details:
  - method/path/query/content-type
  - request headers snapshot
  - raw body
  - replay-ready `curl` command for local simulation

Replay note:

1. Start the app locally.
2. Open `/payments-harness?token=<token>` in the cloud environment.
3. In **Raw Webhook Capture (All)**, click the row you want.
4. Copy the generated `curl` command from **Replay Curl (local)**.
5. Run it locally (adjust base URL/port if needed).

This reproduces the inbound webhook request as closely as possible from captured data.

## Local Development
Run:

```bash
dotnet build MO6.csproj -v minimal
dotnet run --project MO6.csproj
```

Default launch settings use Development environment for normal local profiles.

## Docker
Build:

```bash
docker build -t mo6:local .
```

Run:

```bash
docker run --name mo6-local-test -d -p 8080:80 mo6:local
```

## Notes
- Current payment/harness implementation details are documented in `SESSION_2026-02-16_payments_fix.md`.
- Production JSON webhook mapping now guards all required `Transactions` string fields against null values (important for callbacks that omit optional card fields).
- Webhook processing now also considers query-string callback values as fallbacks (for providers that split fields between JSON/form body and query params).
- Direct-debit cancel flow prioritizes transactions with provider-grade identifiers and treats `DirectDebitId` as monthly-first for page-code routing.
- If you see a generic site locally, check for port conflicts (for example Apache/Nginx already bound to port 80).
