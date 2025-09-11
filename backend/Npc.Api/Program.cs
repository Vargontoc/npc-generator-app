using System.IO.Compression;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Services;
using Npc.Api.Services.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();


builder.Services.AddDbContext<CharacterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IModerationAgent, ModerationAgentService>();
builder.Services.AddScoped<IModerationService, ModerationService>();


var app = builder.Build();
using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CharacterDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
