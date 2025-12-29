namespace StarMarathon.Application.Interfaces;

public interface IAuthService
{
    // Возвращает кортеж: (session_jwt, auth_jwt)
    Task<(string? SessionToken, string? AuthToken)> LoginAsync(long tgId, string pin, string? username, string? phone);

    Task<UserProfileDto> GetProfileAsync(string sessionToken);
    Task<List<RatingItemDto>> GetRatingAsync(string sessionToken);
    Task<List<TransactionDto>> GetTransactionsAsync(string sessionToken);

    // amount > 0 (начисление), amount < 0 (списание)
    Task<bool> AddTransactionAsync(string sessionToken, int amount, string description);
}

// DTOs для ответов (public records)
public record UserProfileDto(string Fio, int Balance);
public record RatingItemDto(string Fio, int Balance, int Place);
public record TransactionDto(string Date, int Amount, string Type, string Descr);