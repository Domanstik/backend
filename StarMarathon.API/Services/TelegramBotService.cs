using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using StarMarathon.Infrastructure.Persistence;
using StarMarathon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace StarMarathon.API.Services;

public sealed class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TelegramBotService(
        IServiceProvider serviceProvider,
        ILogger<TelegramBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("BOT_TOKEN не найден. Telegram-бот отключён.");
            _botClient = null!;
            return;
        }

        _botClient = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botClient == null)
            return;

        _logger.LogInformation("Telegram бот запущен");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken ct)
    {
        if (update.Message is not { } message)
            return;

        if (message.Contact is not { } contact)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StarDbContext>();

        try
        {
            long telegramId = contact.UserId ?? message.From!.Id;
            string phoneNumber = contact.PhoneNumber;

            if (!phoneNumber.StartsWith("+"))
                phoneNumber = "+" + phoneNumber;

            _logger.LogInformation("Контакт получен: {Id} {Phone}", telegramId, phoneNumber);

            var user = await db.Profiles
                .FirstOrDefaultAsync(u => u.Id == telegramId, ct);

            if (user == null)
            {
                user = new UserProfile
                {
                    Id = telegramId,
                    Username = message.From?.Username ?? "",
                    Role = "user",
                    LanguageCode = "ru"
                    // PhoneNumber = phoneNumber (после добавления поля)
                };

                db.Profiles.Add(user);
            }
            else
            {
                // user.PhoneNumber = phoneNumber;
            }

            await db.SaveChangesAsync(ct);

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Спасибо! Ваш номер подтверждён. Возвращайтесь в приложение.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки контакта");
        }
    }

    private Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}
