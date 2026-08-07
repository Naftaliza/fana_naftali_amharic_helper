using AmharicHelper.Infrastructure;
using AmharicHelper.Infrastructure.Stt;
using AmharicHelper.Infrastructure.Tts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AmharicHelper.UnitTests;

/// <summary>
/// Regression coverage for AddInfrastructure's Stt/Tts region+key fallback (DependencyInjection.cs)
/// — the actual production wiring, not a re-implementation of it, so a future edit to that method
/// is what this test exercises. Guards a bug that shipped once already: SttOptions.AzureRegion
/// used to default to a non-empty string ("eastus"), which silently defeated the
/// "inherit Tts's region when Stt's own is blank" PostConfigure check (a non-empty default is
/// never "blank"), so every voice-input request went to the wrong Azure region regardless of
/// what Tts:AzureRegion was actually configured with — a working key, talking to a region that
/// simply doesn't recognize it, returning an opaque 401.
/// </summary>
public class SttOptionsWiringTests
{
    private static IServiceProvider Build(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        // ConnectionStrings:Default is the only config AddInfrastructure requires to not throw
        // at registration time — nothing else is dialed (DB, HTTP) merely by building the
        // provider and resolving IOptions<T>.
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Stt_inherits_region_and_key_from_Tts_when_its_own_are_unset()
    {
        var provider = Build(new()
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Tts:AzureRegion"] = "westus2",
            ["Tts:AzureSpeechKey"] = "tts-key-123",
        });

        var stt = provider.GetRequiredService<IOptions<SttOptions>>().Value;

        Assert.Equal("westus2", stt.AzureRegion);
        Assert.Equal("tts-key-123", stt.AzureSpeechKey);
    }

    [Fact]
    public void Stt_keeps_its_own_region_and_key_when_explicitly_configured()
    {
        var provider = Build(new()
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Tts:AzureRegion"] = "westus2",
            ["Tts:AzureSpeechKey"] = "tts-key-123",
            ["Stt:AzureRegion"] = "northeurope",
            ["Stt:AzureSpeechKey"] = "stt-key-456",
        });

        var stt = provider.GetRequiredService<IOptions<SttOptions>>().Value;

        Assert.Equal("northeurope", stt.AzureRegion);
        Assert.Equal("stt-key-456", stt.AzureSpeechKey);
    }

    [Fact]
    public void SttOptions_AzureRegion_default_is_blank_not_a_real_region()
    {
        // The actual bug: a non-empty class-level default silently defeats the
        // string.IsNullOrWhiteSpace(...) fallback check in DependencyInjection.cs, regardless of
        // what Tts is configured with. This is the one-line assertion that would have caught it.
        Assert.Equal("", new SttOptions().AzureRegion);
    }
}
