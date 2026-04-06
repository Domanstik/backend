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
// === TEMPORARY STORM API TEST BLOCK (DELETE AFTER VERIFICATION) ===
// =========================================================================
try
{
    Console.WriteLine(">>> [STORM TEST] STARTING TEST REQUESTS <<<");
    using var testClient = new HttpClient();

    // 1. Test authLogin
    var authLoginRequest = new { auth_jwt = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJqdGkiOiJRVmlWMHZjWWg4dExJRTc4djlHdVZBIiwiaWF0IjoxNzc1NDkwMzQ4LCJzdWIiOiJhdXRoIiwiZGlkIjoiMSIsInRpZCI6IjY0NTM3MDQzMyJ9.x1v3TDWy4BlK2mjB4GeEbv3gP2GD1feRVRKD-rxpDH0" };
    var loginJson = System.Text.Json.JsonSerializer.Serialize(authLoginRequest);
    var loginContent = new StringContent(loginJson, System.Text.Encoding.UTF8, "application/json");

    Console.WriteLine(">>> [STORM TEST] Sending POST to authLogin...");
    var loginResponse = await testClient.PostAsync("https://stars1.stormbv.com/api/authLogin", loginContent);
    var loginResponseText = await loginResponse.Content.ReadAsStringAsync();

    Console.WriteLine($">>> [STORM TEST] authLogin Response: {loginResponse.StatusCode}");
    Console.WriteLine($">>> [STORM TEST] authLogin Body: {loginResponseText}");

    // Proceed if login is successful and we got a session token
    if (loginResponse.IsSuccessStatusCode && loginResponseText.Contains("session_jwt"))
    {
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(loginResponseText);
        var sessionJwt = jsonDoc.RootElement.GetProperty("session_jwt").GetString();

        // Prepare the payload (session_jwt) used for all subsequent requests
        var commonRequest = new { session_jwt = sessionJwt };
        var commonJson = System.Text.Json.JsonSerializer.Serialize(commonRequest);

        // 2. Test getProfile
        Console.WriteLine(">>> [STORM TEST] Sending POST to getProfile...");
        var profileContent = new StringContent(commonJson, System.Text.Encoding.UTF8, "application/json");
        var profileResponse = await testClient.PostAsync("https://stars1.stormbv.com/api/getProfile", profileContent);
        var profileResponseText = await profileResponse.Content.ReadAsStringAsync();

        Console.WriteLine($">>> [STORM TEST] getProfile Response: {profileResponse.StatusCode}");
        Console.WriteLine($">>> [STORM TEST] getProfile Body: {profileResponseText}");

        // 3. Test getRating (Leaderboard)
        Console.WriteLine(">>> [STORM TEST] Sending POST to getRating...");
        var ratingContent = new StringContent(commonJson, System.Text.Encoding.UTF8, "application/json");
        var ratingResponse = await testClient.PostAsync("https://stars1.stormbv.com/api/getRating", ratingContent);
        var ratingResponseText = await ratingResponse.Content.ReadAsStringAsync();

        Console.WriteLine($">>> [STORM TEST] getRating Response: {ratingResponse.StatusCode}");
        Console.WriteLine($">>> [STORM TEST] getRating Body: {ratingResponseText}");

        // 4. Test getTransactions (History)
        Console.WriteLine(">>> [STORM TEST] Sending POST to getTransactions...");
        var txContent = new StringContent(commonJson, System.Text.Encoding.UTF8, "application/json");
        var txResponse = await testClient.PostAsync("https://stars1.stormbv.com/api/getTransactions", txContent);
        var txResponseText = await txResponse.Content.ReadAsStringAsync();

        Console.WriteLine($">>> [STORM TEST] getTransactions Response: {txResponse.StatusCode}");
        Console.WriteLine($">>> [STORM TEST] getTransactions Body: {txResponseText}");
    }
    else
    {
        Console.WriteLine(">>> [STORM TEST] Skipping next steps because authLogin failed or missing session_jwt.");
    }

    Console.WriteLine(">>> [STORM TEST] END OF TEST <<<");
}
catch (Exception ex)
{
    Console.WriteLine($">>> [STORM TEST] ERROR: {ex.Message}");
}
// =========================================================================
// === END OF TEST BLOCK ===
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