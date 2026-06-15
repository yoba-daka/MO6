# Payments Isolated Test

Standalone Grow/Meshulam sandbox runner for creating a monthly payment, receiving the `notifyUrl` callback, and cancelling the latest simulated membership with `updateDirectDebit`.

This does not reference or run the MO6 web app and does not write to the MO6 database. State is kept in memory until the process exits.

## Run

```powershell
dotnet run --project tools\PaymentsIsolatedTest -- --port 5099
```

Open the local URL printed by the tool.

## Dev Tunnel

Grow needs a public HTTPS callback URL. Start a tunnel to the same local port:

```powershell
devtunnel host -p 5099 --allow-anonymous --host-header unchanged
```

Paste the public `https://...devtunnels.ms` URL into the tool's `Public URL` field before creating the sandbox monthly payment. The tool sends Grow:

- `successUrl`: `{publicUrl}/?token=...`
- `cancelUrl`: `{publicUrl}/?token=...`
- `notifyUrl`: `{publicUrl}/notify`

## Configuration

The runner reads sandbox settings from the repo `appsettings.json` / `appsettings.Development.json` when present:

- `PaymentsHarness:Token`
- `Meshulam:SandboxAddress`
- `Meshulam:SandboxUserID`
- `Meshulam:SandboxMonthlyPageCode`

You can override with command-line flags:

```powershell
dotnet run --project tools\PaymentsIsolatedTest -- --port 5099 --public-url https://example.devtunnels.ms --token local-test
```

Windows-friendly env vars are also supported, for example:

- `MO6_PAYMENTS_TEST_PORT`
- `MO6_PAYMENTS_TEST_PUBLIC_URL`
- `MO6_PAYMENTS_TEST_TOKEN`
- `MO6_PAYMENTS_TEST_MESHULAM__SANDBOXADDRESS`
- `MO6_PAYMENTS_TEST_MESHULAM__SANDBOXUSERID`
- `MO6_PAYMENTS_TEST_MESHULAM__SANDBOXMONTHLYPAGECODE`
