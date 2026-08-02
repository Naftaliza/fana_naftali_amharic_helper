using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.Documents;
using AmharicHelper.Infrastructure.Ai;
using AmharicHelper.Infrastructure.Email;
using AmharicHelper.Infrastructure.Invoicing;
using AmharicHelper.Infrastructure.Ocr;
using AmharicHelper.Infrastructure.Persistence;
using AmharicHelper.Infrastructure.Processing;
using AmharicHelper.Infrastructure.Repositories;
using AmharicHelper.Infrastructure.Security;
using AmharicHelper.Infrastructure.Storage;
using AmharicHelper.Infrastructure.Tts;
using AmharicHelper.Infrastructure.Wallet;
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
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IAnalyticsEventRepository, AnalyticsEventRepository>();
        services.AddScoped<IEventTracker, EventTracker>();
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<ISponsorshipRepository, SponsorshipRepository>();

        // Server-side usage meter (see WalletService) — replaces the old client-only
        // frontend/lib/trial.ts localStorage counter and gates the paid AI/TTS calls behind a
        // real, race-safe credit ledger rather than nothing at all.
        services.AddScoped<IWalletService, WalletService>();

        // Background OCR pipeline: upload persists pages + enqueues, DocumentProcessingWorker
        // dequeues and runs DocumentProcessor off the request thread (see plan). The queue is
        // registered as both its concrete type (the worker needs the ChannelReader) and the
        // narrower interface (everything else only needs to enqueue).
        services.AddSingleton<DocumentProcessingQueue>();
        services.AddSingleton<IDocumentProcessingQueue>(sp => sp.GetRequiredService<DocumentProcessingQueue>());
        services.AddScoped<DocumentProcessor>();
        services.AddHostedService<DocumentProcessingWorker>();

        // Deletes documents past their RetainUntil date (see 021_legal_consent.sql) on a fixed
        // interval — the app previously kept every uploaded page file (bank/medical/government
        // letters) forever.
        services.AddHostedService<RetentionSweepWorker>();

        // Provider invoicing: PDF generation (QuestPDF — Community license, revenue-capped; see
        // README) and outbound email, configured via Email:Smtp (reused for both senders below —
        // Password/From apply to both; Host/Port/Username are Smtp-only).
        services.Configure<EmailOptions>(config.GetSection("Email:Smtp"));
        // Email:Provider selects the sender (SendGridApi | Smtp). SendGridApi (HTTPS) is the
        // default because Railway — this app's production host — blocks outbound SMTP ports
        // (25/465/587), so raw SMTP can never connect from a Railway container regardless of
        // how correct the SendGrid credentials are. Smtp remains available for hosts that don't
        // block those ports.
        var emailProvider = config["Email:Provider"] ?? "SendGridApi";
        if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddHttpClient<IEmailSender, SendGridApiEmailSender>();
        services.AddScoped<IInvoicePdfBuilder, QuestPdfInvoiceBuilder>();
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
            // An explicit timeout replaces .NET's 100s default — combined with
            // AnthropicHttp's 4 retry attempts, an unset timeout could otherwise pin a
            // request for ~400s. 60s is well above the typical 5-20s a single OCR call takes.
            default: services.AddHttpClient<IOcrProvider, ClaudeOcrProvider>(c => c.Timeout = TimeSpan.FromSeconds(60)); break;
        }

        // AI provider — selectable via Ai:Provider (Claude | OpenAI)
        var aiProvider = config["Ai:Provider"] ?? "Claude";
        if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IAiProvider, OpenAiProvider>();
        else
            // Longer than the OCR client's: a full trilingual (he/am/en) structured analysis,
            // forced through tool_choice with max_tokens=8192, has been observed taking
            // consistently longer than 60s to generate — that's not a flaky call worth
            // retrying quickly, it's a call that needs more time to begin with.
            services.AddHttpClient<IAiProvider, ClaudeAiProvider>(c => c.Timeout = TimeSpan.FromSeconds(120));

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
