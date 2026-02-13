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
    options.AddPolicy("AllowFirebase", policy =>
    {
        policy.WithOrigins(
                "https://star-mini-app.web.app",           // Твой Firebase
                "https://star-mini-app.firebaseapp.com",   // Зеркало Firebase
                "http://localhost:5173",                   // Локальный фронт
                "https://localhost:5173"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Разрешаем куки/авторизацию
    });
});

// Бот
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

// --- ВАЖНО: Swagger работает ВСЕГДА (даже на Render) ---
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// --- ВАЖНО: CORS перед Auth ---
app.UseCors("AllowFirebase");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Render требует слушать порт из переменной PORT
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");