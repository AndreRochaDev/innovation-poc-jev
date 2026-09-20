using JevClassifier.Api.Models;
using JevClassifier.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<JevOptions>(builder.Configuration.GetSection("Jev"));
builder.Services.Configure<TriageOptions>(builder.Configuration.GetSection("Triage"));

var jevOpts = builder.Configuration.GetSection("Jev").Get<JevOptions>() ?? new JevOptions();
builder.Services.AddHttpClient<IJevClient, JevClient>(c =>
{
    c.BaseAddress = new Uri(jevOpts.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(jevOpts.TimeoutSeconds);
});
builder.Services.AddScoped<TriageService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();
