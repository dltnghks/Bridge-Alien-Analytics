using BridgeAlien.Analytics.Api.Repositories;

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

app.MapOpenApi();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "Bridge-Alien Analytics API"));

app.UseAuthorization();
app.MapControllers();

app.Run();
