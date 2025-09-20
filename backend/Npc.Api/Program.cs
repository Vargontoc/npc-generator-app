using System.IO.Compression;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Neo4j.Driver;
using Npc.Api.Data;
using Npc.Api.Infrastructure.Http;
using Npc.Api.Infrastructure.Metrics;
using Npc.Api.Infrastructure.Observability;
using Npc.Api.Middleware;
using Npc.Api.Services;
using Npc.Api.Services.Impl;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

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

builder.Services.AddHealthChecks()
    // Basic checks
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "ready" })

    // Database health checks
    .AddNpgSql(postgresConnection, name: "postgresql", tags: new[] { "db", "postgresql", "ready" })
    .AddNeo4j(neo4jUri, neo4jUser, neo4jPassword, name: "neo4j", tags: new[] { "db", "neo4j", "ready" })

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
    .AddCheck<Infrastructure.HealthChecks.ApplicationHealthCheck>("application", tags: new[] { "application", "ready" })
    .AddCheck<Infrastructure.HealthChecks.CacheHealthCheck>("cache-operations", tags: new[] { "cache", "operations" })
    .AddCheck<Infrastructure.HealthChecks.ExternalServicesHealthCheck>("external-services", tags: new[] { "external", "services" })
    .AddCheck<Infrastructure.HealthChecks.DatabaseOperationsHealthCheck>("database-operations", tags: new[] { "db", "operations" });

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
    var xml = Path.Combine(AppContext.BaseDirectory, "Npc.Api.xml");
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
builder.Services.AddExceptionHandler<Infrastructure.Exceptions.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Redis Cache Configuration
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "NpcGeneratorService";
});
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddScoped<Infrastructure.Cache.ICacheService, Infrastructure.Cache.RedisCacheService>();

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
builder.Services.AddScoped<Repositories.ICharacterRepository, Repositories.CharacterRepository>();
builder.Services.AddScoped<Repositories.IWorldRepository, Repositories.WorldRepository>();
builder.Services.AddScoped<Repositories.ILoreRepository, Repositories.LoreRepository>();
builder.Services.AddScoped<Repositories.IConversationRepository, Repositories.ConversationRepository>();

// CQRS Pattern
builder.Services.AddScoped<Application.Mediator.IMediator, Application.Mediator.SimpleMediator>();

// Domain Events
builder.Services.AddScoped<Domain.Events.IDomainEventDispatcher, Domain.Events.DomainEventDispatcher>();

// Domain Event Handlers - Database Sync
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.CharacterCreatedEvent>, Domain.Events.Handlers.CharacterCreatedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.CharacterUpdatedEvent>, Domain.Events.Handlers.CharacterUpdatedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.CharacterDeletedEvent>, Domain.Events.Handlers.CharacterDeletedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.WorldCreatedEvent>, Domain.Events.Handlers.WorldCreatedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.WorldUpdatedEvent>, Domain.Events.Handlers.WorldUpdatedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.WorldDeletedEvent>, Domain.Events.Handlers.WorldDeletedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.LoreCreatedEvent>, Domain.Events.Handlers.LoreCreatedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.LoreUpdatedEvent>, Domain.Events.Handlers.LoreUpdatedEventHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.LoreDeletedEvent>, Domain.Events.Handlers.LoreDeletedEventHandler>();

// Domain Event Handlers - Conversation Sync
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.ConversationCreatedEvent>, Domain.Events.Handlers.ConversationMetadataHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.UtteranceCreatedEvent>, Domain.Events.Handlers.UtteranceMetadataHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.UtteranceCreatedEvent>, Domain.Events.Handlers.CharacterRelationshipInferenceHandler>();

// Cache invalidation handlers
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.CharacterCreatedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.CharacterUpdatedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.CharacterDeletedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.WorldCreatedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.WorldUpdatedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.WorldDeletedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.LoreCreatedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.LoreUpdatedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();
builder.Services.AddScoped<Domain.Events.IDomainEventHandler<Domain.Events.LoreDeletedEvent>, Domain.Events.Handlers.CacheInvalidationHandler>();

// Command Handlers
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.CreateCharacterCommand, Entities.Character>, Application.Commands.CreateCharacterCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.UpdateCharacterCommand, Entities.Character>, Application.Commands.UpdateCharacterCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.DeleteCharacterCommand>, Application.Commands.DeleteCharacterCommandHandler>();

// Bulk Command Handlers
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.BulkCreateCharactersCommand, Application.Commands.BulkOperationResult<Entities.Character>>, Application.Commands.BulkCreateCharactersCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.BulkUpdateCharactersCommand, Application.Commands.BulkOperationResult<Entities.Character>>, Application.Commands.BulkUpdateCharactersCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.BulkDeleteCharactersCommand, Application.Commands.BulkOperationResult<Application.Commands.Unit>>, Application.Commands.BulkDeleteCharactersCommandHandler>();

// Character Query Handlers - Using Cached Versions
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetCharacterByIdQuery, Entities.Character?>, Application.Queries.CachedGetCharacterByIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetCharactersPagedQuery, (IEnumerable<Entities.Character> Items, int TotalCount)>, Application.Queries.CachedGetCharactersPagedQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetCharactersByAgeRangeQuery, IEnumerable<Entities.Character>>, Application.Queries.CachedGetCharactersByAgeRangeQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.SearchCharactersByNameQuery, IEnumerable<Entities.Character>>, Application.Queries.CachedSearchCharactersByNameQueryHandler>();

// World Command Handlers
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.CreateWorldCommand, Entities.World>, Application.Commands.CreateWorldCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.UpdateWorldCommand, Entities.World>, Application.Commands.UpdateWorldCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.DeleteWorldCommand>, Application.Commands.DeleteWorldCommandHandler>();

// World Query Handlers - Using Cached Versions
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldByIdQuery, Entities.World?>, Application.Queries.CachedGetWorldByIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldsPagedQuery, (IEnumerable<Entities.World> Items, int TotalCount)>, Application.Queries.CachedGetWorldsPagedQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldsWithLoreQuery, IEnumerable<Entities.World>>, Application.Queries.CachedGetWorldsWithLoreQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldWithLoreByIdQuery, Entities.World?>, Application.Queries.CachedGetWorldWithLoreByIdQueryHandler>();

// Lore Command Handlers
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.CreateLoreCommand, Entities.Lore>, Application.Commands.CreateLoreCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.UpdateLoreCommand, Entities.Lore>, Application.Commands.UpdateLoreCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.DeleteLoreCommand>, Application.Commands.DeleteLoreCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.SuggestLoreCommand, Dtos.LoreSuggestResponse>, Application.Commands.SuggestLoreCommandHandler>();

// Lore Query Handlers - Using Cached Versions
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetLoreByIdQuery, Entities.Lore?>, Application.Queries.CachedGetLoreByIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetLoreByWorldIdQuery, IEnumerable<Entities.Lore>>, Application.Queries.CachedGetLoreByWorldIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetGeneratedLoreQuery, IEnumerable<Entities.Lore>>, Application.Queries.CachedGetGeneratedLoreQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.SearchLoreByTextQuery, IEnumerable<Entities.Lore>>, Application.Queries.CachedSearchLoreByTextQueryHandler>();

builder.Services.AddHttpClient<IAgentConversationService, AgentConversationService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<AgentOptions>>();
    if (!string.IsNullOrWhiteSpace(opt.Value.BaseUrl))
        http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(opt.Value.ApiKey))
        http.DefaultRequestHeaders.Add("X-API-Key", opt.Value.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);
}).AddPolicyHandler(AgentPollyPolicies.CreateComposite());

builder.Services.AddHttpClient<ITtsService, TtsService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<TtsOptions>>();
    if (!string.IsNullOrWhiteSpace(opt.Value.BaseUrl))
        http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(opt.Value.ApiKey))
        http.DefaultRequestHeaders.Add("X-API-Key", opt.Value.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);


}).AddPolicyHandler(AgentPollyPolicies.CreateComposite());

builder.Services.AddHttpClient<IImageGenService, ImageGenService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<ImageGenOptions>>();
    if (!string.IsNullOrWhiteSpace(opt.Value.BaseUrl))
        http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(opt.Value.ApiKey))
        http.DefaultRequestHeaders.Add("X-API-Key", opt.Value.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);


}).AddPolicyHandler(AgentPollyPolicies.CreateComposite());


builder.Services.AddScoped<IAgentConversationService, AgentConversationService>();
builder.Services.AddScoped<ITtsService, TtsService>();
builder.Services.AddScoped<IImageGenService, ImageGenService>();
builder.Services.AddScoped<Infrastructure.Audit.IAuditService, Infrastructure.Audit.AuditService>();

var app = builder.Build();

app.UseCors("Default");
app.UseRateLimiter();

// Exception handling middleware (must be early in pipeline)
app.UseExceptionHandler();

app.UseApiKeyAuth();
app.MapControllers().RequireRateLimiting("global");

using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CharacterDbContext>();
    db.Database.Migrate();

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
    ResponseWriter = Infrastructure.HealthChecks.HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = Infrastructure.HealthChecks.HealthCheckResponseWriter.WriteResponse
});

// Detailed health check endpoints
app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db"),
    ResponseWriter = Infrastructure.HealthChecks.HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/cache", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("cache"),
    ResponseWriter = Infrastructure.HealthChecks.HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/external", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("external"),
    ResponseWriter = Infrastructure.HealthChecks.HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = Infrastructure.HealthChecks.HealthCheckResponseWriter.WriteDetailedResponse
});

// Health Checks UI (only in development for security)
if (app.Environment.IsDevelopment())
{
    app.MapHealthChecksUI(options =>
    {
        options.UIPath = "/health-ui";
        options.ApiPath = "/health-ui-api";
    });
}

app.MapControllers();

app.Run();

public partial class Program {}