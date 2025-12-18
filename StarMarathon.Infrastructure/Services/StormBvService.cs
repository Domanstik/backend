using System.Net.Http.Json;
using StarMarathon.Application.Interfaces;

namespace StarMarathon.Infrastructure.Services;

public class StormBvService : IAuthService
{
    private readonly HttpClient _http;

    public StormBvService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("StormAPI");
    }

    // 1. Вход: Register -> Login
    public async Task<(string? SessionToken, string? AuthToken)> LoginAsync(long tgId, string pin, string? username, string? phone)
    {
        try
        {
            // ШАГ 1: Register (Получаем auth_jwt)
            var regPayload = new { pin, tg_id = tgId, phone, username };
            var regRes = await _http.PostAsJsonAsync("authRegister", regPayload);

            if (!regRes.IsSuccessStatusCode) return (null, null);
            // Если 204 - неверный пин
            if (regRes.StatusCode == System.Net.HttpStatusCode.NoContent) return (null, null);

            var regData = await regRes.Content.ReadFromJsonAsync<AuthResponse>();
            var authJwt = regData?.auth_jwt;

            if (string.IsNullOrEmpty(authJwt)) return (null, null);

            // ШАГ 2: Login (Получаем session_jwt)
            var loginPayload = new { auth_jwt = authJwt };
            var loginRes = await _http.PostAsJsonAsync("authLogin", loginPayload);

            if (!loginRes.IsSuccessStatusCode) return (null, authJwt);

            var loginData = await loginRes.Content.ReadFromJsonAsync<SessionResponse>();
            return (loginData?.session_jwt, authJwt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StormAPI Error: {ex.Message}");
            return (null, null);
        }
    }

    // 2. Профиль
    public async Task<UserProfileDto> GetProfileAsync(string sessionToken)
    {
        var res = await PostJson("getProfile", new { session_jwt = sessionToken });
        if (res == null) return new UserProfileDto("Неизвестный", 0);

        var data = await res.Content.ReadFromJsonAsync<ProfileResponse>();
        return new UserProfileDto(data?.fio ?? "", data?.balance ?? 0);
    }

    // 3. Рейтинг
    public async Task<List<RatingItemDto>> GetRatingAsync(string sessionToken)
    {
        var res = await PostJson("getRating", new { session_jwt = sessionToken });
        if (res == null) return new();
        return await res.Content.ReadFromJsonAsync<List<RatingItemDto>>() ?? new();
    }

    // 4. Транзакции
    public async Task<List<TransactionDto>> GetTransactionsAsync(string sessionToken)
    {
        var res = await PostJson("getTransactions", new { session_jwt = sessionToken });
        if (res == null) return new();
        return await res.Content.ReadFromJsonAsync<List<TransactionDto>>() ?? new();
    }

    // 5. Добавить транзакцию (Покупка/Награда)
    public async Task<bool> AddTransactionAsync(string sessionToken, int amount, string description)
    {
        var res = await PostJson("addTransaction", new { session_jwt = sessionToken, amount, descr = description });

        // 200 OK - успешно
        // 204 - Person not found
        // 400/401/500 - ошибки
        return res != null && res.IsSuccessStatusCode;
    }

    // --- Вспомогательный метод ---
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

    // Внутренние классы для парсинга JSON ответов API
    private class AuthResponse { public string auth_jwt { get; set; } }
    private class SessionResponse { public string session_jwt { get; set; } }
    private class ProfileResponse { public string fio { get; set; } public int balance { get; set; } }
}