using System.IO.Compression;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Writers;
using Neo4j.Driver;
using Npc.Api.Data;
using Npc.Api.Infrastructure.Http;
using Npc.Api.Services;
using Npc.Api.Services.Impl;

var builder = WebApplication.CreateBuilder(args);

var neo4jConf = builder.Configuration.GetSection("Neo4j");
var neoUri = neo4jConf.GetValue<string>("Uri");
var neoUser = neo4jConf.GetValue<string>("User");
var neoPwd = neo4jConf.GetValue<string>("Password");
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();


builder.Services.AddDbContext<CharacterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IModerationAgent, ModerationAgentService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(neoUri, AuthTokens.Basic(neoUser, neoPwd)));
builder.Services.AddScoped<IConversationGraphService, ConversationGraphService>();

builder.Services.AddHttpClient<IAgentConversationService, AgentConversationService>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<AgentOptions>>();
    http.BaseAddress = new Uri(opt.Value.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(opt.Value.Timeout <= 0 ? 15 : opt.Value.Timeout);
}).AddPolicyHandler(AgentPollyPolicies.CreateComposite());

builder.Services.AddScoped<IAgentConversationService, AgentConversationService>();

var app = builder.Build();
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
