using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MO6.Models;
using MyProject12;
using MyProject12.Models;
using MyProject12.Services;
using Xunit;

namespace MO6.Tests;

public class TemporaryMemberResolverTests
{
    [Fact]
    public async Task ResolveNewestUnprocessedByExactEmail_ShouldMatchCaseInsensitively_AndPreferNewest()
    {
        await using var fixture = await CreateDbAsync();
        fixture.Db.TemporaryMembers.AddRange(
            new TemporaryMember
            {
                Email = " person@example.com ",
                Name = "Old",
                Password = "p",
                Phone = "1",
                Token = "a",
                Created = new DateTime(2026, 3, 20, 10, 0, 0),
                Processed = false
            },
            new TemporaryMember
            {
                Email = "PERSON@example.com",
                Name = "New",
                Password = "p",
                Phone = "2",
                Token = "b",
                Created = new DateTime(2026, 3, 21, 10, 0, 0),
                Processed = false
            },
            new TemporaryMember
            {
                Email = "otherperson@example.com",
                Name = "Other",
                Password = "p",
                Phone = "3",
                Token = "c",
                Created = new DateTime(2026, 3, 22, 10, 0, 0),
                Processed = false
            });
        await fixture.Db.SaveChangesAsync();

        var sut = new TemporaryMemberResolver(fixture.Db);

        var result = sut.ResolveNewestUnprocessedByExactEmail("Person@Example.com");

        result.Should().NotBeNull();
        result!.Name.Should().Be("New");
        result.Token.Should().Be("b");
    }

    [Fact]
    public async Task ResolveNewestUnprocessedByExactEmail_ShouldIgnoreProcessedRows()
    {
        await using var fixture = await CreateDbAsync();
        fixture.Db.TemporaryMembers.AddRange(
            new TemporaryMember
            {
                Email = "person@example.com",
                Name = "Processed",
                Password = "p",
                Phone = "1",
                Token = "processed",
                Created = new DateTime(2026, 3, 22, 10, 0, 0),
                Processed = true
            },
            new TemporaryMember
            {
                Email = "person@example.com",
                Name = "Pending",
                Password = "p",
                Phone = "2",
                Token = "pending",
                Created = new DateTime(2026, 3, 21, 10, 0, 0),
                Processed = false
            });
        await fixture.Db.SaveChangesAsync();

        var sut = new TemporaryMemberResolver(fixture.Db);

        var result = sut.ResolveNewestUnprocessedByExactEmail("PERSON@example.com");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Pending");
        result.Token.Should().Be("pending");
    }

    [Fact]
    public async Task ResolveNewestUnprocessedByExactEmail_ShouldReturnNull_WhenOnlyProcessedRowsExist()
    {
        await using var fixture = await CreateDbAsync();
        fixture.Db.TemporaryMembers.Add(
            new TemporaryMember
            {
                Email = "person@example.com",
                Name = "Processed",
                Password = "p",
                Phone = "1",
                Token = "processed",
                Created = new DateTime(2026, 3, 22, 10, 0, 0),
                Processed = true
            });
        await fixture.Db.SaveChangesAsync();

        var sut = new TemporaryMemberResolver(fixture.Db);

        var result = sut.ResolveNewestUnprocessedByExactEmail("person@example.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveSafeAutoHealCandidate_ShouldRequireMatchingPhone_WhenPhonesArePresent()
    {
        await using var fixture = await CreateDbAsync();
        fixture.Db.TemporaryMembers.AddRange(
            new TemporaryMember
            {
                Email = "payer@example.com",
                Name = "Correct Match",
                Password = "p",
                Phone = "050-123-4567",
                Token = "correct",
                Created = new DateTime(2026, 3, 21, 9, 0, 0),
                Processed = false
            },
            new TemporaryMember
            {
                Email = "payer@example.com",
                Name = "Wrong Phone",
                Password = "p",
                Phone = "050-000-0000",
                Token = "wrong-phone",
                Created = new DateTime(2026, 3, 21, 10, 0, 0),
                Processed = false
            });
        await fixture.Db.SaveChangesAsync();

        var sut = new TemporaryMemberResolver(fixture.Db);
        var transaction = new Transaction
        {
            Created = new DateTime(2026, 3, 21, 11, 0, 0),
            PayerEmail = "PAYER@example.com",
            FullName = "Correct Match",
            PayerPhone = "0501234567"
        };

        var result = sut.ResolveSafeAutoHealCandidate(transaction, TimeSpan.FromHours(24));

        result.Should().NotBeNull();
        result!.Token.Should().Be("correct");
    }

    [Fact]
    public async Task ResolveSafeAutoHealCandidate_ShouldFallbackToName_WhenPhonesAreMissing()
    {
        await using var fixture = await CreateDbAsync();
        fixture.Db.TemporaryMembers.Add(
            new TemporaryMember
            {
                Email = "payer@example.com",
                Name = "  דרורית   מתוקי ",
                Password = "p",
                Phone = "",
                Token = "name-match",
                Created = new DateTime(2026, 3, 21, 10, 0, 0),
                Processed = false
            });
        await fixture.Db.SaveChangesAsync();

        var sut = new TemporaryMemberResolver(fixture.Db);
        var transaction = new Transaction
        {
            Created = new DateTime(2026, 3, 21, 11, 0, 0),
            PayerEmail = "payer@example.com",
            FullName = "דרורית מתוקי",
            PayerPhone = ""
        };

        var result = sut.ResolveSafeAutoHealCandidate(transaction, TimeSpan.FromHours(24));

        result.Should().NotBeNull();
        result!.Token.Should().Be("name-match");
    }

    [Fact]
    public async Task ResolveSafeAutoHealCandidate_ShouldIgnoreOldCandidates()
    {
        await using var fixture = await CreateDbAsync();
        fixture.Db.TemporaryMembers.Add(
            new TemporaryMember
            {
                Email = "payer@example.com",
                Name = "Old",
                Password = "p",
                Phone = "0501234567",
                Token = "old",
                Created = new DateTime(2026, 3, 18, 10, 0, 0),
                Processed = false
            });
        await fixture.Db.SaveChangesAsync();

        var sut = new TemporaryMemberResolver(fixture.Db);
        var transaction = new Transaction
        {
            Created = new DateTime(2026, 3, 21, 11, 0, 0),
            PayerEmail = "payer@example.com",
            FullName = "Old",
            PayerPhone = "0501234567"
        };

        var result = sut.ResolveSafeAutoHealCandidate(transaction, TimeSpan.FromHours(24));

        result.Should().BeNull();
    }

    private static async Task<DbFixture> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;

        var db = new DB(options);
        await db.Database.EnsureCreatedAsync();

        return new DbFixture(connection, db);
    }

    private sealed class DbFixture : IAsyncDisposable
    {
        public DbFixture(SqliteConnection connection, DB db)
        {
            Connection = connection;
            Db = db;
        }

        public SqliteConnection Connection { get; }
        public DB Db { get; }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
