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

// 1. Загружаем переменные из .env
Env.Load();

// Получаем настройки
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new Exception("DATABASE_URL не найден в .env");
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
    ?? throw new Exception("SUPABASE_URL не найден в .env");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
    ?? throw new Exception("SUPABASE_KEY не найден в .env");
var jwtKeyString = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? throw new Exception("JWT_KEY не найден в .env");

// 2. Подключаем Базу Данных (PostgreSQL)
builder.Services.AddDbContext<StarDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Подключаем Supabase Client (для файлов)
var supabaseOptions = new SupabaseOptions
{
    AutoRefreshToken = true,
    AutoConnectRealtime = false
};
var supabaseClient = new Client(supabaseUrl, supabaseKey, supabaseOptions);
await supabaseClient.InitializeAsync();
builder.Services.AddSingleton(supabaseClient);

// 4. Регистрируем наши сервисы
builder.Services.AddScoped<IFileStorageService, SupabaseStorageService>();

// Регистрация StormBV сервиса (Внешнее API)
builder.Services.AddHttpClient("StormAPI", client =>
{
    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("STORM_API_URL") ?? "https://stars1.stormbv.com/api/");
});
builder.Services.AddScoped<IAuthService, StormBvService>();

// 5. Настройка JWT Auth
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

// --- НАСТРОЙКА SWAGGER (С полными именами классов) ---
builder.Services.AddSwaggerGen(c =>
{
    // Описываем схему авторизации
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Введите токен в формате: Bearer {ваш_токен}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Требование авторизации
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});
// ---------------------------------------------------

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

// Включаем Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Middleware авторизации (порядок важен!)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();