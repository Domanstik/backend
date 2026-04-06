using DotNetEnv;
using StarMarathon.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StarMarathon.Application.Interfaces;
using StarMarathon.Infrastructure.Persistence;
using StarMarathon.Infrastructure.Services;
using Supabase;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Загружаем переменные (для локальной разработки)
Env.Load();

// Получаем настройки
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new Exception("DATABASE_URL не найден");
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? throw new Exception("SUPABASE_URL не найден");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
    ?? throw new Exception("SUPABASE_KEY не найден");
var jwtKeyString = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? throw new Exception("JWT_KEY не найден");

// 2. БД
builder.Services.AddDbContext<StarDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Supabase
var supabaseOptions = new SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = false };
var supabaseClient = new Client(supabaseUrl, supabaseKey, supabaseOptions);
await supabaseClient.InitializeAsync();
builder.Services.AddSingleton(supabaseClient);

// 4. Сервисы
builder.Services.AddScoped<IFileStorageService, SupabaseStorageService>();
builder.Services.AddHttpClient("StormAPI", client =>
{
    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("STORM_API_URL") ?? "https://stars1.stormbv.com/api/");
});
builder.Services.AddScoped<IAuthService, StormBvService>();

// 5. JWT
var jwtKey = Encoding.UTF8.GetBytes(jwtKeyString);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = "StarMarathon",
            ValidAudience = "StarMarathonClient",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey)
        };
    });

// 6. Контроллеры и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // (Сократил настройку для краткости, твоя полная версия тоже ок)

// --- ВАЖНО: Настройка CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", 
        b => b.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Бот
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

// =========================================================================
// === ВРЕМЕННЫЙ ТЕСТОВЫЙ БЛОК STORM API (УДАЛИТЬ ПОСЛЕ ПРОВЕРКИ) ===
// =========================================================================
try
{
    Console.WriteLine(">>> [STORM TEST] НАЧАЛО ТЕСТОВОГО ЗАПРОСА <<<");
    using var testClient = new HttpClient();

    // 1. Тестируем authLogin
    var authLoginRequest = new { auth_jwt = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJqdGkiOiJFRWltMDJ4dmlFWWo4RFBSVjBibnZRIiwiaWF0IjoxNzc1NDU0ODI5LCJzdWIiOiJhdXRoIiwiZGlkIjoiMSIsInRpZCI6IklEXzEwMzYifQ.EHAX6xF9z6lzYOCqSXybkqno-67v6S0dUH5yf3xixFk" };
    var loginJson = System.Text.Json.JsonSerializer.Serialize(authLoginRequest);
    var loginContent = new StringContent(loginJson, System.Text.Encoding.UTF8, "application/json");

    Console.WriteLine(">>> [STORM TEST] Отправляем POST на authLogin...");
    var loginResponse = await testClient.PostAsync("https://stars1.stormbv.com/api/authLogin", loginContent);
    var loginResponseText = await loginResponse.Content.ReadAsStringAsync();

    Console.WriteLine($">>> [STORM TEST] Ответ authLogin: {loginResponse.StatusCode}");
    Console.WriteLine($">>> [STORM TEST] Тело: {loginResponseText}");

    // 2. Тестируем getProfile (если логин прошел)
    if (loginResponse.IsSuccessStatusCode && loginResponseText.Contains("session_jwt"))
    {
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(loginResponseText);
        var sessionJwt = jsonDoc.RootElement.GetProperty("session_jwt").GetString();

        var profileRequest = new { session_jwt = sessionJwt };
        var profileJson = System.Text.Json.JsonSerializer.Serialize(profileRequest);
        var profileContent = new StringContent(profileJson, System.Text.Encoding.UTF8, "application/json");

        Console.WriteLine(">>> [STORM TEST] Отправляем POST на getProfile...");
        var profileResponse = await testClient.PostAsync("https://stars1.stormbv.com/api/getProfile", profileContent);
        var profileResponseText = await profileResponse.Content.ReadAsStringAsync();

        Console.WriteLine($">>> [STORM TEST] Ответ getProfile: {profileResponse.StatusCode}");
        Console.WriteLine($">>> [STORM TEST] Тело: {profileResponseText}");
    }
    Console.WriteLine(">>> [STORM TEST] КОНЕЦ ТЕСТА <<<");
}
catch (Exception ex)
{
    Console.WriteLine($">>> [STORM TEST] ОШИБКА: {ex.Message}");
}
// =========================================================================
// === КОНЕЦ ТЕСТОВОГО БЛОКА ===
// =========================================================================

// --- ВАЖНО: Swagger работает ВСЕГДА (даже на Render) ---
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// --- ВАЖНО: CORS перед Auth ---
app.UseCors("AllowAll"); // <--- Поменяй AllowFirebase на AllowAll

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Render требует слушать порт из переменной PORT
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");