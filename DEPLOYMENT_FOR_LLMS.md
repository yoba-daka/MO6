# Deployment Notes for LLM Agents

This repo deploys production from the `main` branch through GitHub Actions.

## Rule of Thumb

To deploy production, commit the intended changes and push them to `main`.

Do not manually run the old local Docker/tag/push flow unless the GitHub Actions workflow is broken or the user explicitly asks for a manual deploy.

## Current Production Workflow

Workflow file:

```text
.github/workflows/deploy-main.yml
```

Triggers:

- Push to `main`
- Manual `workflow_dispatch` from GitHub Actions

The workflow:

1. Checks out the repo.
2. Sets up .NET 8.
3. Runs tests:

   ```bash
   dotnet test MO6.sln --configuration Release --filter "FullyQualifiedName!~CapturedRequests"
   ```

4. Logs into Azure using GitHub OIDC.
5. Logs into Azure Container Registry `moshe`.
6. Builds the Docker image from `Dockerfile`.
7. Pushes:

   ```text
   moshe.azurecr.io/mo6:latest
   moshe.azurecr.io/mo6:<github-sha>
   ```

8. Restarts Azure Web App `mo6` in resource group `mo6`.

## Azure/GitHub Identity

The workflow uses GitHub environment `production`.

Required GitHub environment secrets:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
```

The Azure federated credential subject must match:

```text
repo:yoba-daka/MO6:environment:production
```

The Azure identity needs:

- `AcrPush` on ACR `moshe`
- permission to restart the web app, currently handled by `Contributor` on resource group `mo6`

## CapturedRequests Tests

Two tests depend on a local ignored file:

```text
requests.txt
```

Those tests are:

```text
MO6.Tests/CapturedRequestsReplayIntegrationTests.cs
MO6.Tests/CapturedRequestsModelMappingIntegrationTests.cs
```

`requests.txt` contains captured Meshulam webhook requests and is intentionally ignored by Git:

```text
/requests.txt
```

Do not commit the raw file. It may contain real webhook/payment/customer data.

Because GitHub Actions cannot access that local ignored fixture, the deploy workflow excludes tests whose fully qualified name contains `CapturedRequests`. The normal CI deploy test count is currently 81 tests.

A better future fix would be to create a sanitized committed fixture under `MO6.Tests/Fixtures/` and update those two tests to use it, then remove the workflow filter.

## Monitoring Deploys

Useful commands:

```powershell
gh run list --repo yoba-daka/MO6 --limit 5
gh run watch --repo yoba-daka/MO6
```

For a specific run:

```powershell
gh run view <run-id> --repo yoba-daka/MO6
gh run view <run-id> --repo yoba-daka/MO6 --log-failed
```

Azure web app logs:

```powershell
az webapp log tail --resource-group mo6 --name mo6
```

ACR tags:

```powershell
az acr repository show-tags --name moshe --repository mo6 --output table
```

## Manual Fallback

The old manual flow, kept only as a fallback, is:

```powershell
docker build -t mo6:latest .
az acr login --name moshe
docker tag mo6:latest moshe.azurecr.io/mo6:latest
docker push moshe.azurecr.io/mo6:latest
az webapp restart --resource-group mo6 --name mo6
```

Prefer the GitHub Actions workflow for normal deployments because it gives a visible run log, uses the configured Azure identity, tags the image by commit SHA, and avoids relying on local Docker state.
