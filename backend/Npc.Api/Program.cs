using System.IO.Compression;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Neo4j.Driver;
using Npc.Api.Data;
using Npc.Api.Infrastructure.Http;
using Npc.Api.Infrastructure.Metrics;
using Npc.Api.Infrastructure.Observability;
using Npc.Api.Infrastructure.BackgroundJobs;
using Npc.Api.Infrastructure.Middleware;
using Npc.Api.Middleware;
using Npc.Api.Services;
using Npc.Api.Services.Impl;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using Npc.Api.Infrastructure.HealthChecks;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Infrastructure.Cache;
using Npc.Api.Infrastructure.Exceptions;
using Npc.Api.Infrastructure.Mapping;
using FluentValidation;
using Npc.Api.Repositories;
using Npc.Api.Application.Mediator;
using Npc.Api.Domain.Events;
using Npc.Api.Domain.Events.Handlers;
using Npc.Api.Application.Commands;
using Npc.Api.Entities;
using Npc.Api.Application.Queries;
using Npc.Api.Dtos;
using Npc.Api.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

var neo4jConf = builder.Configuration.GetSection("Neo4j");
var neoUri = neo4jConf.GetValue<string>("Uri");
var neoUser = neo4jConf.GetValue<string>("User");
var neoPwd = neo4jConf.GetValue<string>("Password");

// Cors Configuration
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
var securitySection = builder.Configuration.GetSection("Security");
var origins = securitySection.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o =>
{
    o.AddPolicy("Default", p =>
    {
        if (origins.Length > 0)
            p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
        {
            // En producción, deshabilitar AllowAnyOrigin por seguridad
            if (builder.Environment.IsDevelopment())
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            else
                p.WithOrigins("https://localhost", "https://127.0.0.1").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    });
});

// Rate limit Configuration
var rl = securitySection.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("global", lim =>
    {
        lim.PermitLimit = rl.PermitLimit;
        lim.Window = TimeSpan.FromSeconds(rl.WindowSeconds);
        lim.QueueLimit = 0;
    });
});


// Log Configuration
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext().WriteTo.Console(
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}").CreateLogger();

builder.Host.UseSerilog();

// Telemetry Configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Npc.Api"))
    .WithMetrics(m =>
    {
    
        m.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddMeter(Telemetry.MeterName)
         .AddPrometheusExporter(); // /metrics
    })
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation();
    });

// Health Check Configuration
var postgresConnection = builder.Configuration.GetConnectionString("Postgres") ?? "";
var neo4jUri = neo4jConf.GetValue<string>("Uri") ?? "";
var neo4jUser = neo4jConf.GetValue<string>("User") ?? "";
var neo4jPassword = neo4jConf.GetValue<string>("Password") ?? "";


// Redis Cache Configuration
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddHealthChecks()
    // Basic checks
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "ready" })

    // Database health checks
    .AddNpgSql(postgresConnection, name: "postgresql", tags: new[] { "db", "postgresql", "ready" })
    .AddCheck<Neo4jHealthCheck>("neo4j", tags: new[] { "db", "neo4j", "ready" })

    // Redis health check
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "cache", "redis", "ready" })

    // External services health checks
    .AddUrlGroup(new Uri($"{builder.Configuration.GetSection("Agent").GetValue<string>("BaseUrl")?.TrimEnd('/') ?? "http://localhost:8080"}/health"),
        name: "agent-service", tags: new[] { "external", "agent" })
    .AddUrlGroup(new Uri($"{builder.Configuration.GetSection("Tts").GetValue<string>("BaseUrl")?.TrimEnd('/') ?? "http://localhost:8001"}/health"),
        name: "tts-service", tags: new[] { "external", "tts" })
    .AddUrlGroup(new Uri($"{builder.Configuration.GetSection("ImageGenerator").GetValue<string>("BaseUrl")?.TrimEnd('/') ?? "http://localhost:8002"}/health"),
        name: "image-service", tags: new[] { "external", "image" })

    // Custom application health checks
    .AddCheck<ApplicationHealthCheck>("application", tags: ["application", "ready"])
    .AddCheck<CacheHealthCheck>("cache-operations", tags: ["cache", "operations"])
    .AddCheck<ExternalServicesHealthCheck>("external-services", tags: ["external", "services"])
    .AddCheck<DatabaseOperationsHealthCheck>("database-operations", tags: ["db", "operations"]);

// Health Checks UI
builder.Services.AddHealthChecksUI(options =>
{
    options.SetEvaluationTimeInSeconds(30); // Check every 30 seconds
    options.MaximumHistoryEntriesPerEndpoint(50); // Keep 50 history entries
    options.AddHealthCheckEndpoint("NPC Generator API", "/health/detailed");
}).AddInMemoryStorage();


// Swagger Configuration
builder.Services.AddSwaggerGen(c =>
{
    var xml = System.IO.Path.Combine(AppContext.BaseDirectory, "Npc.Api.xml");
    if (File.Exists(xml))
        c.IncludeXmlComments(xml, includeControllerXmlComments: true);

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NPC Generator API",
        Version = "v1",
        Description = "API para generación y gestión de NPCs, lore, conversaciones, TTS e imágenes."
    });

    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API Key requerida (X-API-Key)."
    };
    c.AddSecurityDefinition("ApiKey", apiKeyScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            apiKeyScheme,
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<TtsOptions>(builder.Configuration.GetSection("Tts"));
builder.Services.Configure<ImageGenOptions>(builder.Configuration.GetSection("ImageGenerator"));

// AutoMapper Configuration
builder.Services.AddAutoMapper(typeof(Program));

// Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();


builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "NpcGeneratorService";
});
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Correlation ID Configuration
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();
builder.Services.AddTransient<HttpCorrelationIdHandler>();

// Hangfire Configuration
var hangfireConnectionString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(hangfireConnectionString);

    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount;
    options.Queues = new[] { "default", "images", "bulk", "cleanup" };
    options.ServerName = Environment.MachineName + "-npc-service";
});

// Background Jobs Services
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
builder.Services.AddScoped<BackgroundJobs>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

builder.Services.AddDbContext<CharacterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IModerationAgent, ModerationAgentService>();
builder.Services.AddScoped<IModerationService, ModerationService>();

builder.Services.AddSingleton<AgentMetrics>();
builder.Services.AddSingleton<TtsMetrics>();
builder.Services.AddSingleton<ImageGenMetrics>();

builder.Services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(neoUri, AuthTokens.Basic(neoUser, neoPwd)));
builder.Services.AddScoped<IConversationGraphService, ConversationGraphService>();

// Repository Pattern
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
builder.Services.AddScoped<IWorldRepository, WorldRepository>();
builder.Services.AddScoped<ILoreRepository, LoreRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

// CQRS Pattern
builder.Services.AddScoped<IMediator, SimpleMediator>();

// Domain Events
builder.Services.AddScoped<IDomainEventDispatcher,  DomainEventDispatcher>();

// Domain Event Handlers - Database Sync
builder.Services.AddScoped<IDomainEventHandler<CharacterCreatedEvent>, CharacterCreatedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<CharacterUpdatedEvent>, CharacterUpdatedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<CharacterDeletedEvent>, CharacterDeletedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorldCreatedEvent>, WorldCreatedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorldUpdatedEvent>, WorldUpdatedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorldDeletedEvent>, WorldDeletedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<LoreCreatedEvent>, LoreCreatedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<LoreUpdatedEvent>, LoreUpdatedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<LoreDeletedEvent>, LoreDeletedEventHandler>();

// Domain Event Handlers - Conversation Sync
builder.Services.AddScoped<IDomainEventHandler<ConversationCreatedEvent>, ConversationMetadataHandler>();
builder.Services.AddScoped<IDomainEventHandler<UtteranceCreatedEvent>, UtteranceMetadataHandler>();
builder.Services.AddScoped<IDomainEventHandler<UtteranceCreatedEvent>, CharacterRelationshipInferenceHandler>();

// Cache invalidation handlers
builder.Services.AddScoped<IDomainEventHandler<CharacterCreatedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<CharacterUpdatedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<CharacterDeletedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorldCreatedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorldUpdatedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorldDeletedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<LoreCreatedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<LoreUpdatedEvent>, CacheInvalidationHandler>();
builder.Services.AddScoped<IDomainEventHandler<LoreDeletedEvent>, CacheInvalidationHandler>();

// Command Handlers
builder.Services.AddScoped<ICommandHandler<CreateCharacterCommand, Character>, CreateCharacterCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateCharacterCommand, Character>, UpdateCharacterCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteCharacterCommand>, DeleteCharacterCommandHandler>();

// Bulk Command Handlers
builder.Services.AddScoped<ICommandHandler<BulkCreateCharactersCommand, BulkOperationResult<Character>>, BulkCreateCharactersCommandHandler>();
builder.Services.AddScoped<ICommandHandler<BulkUpdateCharactersCommand, BulkOperationResult<Character>>, BulkUpdateCharactersCommandHandler>();
builder.Services.AddScoped<ICommandHandler<BulkDeleteCharactersCommand, BulkOperationResult<Unit>>, BulkDeleteCharactersCommandHandler>();

// Character Query Handlers - Using Cached Versions
builder.Services.AddScoped<IQueryHandler<GetCharacterByIdQuery, Character?>, CachedGetCharacterByIdQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetCharactersPagedQuery, (IEnumerable<Character> Items, int TotalCount)>, CachedGetCharactersPagedQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetCharactersByAgeRangeQuery, IEnumerable<Character>>, CachedGetCharactersByAgeRangeQueryHandler>();
builder.Services.AddScoped<IQueryHandler<SearchCharactersByNameQuery, IEnumerable<Character>>, CachedSearchCharactersByNameQueryHandler>();

// World Command Handlers
builder.Services.AddScoped<ICommandHandler<CreateWorldCommand, World>, CreateWorldCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateWorldCommand, World>, UpdateWorldCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteWorldCommand>, DeleteWorldCommandHandler>();

// World Query Handlers - Using Cached Versions
builder.Services.AddScoped<IQueryHandler<GetWorldByIdQuery, World?>, CachedGetWorldByIdQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetWorldsPagedQuery, (IEnumerable<World> Items, int TotalCount)>, CachedGetWorldsPagedQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetWorldsWithLoreQuery, IEnumerable<World>>, CachedGetWorldsWithLoreQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetWorldWithLoreByIdQuery, World?>, CachedGetWorldWithLoreByIdQueryHandler>();

// Lore Command Handlers
builder.Services.AddScoped<ICommandHandler<CreateLoreCommand, Lore>, CreateLoreCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateLoreCommand, Lore>, UpdateLoreCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteLoreCommand>, DeleteLoreCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SuggestLoreCommand, LoreSuggestResponse>, SuggestLoreCommandHandler>();

// Lore Query Handlers - Using Cached Versions
builder.Services.AddScoped<IQueryHandler<GetLoreByIdQuery, Lore?>, CachedGetLoreByIdQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetLoreByWorldIdQuery, IEnumerable<Lore>>, CachedGetLoreByWorldIdQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetGeneratedLoreQuery, IEnumerable<Lore>>, CachedGetGeneratedLoreQueryHandler>();
builder.Services.AddScoped<IQueryHandler<SearchLoreByTextQuery, IEnumerable<Lore>>, CachedSearchLoreByTextQueryHandler>();

builder.Services.AddHttpClient<IAgentConversationService, AgentConversationService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<AgentOptions>>();
    if (!string.IsNullOrWhiteSpace(opt.Value.BaseUrl))
        http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(opt.Value.ApiKey))
        http.DefaultRequestHeaders.Add("X-API-Key", opt.Value.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);
}).AddPolicyHandler(AgentPollyPolicies.CreateComposite())
.AddHttpMessageHandler<HttpCorrelationIdHandler>();

builder.Services.AddHttpClient<ITtsService, TtsService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<TtsOptions>>();
    if (!string.IsNullOrWhiteSpace(opt.Value.BaseUrl))
        http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(opt.Value.ApiKey))
        http.DefaultRequestHeaders.Add("X-API-Key", opt.Value.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);


}).AddPolicyHandler(AgentPollyPolicies.CreateComposite())
.AddHttpMessageHandler<HttpCorrelationIdHandler>();

builder.Services.AddHttpClient<IImageGenService, ImageGenService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<ImageGenOptions>>();
    if (!string.IsNullOrWhiteSpace(opt.Value.BaseUrl))
        http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(opt.Value.ApiKey))
        http.DefaultRequestHeaders.Add("X-API-Key", opt.Value.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);


}).AddPolicyHandler(AgentPollyPolicies.CreateComposite())
.AddHttpMessageHandler<HttpCorrelationIdHandler>();


builder.Services.AddScoped<IAgentConversationService, AgentConversationService>();
builder.Services.AddScoped<ITtsService, TtsService>();
builder.Services.AddScoped<IImageGenService, ImageGenService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Localization services
builder.Services.AddScoped<ILocalizationService, LocalizationService>();

// GraphQL Configuration
builder.Services
    .AddDbContextFactory<CharacterDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")))
    .AddGraphQLServer()
    .AddQueryType<Npc.Api.GraphQL.Queries.Query>()
    .AddMutationType<Npc.Api.GraphQL.Mutations.Mutation>()
    .AddSubscriptionType<Npc.Api.GraphQL.Subscriptions.Subscription>()
    .AddType<Npc.Api.GraphQL.Types.CharacterType>()
    .AddType<Npc.Api.GraphQL.Types.WorldType>()
    .AddType<Npc.Api.GraphQL.Types.LoreType>()
    .AddType<Npc.Api.GraphQL.Types.ConversationType>()
    .AddType<Npc.Api.GraphQL.Types.UtteranceType>()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddDataLoader<Npc.Api.GraphQL.DataLoaders.CharacterByIdDataLoader>()
    .AddDataLoader<Npc.Api.GraphQL.DataLoaders.CharactersByWorldDataLoader>()
    .AddDataLoader<Npc.Api.GraphQL.DataLoaders.WorldByIdDataLoader>()
    .AddDataLoader<Npc.Api.GraphQL.DataLoaders.LoreByIdDataLoader>()
    .AddDataLoader<Npc.Api.GraphQL.DataLoaders.LoreByWorldDataLoader>()
    .AddRedisSubscriptions(_ => StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));

var app = builder.Build();

app.UseCors("Default");

// Localization middleware (early in pipeline)
app.UseLocalization();

// Correlation ID middleware (must be early in pipeline)
app.UseCorrelationId();

app.UseRateLimiter();

// Exception handling middleware (must be early in pipeline)
app.UseExceptionHandler();

app.UseApiKeyAuth();

// GraphQL endpoint
app.MapGraphQL("/graphql").RequireRateLimiting("global");

app.MapControllers().RequireRateLimiting("global");

using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CharacterDbContext>();
    db.Database.Migrate();

    // Seed languages
    await LanguageSeeder.SeedLanguagesAsync(db);

    var driver = app.Services.GetRequiredService<IDriver>();
    await Neo4jBootstrap.EnsureAsync(driver, CancellationToken.None);
}

app.Lifetime.ApplicationStopping.Register(() =>
{
    var driver = app.Services.GetRequiredService<IDriver>();
    driver.Dispose();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints
app.MapPrometheusScrapingEndpoint("/metrics");

// Health Check Endpoints
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

// Detailed health check endpoints
app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/cache", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("cache"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/external", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("external"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedResponse
});

// Health Checks UI (only in development for security)
if (app.Environment.IsDevelopment())
{
    app.MapHealthChecksUI(options =>
    {
        options.UIPath = "/health-ui";
        options.ApiPath = "/health-ui-api";
    });

    // Hangfire Dashboard (only in development for security)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}


app.MapControllers();

app.Run();

public partial class Program {}