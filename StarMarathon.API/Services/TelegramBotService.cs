using Microsoft.EntityFrameworkCore;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;


namespace StarMarathon.API.Services;

public class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TelegramBotService(IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("BOT_TOKEN не найден! Бот не запустится.");
            _botClient = null!;
        }
        else
        {
            _botClient = new TelegramBotClient(token);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botClient == null) return;

        // ИСПРАВЛЕНИЕ 1: DeleteWebhookAsync -> DeleteWebhook
        try { await _botClient.DeleteWebhook(cancellationToken: stoppingToken); } catch { }

        _logger.LogInformation("Telegram бот запущен и ожидает сообщения...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message] // Синтаксис массива C# 12
        };

        // ИСПРАВЛЕНИЕ 2: pollingErrorHandler -> errorHandler
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync, // <-- Имя параметра изменилось
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message) return;
        long chatId = message.Chat.Id;

        // 1. Обработка команды /start
        if (message.Text != null && message.Text.StartsWith("/start"))
        {
            // ИСПРАВЛЕНИЕ 3: Создание кнопки через WithRequestContact
            var keyboard = new ReplyKeyboardMarkup(true) // true = ResizeKeyboard
            {
                Keyboard = new[]
                {
                    new[] { KeyboardButton.WithRequestContact("📱 Поделиться номером телефона") }
                },
                OneTimeKeyboard = true
            };

            // ИСПРАВЛЕНИЕ 4: SendTextMessageAsync -> SendMessage
            await botClient.SendMessage(
                chatId: chatId,
                text: "Для завершения регистрации, пожалуйста, нажмите кнопку ниже, чтобы отправить свой номер телефона.",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );
            return;
        }

        // 2. Обработка Контакта
        if (message.Contact is not { } contact) return;

        if (contact.UserId != message.From?.Id)
        {
            await botClient.SendMessage(chatId, "Пожалуйста, отправьте СВОЙ номер телефона через кнопку меню.", cancellationToken: cancellationToken);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StarDbContext>();

        try
        {
            long telegramId = contact.UserId ?? message.From!.Id;
            string phoneNumber = contact.PhoneNumber;

            if (!phoneNumber.StartsWith("+")) phoneNumber = "+" + phoneNumber;

            _logger.LogInformation($"Получен контакт: {telegramId} - {phoneNumber}");

            var user = await db.Profiles.FirstOrDefaultAsync(u => u.Id == telegramId, cancellationToken);

            if (user == null)
            {
                user = new UserProfile
                {
                    Id = telegramId,
                    Username = message.From?.Username ?? "",
                    Role = "user",
                    LanguageCode = "ru",
                    PhoneNumber = phoneNumber
                };
                db.Profiles.Add(user);
            }
            else
            {
                user.PhoneNumber = phoneNumber;
                if (!string.IsNullOrEmpty(message.From?.Username))
                    user.Username = message.From.Username;
            }

            await db.SaveChangesAsync(cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: "✅ Номер успешно принят! Вернитесь в приложение StarMarathon.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении контакта");
            await botClient.SendMessage(chatId, "Произошла ошибка при сохранении. Попробуйте еще раз.", cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram API Error");
        return Task.CompletedTask; 
    }
}