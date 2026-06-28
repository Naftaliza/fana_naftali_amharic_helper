using AmharicHelper.Application.Abstractions;
using AmharicHelper.Infrastructure.Ai;
using AmharicHelper.Infrastructure.Ocr;
using AmharicHelper.Infrastructure.Persistence;
using AmharicHelper.Infrastructure.Repositories;
using AmharicHelper.Infrastructure.Security;
using AmharicHelper.Infrastructure.Storage;
using AmharicHelper.Infrastructure.Tts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AmharicHelper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Database (PostgreSQL via Npgsql). Be lenient about DateTime Kind so UTC
        // timestamps from the domain map cleanly to timestamptz columns.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddScoped<DatabaseMigrator>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentAnalysisRepository, DocumentAnalysisRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<ITtsAudioCacheRepository, TtsAudioCacheRepository>();
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<ILeadRepository, LeadRepository>();

        // Security
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // File storage
        var configuredRoot = config["Storage:RootPath"];
        var storageRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(AppContext.BaseDirectory, "uploads")
            : configuredRoot;
        services.AddSingleton<IFileStorage>(new LocalFileStorage(storageRoot));

        // AI options are shared by the AI provider and the Claude OCR provider.
        services.Configure<AiOptions>(config.GetSection("Ai"));

        // OCR provider — selectable via Ocr:Provider (Claude | Mock | Google | Azure).
        // Claude vision is the default real engine.
        var ocrProvider = config["Ocr:Provider"] ?? "Claude";
        switch (ocrProvider.ToLowerInvariant())
        {
            case "mock": services.AddScoped<IOcrProvider, MockOcrProvider>(); break;
            case "google": services.AddScoped<IOcrProvider, GoogleVisionOcrProvider>(); break;
            case "azure": services.AddScoped<IOcrProvider, AzureOcrProvider>(); break;
            default: services.AddHttpClient<IOcrProvider, ClaudeOcrProvider>(); break;
        }

        // AI provider — selectable via Ai:Provider (Claude | OpenAI)
        var aiProvider = config["Ai:Provider"] ?? "Claude";
        if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IAiProvider, OpenAiProvider>();
        else
            services.AddHttpClient<IAiProvider, ClaudeAiProvider>();

        // Text-to-speech — selectable via Tts:Provider (Azure | ElevenLabs).
        // Azure is the default because it has native Amharic neural voices.
        services.Configure<TtsOptions>(config.GetSection("Tts"));
        var ttsProvider = config["Tts:Provider"] ?? "Azure";
        if (ttsProvider.Equals("ElevenLabs", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<ITtsProvider, ElevenLabsTtsProvider>();
        else
            services.AddHttpClient<ITtsProvider, AzureTtsProvider>();

        return services;
    }
}
