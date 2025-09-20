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
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "ready" });


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

// Character Query Handlers
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetCharacterByIdQuery, Entities.Character?>, Application.Queries.GetCharacterByIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetCharactersPagedQuery, (IEnumerable<Entities.Character> Items, int TotalCount)>, Application.Queries.GetCharactersPagedQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetCharactersByAgeRangeQuery, IEnumerable<Entities.Character>>, Application.Queries.GetCharactersByAgeRangeQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.SearchCharactersByNameQuery, IEnumerable<Entities.Character>>, Application.Queries.SearchCharactersByNameQueryHandler>();

// World Command Handlers
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.CreateWorldCommand, Entities.World>, Application.Commands.CreateWorldCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.UpdateWorldCommand, Entities.World>, Application.Commands.UpdateWorldCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.DeleteWorldCommand>, Application.Commands.DeleteWorldCommandHandler>();

// World Query Handlers
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldByIdQuery, Entities.World?>, Application.Queries.GetWorldByIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldsPagedQuery, (IEnumerable<Entities.World> Items, int TotalCount)>, Application.Queries.GetWorldsPagedQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldsWithLoreQuery, IEnumerable<Entities.World>>, Application.Queries.GetWorldsWithLoreQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetWorldWithLoreByIdQuery, Entities.World?>, Application.Queries.GetWorldWithLoreByIdQueryHandler>();

// Lore Command Handlers
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.CreateLoreCommand, Entities.Lore>, Application.Commands.CreateLoreCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.UpdateLoreCommand, Entities.Lore>, Application.Commands.UpdateLoreCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.DeleteLoreCommand>, Application.Commands.DeleteLoreCommandHandler>();
builder.Services.AddScoped<Application.Commands.ICommandHandler<Application.Commands.SuggestLoreCommand, Dtos.LoreSuggestResponse>, Application.Commands.SuggestLoreCommandHandler>();

// Lore Query Handlers
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetLoreByIdQuery, Entities.Lore?>, Application.Queries.GetLoreByIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetLoreByWorldIdQuery, IEnumerable<Entities.Lore>>, Application.Queries.GetLoreByWorldIdQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.GetGeneratedLoreQuery, IEnumerable<Entities.Lore>>, Application.Queries.GetGeneratedLoreQueryHandler>();
builder.Services.AddScoped<Application.Queries.IQueryHandler<Application.Queries.SearchLoreByTextQuery, IEnumerable<Entities.Lore>>, Application.Queries.SearchLoreByTextQueryHandler>();

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

app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new() { Predicate = _ => true });

app.MapControllers();

app.Run();

public partial class Program {}