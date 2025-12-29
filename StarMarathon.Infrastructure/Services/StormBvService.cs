using System.Net.Http.Json;
using System.Text.Json; // Для JsonSerializerOptions
using Microsoft.Extensions.Logging;
using StarMarathon.Application.Interfaces; // Тут лежат ваши DTO и Интерфейс

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
        // Чтобы "fio" из JSON мапилось в "Fio" в C#
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

    // 2. Профиль
    public async Task<UserProfileDto> GetProfileAsync(string sessionToken)
    {
        var res = await PostJson("getProfile", new { session_jwt = sessionToken });
        if (res == null) return new UserProfileDto("Неизвестный", 0);

        // Десериализуем сразу в DTO из интерфейса
        var data = await res.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        return data ?? new UserProfileDto("Ошибка", 0);
    }

    // 3. Рейтинг
    public async Task<List<RatingItemDto>> GetRatingAsync(string sessionToken)
    {
        var res = await PostJson("getRating", new { session_jwt = sessionToken });
        if (res == null) return new();

        return await res.Content.ReadFromJsonAsync<List<RatingItemDto>>(_jsonOptions) ?? new();
    }

    // 4. Транзакции
    public async Task<List<TransactionDto>> GetTransactionsAsync(string sessionToken)
    {
        var res = await PostJson("getTransactions", new { session_jwt = sessionToken });
        if (res == null) return new();

        return await res.Content.ReadFromJsonAsync<List<TransactionDto>>(_jsonOptions) ?? new();
    }

    // 5. Добавить транзакцию
    public async Task<bool> AddTransactionAsync(string sessionToken, int amount, string description)
    {
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

    // Внутренние классы для Auth (так как их структура уникальна и не используется в UI)
    private class AuthResponse { public string auth_jwt { get; set; } }
    private class SessionResponse { public string session_jwt { get; set; } }
}