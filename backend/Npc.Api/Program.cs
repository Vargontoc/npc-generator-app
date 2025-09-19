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