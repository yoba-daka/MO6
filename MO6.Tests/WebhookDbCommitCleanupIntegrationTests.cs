using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyProject12.Models;
using MyProject12.Services;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace MO6.Tests;

public class WebhookDbCommitCleanupIntegrationTests
{
    private sealed class ParsedCurlRequest
    {
        public string Method { get; set; } = "POST";
        public string Url { get; set; } = "http://localhost:5000/meshulam-response";
        public string ContentType { get; set; } = "application/json";
        public string Body { get; set; } = string.Empty;
    }

    private sealed class TransactionCommitDbContext : DbContext
    {
        public TransactionCommitDbContext(DbContextOptions<TransactionCommitDbContext> options) : base(options)
        {
        }

        public DbSet<Transaction> Transactions => Set<Transaction>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<Transaction>();
            entity.ToTable("Transactions");
            entity.HasKey(x => x.ID);
            entity.Property(x => x.ID).ValueGeneratedOnAdd();

            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.TransactionToken).IsRequired();
            entity.Property(x => x.PaymentDate).IsRequired();
            entity.Property(x => x.Asmachta).IsRequired();
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.FullName).IsRequired();
            entity.Property(x => x.PayerPhone).IsRequired();
            entity.Property(x => x.PayerEmail).IsRequired();
            entity.Property(x => x.CardSuffix).IsRequired();
            entity.Property(x => x.CardType).IsRequired();
            entity.Property(x => x.CardBrand).IsRequired();
            entity.Property(x => x.CardExp).IsRequired();
            entity.Property(x => x.ProcessToken).IsRequired();
            entity.Property(x => x.CardToken).IsRequired();
        }
    }

    [Fact]
    public async Task Replay_ApprovedWebhookCurl_ShouldCommitAndThenCleanup_FromDatabase()
    {
        const string curl = """
        curl -i -X POST 'http://localhost:5000/meshulam-response?Process=84ade426-4569-0c6a-df21-2386ce37e015&DirectDebit=45088143' -H 'Content-Type: application/json' --data-raw '{"webhookKey":"84ade426-4569-0c6a-df21-2386ce37e015","identifyParam":"","transactionCode":"cimItersouPpLWxggxznYg==","transactionType":"אשראי","paymentSum":"67","paymentsNum":0,"allPaymentNum":1,"firstPaymentSum":0,"periodicalPaymentSum":0,"paymentType":"הוראת קבע","paymentDate":"20/2/26","asmachta":"466720174","paymentDesc":"MO6 Payment","fullName":"אבי סהלו","payerPhone":"0546782823","payerEmail":"afk1281@gmail.com","cardSuffix":"9887","cardBrand":"Visa","cardType":"Local","cardBin":"407517","paymentSource":"מערכת חיצונית","ip":"46.210.200.217","invoiceURL":"","directDebitId":45088143}'
        """;

        var parsed = ParseCurl(curl);
        var request = BuildRequest(parsed);
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        payload.IsJson.Should().BeTrue();
        var transaction = MeshulamTransactionMapper.MapJsonToTransaction(payload.RawBody);
        transaction.Should().NotBeNull();
        transaction.TransactionToken.Should().NotBeNullOrWhiteSpace();
        transaction.DirectDebitId.Should().Be(45088143);
        transaction.StatusCode.Should().Be(2);

        var dbFile = Path.Combine(Path.GetTempPath(), $"mo6-webhook-test-{Guid.NewGuid():N}.sqlite");
        try
        {
            var options = new DbContextOptionsBuilder<TransactionCommitDbContext>()
                .UseSqlite($"Data Source={dbFile}")
                .Options;

            await using var db = new TransactionCommitDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();

            var committed = await db.Transactions
                .Where(x => x.TransactionToken == transaction.TransactionToken)
                .ToListAsync();
            committed.Should().HaveCount(1);
            committed[0].PayerEmail.Should().Be("afk1281@gmail.com");

            db.Transactions.RemoveRange(committed);
            await db.SaveChangesAsync();

            var remaining = await db.Transactions
                .CountAsync(x => x.TransactionToken == transaction.TransactionToken);
            remaining.Should().Be(0);
        }
        finally
        {
            if (File.Exists(dbFile))
            {
                File.Delete(dbFile);
            }
        }
    }

    private static HttpRequest BuildRequest(ParsedCurlRequest parsed)
    {
        var uri = new Uri(parsed.Url);
        var context = new DefaultHttpContext();
        context.Request.Method = parsed.Method;
        context.Request.Path = uri.AbsolutePath;
        context.Request.QueryString = new QueryString(uri.Query);
        context.Request.ContentType = parsed.ContentType;

        var bytes = Encoding.UTF8.GetBytes(parsed.Body);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        return context.Request;
    }

    private static ParsedCurlRequest ParseCurl(string curl)
    {
        var parsed = new ParsedCurlRequest();

        var method = Regex.Match(curl, @"-X\s+([A-Z]+)", RegexOptions.IgnoreCase);
        if (method.Success)
        {
            parsed.Method = method.Groups[1].Value.ToUpperInvariant();
        }

        var url = Regex.Match(curl, @"'(?<url>https?://[^']+)'", RegexOptions.IgnoreCase);
        if (url.Success)
        {
            parsed.Url = url.Groups["url"].Value;
        }

        var contentType = Regex.Match(curl, @"-H\s+'Content-Type:\s*(?<value>[^']+)'", RegexOptions.IgnoreCase);
        if (contentType.Success)
        {
            parsed.ContentType = contentType.Groups["value"].Value.Trim();
        }

        var body = Regex.Match(curl, @"--data-raw\s+'(?<body>.*)'\s*$", RegexOptions.Singleline);
        if (body.Success)
        {
            parsed.Body = body.Groups["body"].Value;
        }

        return parsed;
    }
}
