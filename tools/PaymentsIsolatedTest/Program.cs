using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

ClearInheritedAspNetPortSettings();

var config = TestConfig.Load(args);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = Array.Empty<string>(),
    ContentRootPath = AppContext.BaseDirectory
});

builder.WebHost.UseSetting("http_ports", string.Empty);
builder.WebHost.UseSetting("https_ports", string.Empty);
builder.WebHost.UseUrls($"http://127.0.0.1:{config.Port}", $"http://[::1]:{config.Port}");
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<TestStore>();
builder.Services.AddHttpClient<SandboxClient>();

var app = builder.Build();

app.MapGet("/", async (HttpContext ctx, TestConfig cfg, TestStore store) =>
{
    if (!await HasAccessAsync(ctx, cfg))
    {
        return Results.Unauthorized();
    }

    var message = ctx.Request.Query["message"].ToString();
    var isError = string.Equals(ctx.Request.Query["isError"].ToString(), "1", StringComparison.Ordinal);
    return Results.Content(RenderPage(ctx, cfg, store, message, isError), "text/html; charset=utf-8");
});

app.MapMethods("/start-monthly", new[] { "GET", "POST" }, async (HttpContext ctx, TestConfig cfg, TestStore store, SandboxClient client) =>
{
    if (!await HasAccessAsync(ctx, cfg))
    {
        return Results.Unauthorized();
    }

    var values = await ReadRequestValuesAsync(ctx.Request);
    var amount = NumberParsing.ParseFloat(Get(values, "amount")) ?? 35f;
    var email = Get(values, "email");
    var fullName = Get(values, "fullName");
    var phone = Get(values, "phone");
    var publicUrl = ResolvePublicBaseUrl(ctx, cfg, Get(values, "publicUrl"));

    if (!LooksPublicHttps(publicUrl))
    {
        return RedirectToRoot(cfg, "Public URL must be an external HTTPS URL before creating a Grow payment", true);
    }

    if (amount <= 0)
    {
        return RedirectToRoot(cfg, "Amount must be positive", true);
    }

    email = string.IsNullOrWhiteSpace(email)
        ? $"isolated{Guid.NewGuid():N}".Substring(0, 22) + "@mo6.co.il"
        : email.Trim();
    fullName = string.IsNullOrWhiteSpace(fullName) ? "Test User" : fullName.Trim();
    phone = string.IsNullOrWhiteSpace(phone) ? "0500000000" : phone.Trim();

    var run = store.CreateRun(email, fullName, phone, amount);
    var result = await client.CreateMonthlyPaymentAsync(amount, publicUrl, run.CField1, cfg.AccessToken, email, fullName, phone);

    if (!result.Success)
    {
        store.MarkCreateFailed(run.RunId, result.Error, result.RawRequest, result.RawResponse);
        return RedirectToRoot(cfg, $"createPaymentProcess failed: {result.Error}", true);
    }

    store.MarkCreated(run.RunId, result.CheckoutUrl, result.RawRequest, result.RawResponse);
    return RedirectToRoot(cfg, $"Monthly sandbox payment created: {run.RunId}", false);
});

app.MapMethods("/notify", new[] { "GET", "POST" }, async (HttpContext ctx, TestStore store) =>
{
    var payload = await PayloadReader.ReadAsync(ctx.Request);
    var transaction = TransactionMapper.Map(payload);
    var run = store.FindRun(payload, transaction);

    if (run == null)
    {
        store.RecordUnmatched(payload, transaction);
        return Results.Ok("unmatched callback recorded");
    }

    var success = TransactionRules.IsSuccessful(transaction) && TransactionRules.IsMonthly(transaction);
    var message = success
        ? "Sandbox callback created/extended simulated membership"
        : "Callback was not a successful monthly payment";

    store.RecordWebhook(run.RunId, payload, transaction, success, message);
    return Results.Ok("ok");
});

app.MapMethods("/cancel-latest", new[] { "GET", "POST" }, async (HttpContext ctx, TestConfig cfg, TestStore store, SandboxClient client) =>
{
    if (!await HasAccessAsync(ctx, cfg))
    {
        return Results.Unauthorized();
    }

    var run = store.FindLatestCancelableRun();
    if (run == null)
    {
        return RedirectToRoot(cfg, "No active simulated monthly membership with provider identifiers was found", true);
    }

    var result = await client.CancelMonthlyPaymentAsync(
        run.TransactionId!.Value,
        run.TransactionToken,
        run.Asmachta,
        run.DirectDebitId,
        run.Membership?.Email ?? run.TestEmail);

    store.RecordCancellation(run.RunId, result);
    return RedirectToRoot(
        cfg,
        result.ProductionParserSuccess
            ? "updateDirectDebit parsed as success; simulated membership marked inactive"
            : "updateDirectDebit parsed as failure; simulated membership left active",
        !result.ProductionParserSuccess);
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("Isolated Grow payments test runner is listening.");
    Console.WriteLine($"Local URL: http://localhost:{config.Port}/?token={Uri.EscapeDataString(config.AccessToken)}");
    Console.WriteLine("Use a public HTTPS Dev Tunnel URL as Public URL before creating a payment.");
    Console.WriteLine();
});

await app.RunAsync();

static void ClearInheritedAspNetPortSettings()
{
    foreach (var key in new[]
             {
                 "ASPNETCORE_URLS",
                 "ASPNETCORE_HTTP_PORTS",
                 "ASPNETCORE_HTTPS_PORTS",
                 "DOTNET_URLS",
                 "DOTNET_HTTP_PORTS",
                 "DOTNET_HTTPS_PORTS"
             })
    {
        Environment.SetEnvironmentVariable(key, null);
    }
}

static async Task<bool> HasAccessAsync(HttpContext ctx, TestConfig cfg)
{
    if (string.IsNullOrWhiteSpace(cfg.AccessToken))
    {
        return false;
    }

    var candidate = ctx.Request.Query["token"].ToString();
    if (string.IsNullOrWhiteSpace(candidate))
    {
        candidate = ctx.Request.Headers["X-Payments-Test-Token"].ToString();
    }

    if (string.IsNullOrWhiteSpace(candidate))
    {
        var referer = ctx.Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            candidate = QueryHelpers.ParseQuery(uri.Query).TryGetValue("token", out var token)
                ? token.ToString()
                : string.Empty;
        }
    }

    if (string.IsNullOrWhiteSpace(candidate) && ctx.Request.HasFormContentType)
    {
        var form = await ctx.Request.ReadFormAsync();
        candidate = form.TryGetValue("token", out var formToken) ? formToken.ToString() : string.Empty;
    }

    return string.Equals(candidate?.Trim(), cfg.AccessToken.Trim(), StringComparison.Ordinal);
}

static async Task<Dictionary<string, string>> ReadRequestValuesAsync(HttpRequest request)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pair in request.Query)
    {
        values[pair.Key] = pair.Value.ToString();
    }

    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        foreach (var pair in form)
        {
            values[pair.Key] = pair.Value.ToString();
        }
    }

    return values;
}

static IResult RedirectToRoot(TestConfig cfg, string message, bool isError)
{
    var url = $"/?token={Uri.EscapeDataString(cfg.AccessToken)}&message={Uri.EscapeDataString(message)}&isError={(isError ? "1" : "0")}";
    return Results.Redirect(url);
}

static string RenderPage(HttpContext ctx, TestConfig cfg, TestStore store, string message, bool isError)
{
    var snapshot = store.GetSnapshot();
    var detectedPublicUrl = ResolvePublicBaseUrl(ctx, cfg, string.Empty);
    var html = new StringBuilder();

    html.Append("<!doctype html><html><head><meta charset='utf-8'>");
    html.Append("<title>Isolated Grow Payments Test</title>");
    html.Append("<style>");
    html.Append("body{font-family:Arial,sans-serif;padding:16px;color:#222}");
    html.Append("table{border-collapse:collapse;width:100%;margin:12px 0}");
    html.Append("th,td{border:1px solid #ddd;padding:6px;font-size:12px;vertical-align:top}");
    html.Append("th{background:#f5f5f5}");
    html.Append(".box{display:flex;gap:8px;flex-wrap:wrap;align-items:end}");
    html.Append(".pill{background:#f3f3f3;padding:4px 8px;border-radius:999px}");
    html.Append(".ok{color:#0a7a0a}.err{color:#a40000}");
    html.Append("input{padding:5px;margin:2px 0;min-width:180px}");
    html.Append("button,a.button{display:inline-block;padding:6px 10px;border:1px solid #777;background:#f7f7f7;color:#111;text-decoration:none}");
    html.Append("pre{white-space:pre-wrap;max-height:260px;overflow:auto;background:#fafafa;padding:8px}");
    html.Append("</style></head><body>");
    html.Append("<h2>Isolated Grow Payments Test</h2>");
    html.Append("<div class='box'>");
    html.Append("<span class='pill'>Local port: ").Append(cfg.Port.ToString(CultureInfo.InvariantCulture)).Append("</span>");
    html.Append("<span class='pill'>Detected public URL: ").Append(H(detectedPublicUrl)).Append("</span>");
    html.Append("<span class='pill'>UTC: ").Append(H(DateTime.UtcNow.ToString("u"))).Append("</span>");
    html.Append("</div>");

    if (!string.IsNullOrWhiteSpace(message))
    {
        html.Append("<p class='").Append(isError ? "err" : "ok").Append("'>").Append(H(message)).Append("</p>");
    }

    html.Append("<h3>Actions</h3>");
    html.Append("<form method='post' action='/start-monthly'>");
    html.Append("<input type='hidden' name='token' value='").Append(H(cfg.AccessToken)).Append("'>");
    html.Append("<div class='box'>");
    html.Append("<label>Public URL<br><input name='publicUrl' value='").Append(H(detectedPublicUrl)).Append("' placeholder='https://...devtunnels.ms'></label>");
    html.Append("<label>Amount<br><input name='amount' value='35'></label>");
    html.Append("<label>Email<br><input name='email' value=''></label>");
    html.Append("<label>Full name<br><input name='fullName' value='Test User'></label>");
    html.Append("<label>Phone<br><input name='phone' value='0500000000'></label>");
    html.Append("<button type='submit'>Create Sandbox Monthly Payment</button>");
    html.Append("<a class='button' href='/cancel-latest?token=").Append(H(cfg.AccessToken)).Append("'>Cancel Latest Active Simulated Membership</a>");
    html.Append("<a class='button' href='/?token=").Append(H(cfg.AccessToken)).Append("'>Refresh</a>");
    html.Append("</div></form>");

    html.Append("<p>Grow notifyUrl used by this tool: <code>").Append(H(TrimEndSlash(detectedPublicUrl))).Append("/notify</code></p>");

    html.Append("<h3>Runs</h3>");
    html.Append("<table><thead><tr><th>Run</th><th>Status</th><th>Checkout</th><th>Simulated Membership</th><th>Provider IDs</th><th>Events</th></tr></thead><tbody>");
    foreach (var run in snapshot.Runs)
    {
        html.Append("<tr>");
        html.Append("<td><b>").Append(H(run.RunId)).Append("</b><br>")
            .Append(H(run.CreatedUtc.ToString("u"))).Append("<br>")
            .Append("cField1: ").Append(H(run.CField1)).Append("<br>")
            .Append("email: ").Append(H(run.TestEmail)).Append("</td>");
        html.Append("<td>").Append(H(run.Status)).Append("<br>").Append(H(run.Message)).Append("</td>");
        html.Append("<td>");
        if (!string.IsNullOrWhiteSpace(run.CheckoutUrl))
        {
            html.Append("<a target='_blank' href='").Append(H(run.CheckoutUrl)).Append("'>Open checkout</a>");
        }
        html.Append("</td>");
        html.Append("<td>");
        if (run.Membership == null)
        {
            html.Append("none");
        }
        else
        {
            html.Append("email: ").Append(H(run.Membership.Email)).Append("<br>")
                .Append("monthly: ").Append(run.Membership.IsMonthly ? "true" : "false").Append("<br>")
                .Append("active: ").Append(run.Membership.IsMonthlyActive ? "true" : "false").Append("<br>")
                .Append("expiration UTC: ").Append(H(run.Membership.ExpirationUtc.ToString("u"))).Append("<br>")
                .Append("transactions: ").Append(H(run.Membership.Transactions));
        }
        html.Append("</td>");
        html.Append("<td>")
            .Append("transactionId: ").Append(H(run.TransactionId?.ToString(CultureInfo.InvariantCulture))).Append("<br>")
            .Append("transactionToken: ").Append(H(run.TransactionToken)).Append("<br>")
            .Append("directDebitId: ").Append(H(run.DirectDebitId?.ToString(CultureInfo.InvariantCulture))).Append("<br>")
            .Append("asmachta: ").Append(H(run.Asmachta)).Append("<br>")
            .Append("process: ").Append(H(run.ProcessId?.ToString(CultureInfo.InvariantCulture))).Append(" / ").Append(H(run.ProcessToken))
            .Append("</td>");
        html.Append("<td>");
        foreach (var evt in run.Events)
        {
            html.Append("<details><summary>")
                .Append(H(evt.CreatedUtc.ToString("u"))).Append(" ")
                .Append(H(evt.Kind)).Append(" ")
                .Append(H(evt.Result)).Append(" ")
                .Append(H(evt.Message))
                .Append("</summary>");
            html.Append("<div>status: ").Append(H(evt.Status)).Append(" / ").Append(H(evt.StatusCode?.ToString(CultureInfo.InvariantCulture))).Append("</div>");
            html.Append("<div>amount: ").Append(H(evt.Amount?.ToString(CultureInfo.InvariantCulture))).Append("</div>");
            html.Append("<div>transaction: ").Append(H(evt.TransactionId?.ToString(CultureInfo.InvariantCulture))).Append(" / ").Append(H(evt.TransactionToken)).Append("</div>");
            html.Append("<div>directDebitId: ").Append(H(evt.DirectDebitId?.ToString(CultureInfo.InvariantCulture))).Append("</div>");
            AppendPre(html, "Raw request", evt.RawRequest);
            AppendPre(html, "Raw response", evt.RawResponse);
            AppendPre(html, "Raw webhook body", evt.RawBody);
            html.Append("</details>");
        }
        html.Append("</td>");
        html.Append("</tr>");
    }
    html.Append("</tbody></table>");

    if (snapshot.UnmatchedEvents.Count > 0)
    {
        html.Append("<h3>Unmatched Callbacks</h3>");
        foreach (var evt in snapshot.UnmatchedEvents)
        {
            html.Append("<details><summary>").Append(H(evt.CreatedUtc.ToString("u"))).Append(" ").Append(H(evt.Message)).Append("</summary>");
            AppendPre(html, "Raw webhook body", evt.RawBody);
            html.Append("</details>");
        }
    }

    html.Append("</body></html>");
    return html.ToString();
}

static void AppendPre(StringBuilder html, string label, string value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        html.Append("<b>").Append(H(label)).Append("</b><pre>").Append(H(value)).Append("</pre>");
    }
}

static string ResolvePublicBaseUrl(HttpContext ctx, TestConfig cfg, string? requestOverride)
{
    if (!string.IsNullOrWhiteSpace(requestOverride))
    {
        return TrimEndSlash(requestOverride.Trim());
    }

    if (!string.IsNullOrWhiteSpace(cfg.PublicUrl))
    {
        return TrimEndSlash(cfg.PublicUrl);
    }

    var scheme = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(scheme))
    {
        scheme = ctx.Request.Scheme;
    }

    var host = ctx.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(host))
    {
        host = ctx.Request.Host.Value;
    }

    if (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) &&
        (host.Contains("devtunnels.ms", StringComparison.OrdinalIgnoreCase) ||
         host.Contains("ngrok", StringComparison.OrdinalIgnoreCase)))
    {
        scheme = "https";
    }

    return TrimEndSlash($"{scheme}://{host}");
}

static bool LooksPublicHttps(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
           !uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}

static string TrimEndSlash(string value) => (value ?? string.Empty).Trim().TrimEnd('/');
static string Get(IDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : string.Empty;
static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

sealed class TestConfig
{
    public int Port { get; init; } = 5099;
    public string PublicUrl { get; init; } = string.Empty;
    public string AccessToken { get; init; } = "local-test";
    public string SandboxAddress { get; init; } = "https://sandbox.meshulam.co.il/api/light/server/1.0/";
    public string SandboxUserId { get; init; } = "530c0ed0c411ce71";
    public string SandboxMonthlyPageCode { get; init; } = "0614c4d8b7a0";

    public static TestConfig Load(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var fileSettings = ReadFileSettings(repoRoot);
        var argSettings = ParseArgs(args);

        string Pick(string key, string fallback)
        {
            foreach (var envKey in GetEnvKeys(key))
            {
                var env = Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrWhiteSpace(env))
                {
                    return env;
                }
            }

            if (argSettings.TryGetValue(key, out var arg) && !string.IsNullOrWhiteSpace(arg))
            {
                return arg;
            }

            if (fileSettings.TryGetValue(key, out var fileValue) && !string.IsNullOrWhiteSpace(fileValue))
            {
                return fileValue;
            }

            return fallback;
        }

        return new TestConfig
        {
            Port = int.TryParse(Pick("port", "5099"), out var port) ? port : 5099,
            PublicUrl = Pick("public-url", string.Empty),
            AccessToken = Pick("token", Pick("PaymentsHarness:Token", "local-test")),
            SandboxAddress = NormalizeBaseAddress(Pick("Meshulam:SandboxAddress", "https://sandbox.meshulam.co.il/api/light/server/1.0/")),
            SandboxUserId = Pick("Meshulam:SandboxUserID", "530c0ed0c411ce71"),
            SandboxMonthlyPageCode = Pick("Meshulam:SandboxMonthlyPageCode", "0614c4d8b7a0")
        };
    }

    private static string NormalizeBaseAddress(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : normalized + "/";
    }

    private static IEnumerable<string> GetEnvKeys(string key)
    {
        var normalized = key
            .Replace(":", "__", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .ToUpperInvariant();

        yield return "MO6_PAYMENTS_TEST_" + normalized;
        yield return "MO6_PAYMENTS_TEST_" + key.ToUpperInvariant();
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var trimmed = arg[2..];
            var sep = trimmed.IndexOf('=');
            if (sep >= 0)
            {
                map[trimmed[..sep]] = trimmed[(sep + 1)..];
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                map[trimmed] = args[++i];
            }
            else
            {
                map[trimmed] = "true";
            }
        }

        return map;
    }

    private static Dictionary<string, string> ReadFileSettings(string repoRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[]
                 {
                     Path.Combine(repoRoot, "appsettings.json"),
                     Path.Combine(repoRoot, "appsettings.Development.json")
                 })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            ReadSectionValue(text, "PaymentsHarness", "Token", map);
            ReadSectionValue(text, "Meshulam", "SandboxAddress", map);
            ReadSectionValue(text, "Meshulam", "SandboxUserID", map);
            ReadSectionValue(text, "Meshulam", "SandboxMonthlyPageCode", map);
        }

        return map;
    }

    private static void ReadSectionValue(string text, string section, string key, Dictionary<string, string> map)
    {
        var sectionMatch = Regex.Match(text, $"\"{Regex.Escape(section)}\"\\s*:\\s*\\{{(?<body>.*?)\\n\\s*\\}}", RegexOptions.Singleline);
        if (!sectionMatch.Success)
        {
            return;
        }

        var keyMatch = Regex.Match(sectionMatch.Groups["body"].Value, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<value>[^\"]*)\"", RegexOptions.Singleline);
        if (keyMatch.Success)
        {
            map[$"{section}:{key}"] = keyMatch.Groups["value"].Value;
            if (section == "PaymentsHarness" && key == "Token")
            {
                map["token"] = keyMatch.Groups["value"].Value;
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MO6.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

sealed class SandboxClient
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;

    public SandboxClient(HttpClient httpClient, TestConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(_config.SandboxAddress);
    }

    public async Task<CreatePaymentResult> CreateMonthlyPaymentAsync(
        float amount,
        string publicUrl,
        string cField1,
        string accessToken,
        string email,
        string fullName,
        string phone)
    {
        var requestData = new Dictionary<string, string>
        {
            { "userId", _config.SandboxUserId },
            { "sum", amount.ToString(CultureInfo.InvariantCulture) },
            { "maxPaymentNum", "1" },
            { "successUrl", $"{publicUrl}/?token={Uri.EscapeDataString(accessToken)}" },
            { "cancelUrl", $"{publicUrl}/?token={Uri.EscapeDataString(accessToken)}" },
            { "notifyUrl", $"{publicUrl}/notify" },
            { "description", "MO6 isolated monthly cancellation test" },
            { "pageField[email]", email },
            { "pageField[fullName]", fullName },
            { "pageField[phone]", phone },
            { "pageCode", _config.SandboxMonthlyPageCode },
            { "cField1", cField1 }
        };

        var rawRequest = BuildRawRequest(requestData);
        var rawResponse = await PostFormAsync("createPaymentProcess/", requestData);
        var result = ParseCreatePaymentResponse(rawResponse);
        result.RawRequest = rawRequest;
        result.RawResponse = rawResponse;
        return result;
    }

    public async Task<CancelResult> CancelMonthlyPaymentAsync(
        int transactionId,
        string transactionToken,
        string asmachta,
        int? directDebitId,
        string email)
    {
        var requestData = new Dictionary<string, string>
        {
            { "userId", _config.SandboxUserId },
            { "pageCode", _config.SandboxMonthlyPageCode },
            { "transactionId", transactionId.ToString(CultureInfo.InvariantCulture) },
            { "transactionToken", transactionToken },
            { "asmachta", asmachta },
            { "changeStatus", "2" }
        };

        if (directDebitId.HasValue && directDebitId.Value > 0)
        {
            requestData["directDebitId"] = directDebitId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            requestData["email"] = email;
        }

        var rawRequest = BuildRawRequest(requestData);
        var rawResponse = await PostFormAsync("updateDirectDebit/", requestData);
        var result = ParseCancelResponse(rawResponse);
        result.RawRequest = rawRequest;
        result.RawResponse = rawResponse;
        return result;
    }

    private async Task<string> PostFormAsync(string endpoint, Dictionary<string, string> requestData)
    {
        using var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(requestData));
        return await response.Content.ReadAsStringAsync();
    }

    private static CreatePaymentResult ParseCreatePaymentResponse(string raw)
    {
        var result = new CreatePaymentResult();
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            var status = ReadInt(root, "status");
            var err = ReadString(root, "err", "error");
            var url = ReadString(root, "data", "url");
            result.Success = status == 1 && string.IsNullOrWhiteSpace(err) && !string.IsNullOrWhiteSpace(url);
            result.CheckoutUrl = url;
            result.Error = result.Success ? string.Empty : $"status={status}; err={err}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private static CancelResult ParseCancelResponse(string raw)
    {
        var result = new CancelResult();
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            result.Status = ReadInt(root, "status");
            result.Err = ReadString(root, "err", "error");
            result.ChangeStatus = ReadString(root, "data", "changeStatus");
            result.ProviderStatusSuccess = result.Status == 1;
            result.ProductionParserSuccess =
                result.Status == 1 &&
                string.IsNullOrWhiteSpace(result.Err) &&
                (string.IsNullOrWhiteSpace(result.ChangeStatus) ||
                 result.ChangeStatus == "2" ||
                 result.ChangeStatus == "0");
            result.Error = result.ProductionParserSuccess
                ? string.Empty
                : $"status={result.Status}; err={result.Err}; changeStatus={result.ChangeStatus}";
        }
        catch (Exception ex)
        {
            result.ProductionParserSuccess = false;
            result.ProviderStatusSuccess = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private static int ReadInt(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsedNumber))
        {
            return parsedNumber;
        }

        return int.TryParse(ReadElementAsString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string ReadString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return string.Empty;
            }
        }

        return ReadElementAsString(current);
    }

    private static string ReadElementAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static string BuildRawRequest(Dictionary<string, string> requestData)
    {
        return string.Join("&", requestData.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}

sealed class CreatePaymentResult
{
    public bool Success { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string RawRequest { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}

sealed class CancelResult
{
    public bool ProductionParserSuccess { get; set; }
    public bool ProviderStatusSuccess { get; set; }
    public int Status { get; set; }
    public string Err { get; set; } = string.Empty;
    public string ChangeStatus { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string RawRequest { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}

sealed class TestStore
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, TestRun> _runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _cFieldToRun = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _transactionTokenToRun = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _directDebitToRun = new();
    private readonly List<TestEvent> _unmatched = new();

    public TestRun CreateRun(string email, string fullName, string phone, float amount)
    {
        var run = new TestRun
        {
            RunId = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            TestEmail = email,
            FullName = fullName,
            Phone = phone,
            Amount = amount
        };
        run.CField1 = "ISOLATED_" + run.RunId;

        lock (_gate)
        {
            _runs[run.RunId] = run;
            _cFieldToRun[run.CField1] = run.RunId;
        }

        return Clone(run);
    }

    public void MarkCreated(string runId, string checkoutUrl, string rawRequest, string rawResponse)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var run))
            {
                return;
            }

            run.CheckoutUrl = checkoutUrl;
            run.Status = "Awaiting sandbox payment";
            run.UpdatedUtc = DateTime.UtcNow;
            AddEvent(run, new TestEvent
            {
                CreatedUtc = DateTime.UtcNow,
                Kind = "createPaymentProcess",
                Result = "ok",
                RawRequest = rawRequest,
                RawResponse = rawResponse
            });
        }
    }

    public void MarkCreateFailed(string runId, string message, string rawRequest, string rawResponse)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var run))
            {
                return;
            }

            run.Status = "Create failed";
            run.Message = message;
            run.UpdatedUtc = DateTime.UtcNow;
            AddEvent(run, new TestEvent
            {
                CreatedUtc = DateTime.UtcNow,
                Kind = "createPaymentProcess",
                Result = "failed",
                Message = message,
                RawRequest = rawRequest,
                RawResponse = rawResponse
            });
        }
    }

    public TestRun? FindRun(Payload payload, Transaction transaction)
    {
        lock (_gate)
        {
            var cField = payload.Get("data[customFields][cField1]", "data.customFields.cField1", "data[cField1]", "customFields[cField1]", "customFields.cField1", "cField1");
            if (!string.IsNullOrWhiteSpace(cField) && _cFieldToRun.TryGetValue(cField, out var runIdFromCField) && _runs.TryGetValue(runIdFromCField, out var runFromCField))
            {
                return Clone(runFromCField);
            }

            if (!string.IsNullOrWhiteSpace(cField))
            {
                var runId = ExtractRunId(cField);
                if (!string.IsNullOrWhiteSpace(runId) && _runs.TryGetValue(runId, out var runFromPlainCField))
                {
                    return Clone(runFromPlainCField);
                }
            }

            if (!string.IsNullOrWhiteSpace(transaction.TransactionToken) &&
                _transactionTokenToRun.TryGetValue(transaction.TransactionToken, out var runIdFromToken) &&
                _runs.TryGetValue(runIdFromToken, out var runFromToken))
            {
                return Clone(runFromToken);
            }

            if (transaction.DirectDebitId.HasValue &&
                _directDebitToRun.TryGetValue(transaction.DirectDebitId.Value, out var runIdFromDirectDebit) &&
                _runs.TryGetValue(runIdFromDirectDebit, out var runFromDirectDebit))
            {
                return Clone(runFromDirectDebit);
            }
        }

        return null;
    }

    public void RecordWebhook(string runId, Payload payload, Transaction transaction, bool successful, string message)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var run))
            {
                return;
            }

            UpdateCorrelation(run, transaction);
            if (successful)
            {
                ApplyMembership(run, transaction);
                run.Status = "Simulated membership active";
                run.Message = "Payment callback processed without DB writes";
            }
            else
            {
                run.Status = "Payment callback ignored";
                run.Message = message;
            }

            run.UpdatedUtc = DateTime.UtcNow;
            AddEvent(run, BuildTransactionEvent("payment-webhook", successful ? "ok" : "failed", message, payload, transaction));
        }
    }

    public void RecordUnmatched(Payload payload, Transaction transaction)
    {
        lock (_gate)
        {
            _unmatched.Insert(0, BuildTransactionEvent("payment-webhook", "unmatched", "No run matched cField1/token/directDebitId", payload, transaction));
            if (_unmatched.Count > 50)
            {
                _unmatched.RemoveRange(50, _unmatched.Count - 50);
            }
        }
    }

    public TestRun? FindLatestCancelableRun()
    {
        lock (_gate)
        {
            return _runs.Values
                .Where(x => x.Membership?.IsMonthlyActive == true)
                .Where(x => x.TransactionId.HasValue &&
                            !string.IsNullOrWhiteSpace(x.TransactionToken) &&
                            !string.IsNullOrWhiteSpace(x.Asmachta))
                .OrderByDescending(x => x.UpdatedUtc)
                .Select(Clone)
                .FirstOrDefault();
        }
    }

    public void RecordCancellation(string runId, CancelResult result)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var run))
            {
                return;
            }

            if (result.ProductionParserSuccess && run.Membership != null)
            {
                run.Membership.IsMonthlyActive = false;
                run.Membership.UpdatedUtc = DateTime.UtcNow;
            }

            run.Status = result.ProductionParserSuccess ? "Simulated membership canceled" : "Cancel reported failure";
            run.Message = result.Error;
            run.UpdatedUtc = DateTime.UtcNow;
            AddEvent(run, new TestEvent
            {
                CreatedUtc = DateTime.UtcNow,
                Kind = "updateDirectDebit",
                Result = result.ProductionParserSuccess ? "ok" : "failed",
                Message = result.Error,
                RawRequest = result.RawRequest,
                RawResponse = result.RawResponse
            });
        }
    }

    public TestSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new TestSnapshot
            {
                Runs = _runs.Values.OrderByDescending(x => x.CreatedUtc).Select(Clone).ToList(),
                UnmatchedEvents = _unmatched.Select(CloneEvent).ToList()
            };
        }
    }

    private void ApplyMembership(TestRun run, Transaction transaction)
    {
        var now = DateTime.UtcNow;
        var refs = GetTransactionReferenceCandidates(transaction);
        run.Membership ??= new SimulatedMembership
        {
            Email = string.IsNullOrWhiteSpace(transaction.PayerEmail) ? run.TestEmail : transaction.PayerEmail,
            IsMonthly = true,
            IsMonthlyActive = true,
            ExpirationUtc = now,
            Transactions = string.Empty
        };

        var baseExpiration = run.Membership.ExpirationUtc > now ? run.Membership.ExpirationUtc : now;
        run.Membership.ExpirationUtc = baseExpiration.AddMonths(1).AddHours(1);
        run.Membership.IsMonthly = true;
        run.Membership.IsMonthlyActive = true;
        run.Membership.UpdatedUtc = now;

        var existing = SplitRefs(run.Membership.Transactions).Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (refs.Select(Normalize).All(x => !existing.Contains(x)))
        {
            run.Membership.Transactions += string.Join(";", refs) + ";";
        }
    }

    private void UpdateCorrelation(TestRun run, Transaction transaction)
    {
        if (transaction.TransactionId.HasValue)
        {
            run.TransactionId = transaction.TransactionId;
        }

        if (!string.IsNullOrWhiteSpace(transaction.TransactionToken))
        {
            run.TransactionToken = transaction.TransactionToken;
            _transactionTokenToRun[transaction.TransactionToken] = run.RunId;
        }

        if (transaction.DirectDebitId.HasValue)
        {
            run.DirectDebitId = transaction.DirectDebitId;
            _directDebitToRun[transaction.DirectDebitId.Value] = run.RunId;
        }

        if (!string.IsNullOrWhiteSpace(transaction.Asmachta))
        {
            run.Asmachta = transaction.Asmachta;
        }

        if (!string.IsNullOrWhiteSpace(transaction.ProcessToken))
        {
            run.ProcessToken = transaction.ProcessToken;
        }

        if (transaction.ProcessId.HasValue)
        {
            run.ProcessId = transaction.ProcessId;
        }
    }

    private static TestEvent BuildTransactionEvent(string kind, string result, string message, Payload payload, Transaction transaction)
    {
        return new TestEvent
        {
            CreatedUtc = DateTime.UtcNow,
            Kind = kind,
            Result = result,
            Message = message,
            RawBody = payload.RawBody,
            Format = payload.Kind,
            PayerEmail = transaction.PayerEmail,
            TransactionId = transaction.TransactionId,
            TransactionToken = transaction.TransactionToken,
            DirectDebitId = transaction.DirectDebitId,
            Asmachta = transaction.Asmachta,
            StatusCode = transaction.StatusCode,
            Status = transaction.Status,
            Amount = transaction.Sum
        };
    }

    private static List<string> GetTransactionReferenceCandidates(Transaction transaction)
    {
        var refs = new List<string>();
        if (transaction.TransactionId.HasValue && transaction.TransactionId.Value > 0)
        {
            refs.Add(transaction.TransactionId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(transaction.TransactionToken))
        {
            refs.Add(transaction.TransactionToken.Trim());
        }

        return refs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> SplitRefs(string raw)
    {
        return (raw ?? string.Empty)
            .Split(new[] { ';', ',', '\n', '\r', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);
    }

    private static string Normalize(string raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    private static string ExtractRunId(string cField)
    {
        var parts = cField.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0].Equals("ISOLATED", StringComparison.OrdinalIgnoreCase) ? parts[1] : string.Empty;
    }

    private static void AddEvent(TestRun run, TestEvent evt)
    {
        run.Events.Insert(0, evt);
        if (run.Events.Count > 50)
        {
            run.Events.RemoveRange(50, run.Events.Count - 50);
        }
    }

    private static TestRun Clone(TestRun source)
    {
        return new TestRun
        {
            RunId = source.RunId,
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = source.UpdatedUtc,
            Status = source.Status,
            Message = source.Message,
            CheckoutUrl = source.CheckoutUrl,
            CField1 = source.CField1,
            TestEmail = source.TestEmail,
            FullName = source.FullName,
            Phone = source.Phone,
            Amount = source.Amount,
            TransactionId = source.TransactionId,
            TransactionToken = source.TransactionToken,
            DirectDebitId = source.DirectDebitId,
            Asmachta = source.Asmachta,
            ProcessToken = source.ProcessToken,
            ProcessId = source.ProcessId,
            Membership = source.Membership == null ? null : CloneMembership(source.Membership),
            Events = source.Events.Select(CloneEvent).ToList()
        };
    }

    private static SimulatedMembership CloneMembership(SimulatedMembership source)
    {
        return new SimulatedMembership
        {
            Email = source.Email,
            IsMonthly = source.IsMonthly,
            IsMonthlyActive = source.IsMonthlyActive,
            ExpirationUtc = source.ExpirationUtc,
            Transactions = source.Transactions,
            UpdatedUtc = source.UpdatedUtc
        };
    }

    private static TestEvent CloneEvent(TestEvent source)
    {
        return new TestEvent
        {
            CreatedUtc = source.CreatedUtc,
            Kind = source.Kind,
            Result = source.Result,
            Message = source.Message,
            RawRequest = source.RawRequest,
            RawResponse = source.RawResponse,
            RawBody = source.RawBody,
            Format = source.Format,
            PayerEmail = source.PayerEmail,
            TransactionId = source.TransactionId,
            TransactionToken = source.TransactionToken,
            DirectDebitId = source.DirectDebitId,
            Asmachta = source.Asmachta,
            StatusCode = source.StatusCode,
            Status = source.Status,
            Amount = source.Amount
        };
    }
}

sealed class TestSnapshot
{
    public List<TestRun> Runs { get; set; } = new();
    public List<TestEvent> UnmatchedEvents { get; set; } = new();
}

sealed class TestRun
{
    public string RunId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public string Message { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string CField1 { get; set; } = string.Empty;
    public string TestEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public float Amount { get; set; }
    public int? TransactionId { get; set; }
    public string TransactionToken { get; set; } = string.Empty;
    public int? DirectDebitId { get; set; }
    public string Asmachta { get; set; } = string.Empty;
    public string ProcessToken { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public SimulatedMembership? Membership { get; set; }
    public List<TestEvent> Events { get; set; } = new();
}

sealed class SimulatedMembership
{
    public string Email { get; set; } = string.Empty;
    public bool IsMonthly { get; set; }
    public bool IsMonthlyActive { get; set; }
    public DateTime ExpirationUtc { get; set; }
    public string Transactions { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}

sealed class TestEvent
{
    public DateTime CreatedUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RawRequest { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
    public string RawBody { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string PayerEmail { get; set; } = string.Empty;
    public int? TransactionId { get; set; }
    public string TransactionToken { get; set; } = string.Empty;
    public int? DirectDebitId { get; set; }
    public string Asmachta { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public float? Amount { get; set; }
}

static class PayloadReader
{
    public static async Task<Payload> ReadAsync(HttpRequest request)
    {
        request.EnableBuffering();
        var rawBody = await ReadRawBodyAsync(request);
        var contentType = request.ContentType ?? string.Empty;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var kind = "unknown";

        foreach (var pair in request.Query)
        {
            Put(values, pair.Key, pair.Value.ToString(), overwrite: false);
        }

        var trimmed = rawBody.TrimStart();
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            if (TryFlattenJson(rawBody, values))
            {
                kind = "json";
            }
        }

        if (kind == "unknown" && request.HasFormContentType)
        {
            request.Body.Position = 0;
            var form = await request.ReadFormAsync();
            request.Body.Position = 0;
            foreach (var pair in form)
            {
                Put(values, pair.Key, pair.Value.ToString(), overwrite: true);
            }

            kind = "form";
        }

        if (kind == "unknown" && rawBody.Contains('='))
        {
            var parsed = QueryHelpers.ParseQuery(rawBody.StartsWith("?") ? rawBody : "?" + rawBody);
            foreach (var pair in parsed)
            {
                Put(values, pair.Key, pair.Value.ToString(), overwrite: true);
            }

            kind = "form";
        }

        return new Payload(kind, rawBody, contentType, values);
    }

    private static async Task<string> ReadRawBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return rawBody;
    }

    private static bool TryFlattenJson(string rawBody, IDictionary<string, string> values)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            Flatten(document.RootElement, string.Empty, values);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Flatten(JsonElement element, string path, IDictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var next = string.IsNullOrWhiteSpace(path) ? property.Name : $"{path}.{property.Name}";
                    Flatten(property.Value, next, values);
                }
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, $"{path}[{i++}]", values);
                }
                break;
            case JsonValueKind.String:
                Put(values, path, element.GetString() ?? string.Empty, overwrite: true);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            default:
                Put(values, path, element.GetRawText(), overwrite: true);
                break;
        }
    }

    private static void Put(IDictionary<string, string> values, string key, string value, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = Payload.NormalizeKey(key);
        if (overwrite || !values.ContainsKey(normalized))
        {
            values[normalized] = value;
        }
    }
}

sealed class Payload
{
    private readonly Dictionary<string, string> _values;

    public Payload(string kind, string rawBody, string contentType, Dictionary<string, string> values)
    {
        Kind = kind;
        RawBody = rawBody;
        ContentType = contentType;
        _values = values;
    }

    public string Kind { get; }
    public string RawBody { get; }
    public string ContentType { get; }

    public string Get(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (_values.TryGetValue(NormalizeKey(key), out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    public static string NormalizeKey(string key)
    {
        var converted = (key ?? string.Empty).Trim().Replace("][", ".").Replace("[", ".").Replace("]", string.Empty);
        converted = Regex.Replace(converted, @"\.+", ".");
        return converted.Trim('.');
    }
}

static class TransactionMapper
{
    public static Transaction Map(Payload payload)
    {
        var paymentTypeRaw = payload.Get("data[paymentType]", "paymentType");
        var transaction = new Transaction
        {
            Status = payload.Get("data[status]", "status"),
            StatusCode = NumberParsing.ParseInt(payload.Get("data[statusCode]", "statusCode")),
            TransactionId = NumberParsing.ParseInt(payload.Get("data[transactionId]", "transactionId", "transactionCode")),
            TransactionToken = payload.Get("data[transactionToken]", "transactionToken"),
            PaymentType = NumberParsing.ParseInt(paymentTypeRaw) ?? MapPaymentTypeFromText(paymentTypeRaw),
            Sum = NumberParsing.ParseFloat(payload.Get("data[sum]", "sum", "paymentSum")),
            PaymentDate = payload.Get("data[paymentDate]", "paymentDate"),
            Asmachta = payload.Get("data[asmachta]", "asmachta", "transactionCode"),
            Description = payload.Get("data[description]", "description", "paymentDesc"),
            FullName = payload.Get("data[fullName]", "fullName", "customerName", "payer_name"),
            PayerPhone = payload.Get("data[payerPhone]", "payerPhone", "phone"),
            PayerEmail = payload.Get("data[payerEmail]", "payerEmail", "email", "customerEmail"),
            ProcessId = NumberParsing.ParseInt(payload.Get("data[processId]", "processId", "paymentLinkProcessId")),
            ProcessToken = payload.Get("data[processToken]", "processToken", "Process", "webhookKey", "paymentLinkProcessToken"),
            DirectDebitId = NumberParsing.ParseInt(payload.Get("data[directDebitId]", "directDebitId", "directDebit", "DirectDebit", "regular_payment_id"))
        };

        if (string.IsNullOrWhiteSpace(transaction.TransactionToken) && !string.IsNullOrWhiteSpace(transaction.Asmachta))
        {
            transaction.TransactionToken = BuildSyntheticToken(transaction);
        }

        if (!transaction.StatusCode.HasValue && (!string.IsNullOrWhiteSpace(transaction.Asmachta) || transaction.DirectDebitId.HasValue))
        {
            transaction.StatusCode = 2;
        }

        if (string.IsNullOrWhiteSpace(transaction.Status) && transaction.StatusCode == 2)
        {
            transaction.Status = "\u05e9\u05d5\u05dc\u05dd";
        }

        return transaction;
    }

    private static int? MapPaymentTypeFromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Contains("\u05d4\u05d5\u05e8\u05d0\u05ea") || normalized.Contains("direct"))
        {
            return 1;
        }

        if (normalized.Contains("credit") || normalized.Contains("card") || normalized.Contains("\u05e8\u05d2\u05d9\u05dc") || normalized.Contains("\u05d0\u05e9\u05e8\u05d0\u05d9"))
        {
            return 2;
        }

        return null;
    }

    private static string BuildSyntheticToken(Transaction transaction)
    {
        var input = $"{transaction.Asmachta}|{transaction.PaymentDate}|{transaction.Sum}|{transaction.PayerEmail}";
        using var sha = SHA256.Create();
        return "wh:" + Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}

static class NumberParsing
{
    public static int? ParseInt(string? value)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static float? ParseFloat(string? value)
    {
        return float.TryParse(value?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}

sealed class Transaction
{
    public string Status { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public int? TransactionId { get; set; }
    public string TransactionToken { get; set; } = string.Empty;
    public int? PaymentType { get; set; }
    public float? Sum { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
    public string Asmachta { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PayerPhone { get; set; } = string.Empty;
    public string PayerEmail { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public string ProcessToken { get; set; } = string.Empty;
    public int? DirectDebitId { get; set; }
}

static class TransactionRules
{
    public static bool IsSuccessful(Transaction transaction)
    {
        return transaction.StatusCode == 2 ||
               string.Equals(transaction.Status, "\u05e9\u05d5\u05dc\u05dd", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMonthly(Transaction transaction)
    {
        return (transaction.DirectDebitId.HasValue && transaction.DirectDebitId.Value > 0) ||
               transaction.PaymentType == 1;
    }
}
