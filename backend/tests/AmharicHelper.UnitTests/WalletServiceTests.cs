using System.Data;
using AmharicHelper.Application.Common;
using AmharicHelper.Infrastructure;
using AmharicHelper.Infrastructure.Persistence;
using AmharicHelper.Infrastructure.Wallet;
using Microsoft.Extensions.Options;
using Xunit;

namespace AmharicHelper.UnitTests;

// TryConsumeAsync's happy/insufficient-balance paths need a real Postgres connection (see
// WalletService's use of pg_advisory_xact_lock) and aren't unit-tested here. What matters for
// the Features:Wallet switch is that it is checked *before* anything touches the database —
// this factory throws the moment its connection string is read, so a passing "disabled" test
// proves the short-circuit happened, and a passing "enabled" test proves execution reached the
// database call instead of silently no-opping.
public class WalletServiceTests
{
    private sealed class ThrowingConnectionFactory : ISqlConnectionFactory
    {
        public bool ConnectionStringAccessed { get; private set; }

        public string ConnectionString
        {
            get
            {
                ConnectionStringAccessed = true;
                throw new InvalidOperationException("The database should not be touched here.");
            }
        }

        public IDbConnection Create() => throw new InvalidOperationException("The database should not be touched here.");
    }

    [Fact]
    public async Task TryConsumeAsync_short_circuits_without_touching_the_database_when_wallet_disabled()
    {
        var factory = new ThrowingConnectionFactory();
        var service = new WalletService(factory, Options.Create(new FeatureFlagsOptions { Wallet = false }));

        var result = await service.TryConsumeAsync(UsageSubject.ForUser(Guid.NewGuid()), "analyze");

        Assert.True(result);
        Assert.False(factory.ConnectionStringAccessed);
    }

    [Fact]
    public async Task TryConsumeAsync_reaches_the_database_when_wallet_enabled()
    {
        var factory = new ThrowingConnectionFactory();
        var service = new WalletService(factory, Options.Create(new FeatureFlagsOptions { Wallet = true }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TryConsumeAsync(UsageSubject.ForUser(Guid.NewGuid()), "analyze"));

        Assert.True(factory.ConnectionStringAccessed);
    }
}
