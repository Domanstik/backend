using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StarMarathon.Application.Interfaces;

namespace StarMarathon.Infrastructure.Services;

public class StormBvService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ILogger<StormBvService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public StormBvService(IHttpClientFactory httpClientFactory, ILogger<StormBvService> logger)
    {
        _http = httpClientFactory.CreateClient("StormAPI");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    // 1. Вход
    public async Task<(string? SessionToken, string? AuthToken)> LoginAsync(long tgId, string pin, string? username, string? phone)
    {
        try
        {
            var regPayload = new { pin, tg_id = tgId, phone, username };

            _logger.LogInformation($"[StormAPI] AuthRegister: PIN={pin}, TG={tgId}");

            var regRes = await _http.PostAsJsonAsync("authRegister", regPayload);
            if (!regRes.IsSuccessStatusCode) return (null, null);

            var regData = await regRes.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
            var authJwt = regData?.auth_jwt;

            if (string.IsNullOrEmpty(authJwt)) return (null, null);

            var loginPayload = new { auth_jwt = authJwt };
            var loginRes = await _http.PostAsJsonAsync("authLogin", loginPayload);

            if (!loginRes.IsSuccessStatusCode) return (null, authJwt);

            var loginData = await loginRes.Content.ReadFromJsonAsync<SessionResponse>(_jsonOptions);
            return (loginData?.session_jwt, authJwt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StormAPI] Login error");
            return (null, null);
        }
    }

    // --- ВАЖНО: Хелпер для получения свежего токена сессии ---
    private async Task<string?> GetSessionTokenAsync(string authJwt)
    {
        if (string.IsNullOrEmpty(authJwt)) return null;

        var res = await PostJson("authLogin", new { auth_jwt = authJwt });
        if (res == null) return null;

        var data = await res.Content.ReadFromJsonAsync<SessionResponse>(_jsonOptions);
        return data?.session_jwt;
    }

    // 2. Профиль
    public async Task<UserProfileDto> GetProfileAsync(string authJwt) // Принимаем authJwt
    {
        // Сначала получаем "свежий" токен на 1 час
        var sessionToken = await GetSessionTokenAsync(authJwt);
        if (sessionToken == null) return new UserProfileDto("Неизвестный", 0);

        var res = await PostJson("getProfile", new { session_jwt = sessionToken });
        if (res == null) return new UserProfileDto("Неизвестный", 0);

        var data = await res.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        return data ?? new UserProfileDto("Ошибка", 0);
    }

    // 3. Рейтинг
    public async Task<List<RatingItemDto>> GetRatingAsync(string authJwt)
    {
        var sessionToken = await GetSessionTokenAsync(authJwt);
        if (sessionToken == null) return new();

        var res = await PostJson("getRating", new { session_jwt = sessionToken });
        if (res == null) return new();

        return await res.Content.ReadFromJsonAsync<List<RatingItemDto>>(_jsonOptions) ?? new();
    }

    // 4. Транзакции
    public async Task<List<TransactionDto>> GetTransactionsAsync(string authJwt)
    {
        var sessionToken = await GetSessionTokenAsync(authJwt);
        if (sessionToken == null) return new();

        var res = await PostJson("getTransactions", new { session_jwt = sessionToken });
        if (res == null) return new();

        return await res.Content.ReadFromJsonAsync<List<TransactionDto>>(_jsonOptions) ?? new();
    }

    // 5. Добавить транзакцию
    public async Task<bool> AddTransactionAsync(string authJwt, int amount, string description)
    {
        var sessionToken = await GetSessionTokenAsync(authJwt);
        if (sessionToken == null) return false;

        var res = await PostJson("addTransaction", new { session_jwt = sessionToken, amount, descr = description });
        return res != null && res.IsSuccessStatusCode;
    }

    // --- Хелперы ---
    private async Task<HttpResponseMessage?> PostJson(string url, object body)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, body);
            if (response.IsSuccessStatusCode) return response;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[StormAPI] Error calling {url}");
            return null;
        }
    }

    // Внутренние классы для Auth
    private class AuthResponse { public string auth_jwt { get; set; } }
    private class SessionResponse { public string session_jwt { get; set; } }
}