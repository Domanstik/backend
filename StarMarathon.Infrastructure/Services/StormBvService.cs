using System.Net.Http.Json;
using Microsoft.Extensions.Logging; // Добавил логгер
using StarMarathon.Application.Interfaces;

namespace StarMarathon.Infrastructure.Services;

public class StormBvService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ILogger<StormBvService> _logger; // Логгер

    public StormBvService(IHttpClientFactory httpClientFactory, ILogger<StormBvService> logger)
    {
        _http = httpClientFactory.CreateClient("StormAPI");
        _logger = logger;
    }

    public async Task<(string? SessionToken, string? AuthToken)> LoginAsync(long tgId, string pin, string? username, string? phone)
    {
        try
        {
            // ШАГ 1: Register
            var regPayload = new { pin, tg_id = tgId, phone, username };

            _logger.LogInformation($"[StormAPI] Отправка authRegister: PIN={pin}, Phone={phone}, TG={tgId}");

            var regRes = await _http.PostAsJsonAsync("authRegister", regPayload);
            var regBody = await regRes.Content.ReadAsStringAsync(); // Читаем ответ текстом

            _logger.LogInformation($"[StormAPI] Ответ authRegister ({regRes.StatusCode}): {regBody}");

            if (!regRes.IsSuccessStatusCode) return (null, null);

            // Парсим
            var regData = System.Text.Json.JsonSerializer.Deserialize<AuthResponse>(regBody); // Используем System.Text.Json
            var authJwt = regData?.auth_jwt;

            if (string.IsNullOrEmpty(authJwt))
            {
                _logger.LogWarning("[StormAPI] auth_jwt пустой!");
                return (null, null);
            }

            // ШАГ 2: Login
            var loginPayload = new { auth_jwt = authJwt };
            var loginRes = await _http.PostAsJsonAsync("authLogin", loginPayload);
            var loginBody = await loginRes.Content.ReadAsStringAsync();

            _logger.LogInformation($"[StormAPI] Ответ authLogin ({loginRes.StatusCode}): {loginBody}");

            if (!loginRes.IsSuccessStatusCode) return (null, authJwt);

            var loginData = System.Text.Json.JsonSerializer.Deserialize<SessionResponse>(loginBody);
            return (loginData?.session_jwt, authJwt);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[StormAPI] Ошибка запроса: {ex.Message}");
            return (null, null);
        }
    }

    // Остальные методы без изменений, но используй PostJsonHelper если нужно
    public async Task<int> GetBalanceAsync(string externalSessionToken)
    {
        var res = await PostJson("getProfile", new { session_jwt = externalSessionToken });
        if (res == null) return 0;
        var data = await res.Content.ReadFromJsonAsync<ProfileResponse>();
        return data?.balance ?? 0;
    }

    public async Task<List<ExternalTransaction>> GetTransactionsAsync(string extToken)
    {
        var res = await PostJson("getTransactions", new { session_jwt = extToken });
        if (res == null) return new();
        return await res.Content.ReadFromJsonAsync<List<ExternalTransaction>>() ?? new();
    }

    public async Task<List<LeaderboardItem>> GetRatingAsync(string extToken)
    {
        var res = await PostJson("getRating", new { session_jwt = extToken });
        if (res == null) return new();
        return await res.Content.ReadFromJsonAsync<List<LeaderboardItem>>() ?? new();
    }

    public async Task<bool> AddTransactionAsync(string extToken, int amount, string description)
    {
        var res = await PostJson("addTransaction", new { session_jwt = extToken, amount, descr = description });
        return res != null && res.IsSuccessStatusCode;
    }

    private async Task<HttpResponseMessage?> PostJson(string url, object body)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, body);
            if (response.IsSuccessStatusCode) return response;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private class AuthResponse { public string auth_jwt { get; set; } }
    private class SessionResponse { public string session_jwt { get; set; } }
    private class ProfileResponse { public string fio { get; set; } public int balance { get; set; } }
}