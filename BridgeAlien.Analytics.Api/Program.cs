using BridgeAlien.Analytics.Api.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Railway가 주입하는 PORT 환경변수 대응
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 연결 문자열: appsettings → DATABASE_URL(Railway) 순서로 탐색
var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("PostgreSQL 연결 문자열이 설정되지 않았습니다.");

builder.Services.AddSingleton(new AnalyticsRepository(connectionString));

var app = builder.Build();

app.UseExceptionHandler(err => err.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { error = ex?.Message, detail = ex?.ToString() });
}));

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok("healthy"));

app.UseAuthorization();
app.MapControllers();

app.Run();
