using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Infrastructure.Persistence;
using StarMarathon.Domain.Entities;

namespace StarMarathon.API.Services;

public sealed class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceProvider _sp;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(IServiceProvider sp, ILogger<TelegramBotService> logger)
    {
        _sp = sp;
        _logger = logger;

        var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("BOT_TOKEN not set, bot disabled");
            _bot = null!;
            return;
        }

        _bot = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_bot == null) return;

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message } },
            stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } msg) return;

        // 🔹 Команда из Mini App
        if (msg.Text == "/start share_phone")
        {
            await bot.SendMessage(
                msg.Chat.Id,
                "Нажмите кнопку ниже, чтобы подтвердить номер телефона",
                replyMarkup: new ReplyKeyboardMarkup(
                    KeyboardButton.WithRequestContact("📱 Поделиться номером")
                )
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                },
                cancellationToken: ct
            );
            return;
        }

        // 🔹 Пришёл контакт
        if (msg.Contact is not { } contact) return;

        if (contact.UserId != msg.From?.Id)
        {
            _logger.LogWarning("Contact UserId mismatch");
            return;
        }

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StarDbContext>();

        var phone = contact.PhoneNumber.StartsWith("+")
            ? contact.PhoneNumber
            : "+" + contact.PhoneNumber;

        var user = await db.Profiles.FirstOrDefaultAsync(u => u.Id == contact.UserId, ct);

        if (user == null)
        {
            user = new UserProfile
            {
                Id = contact.UserId.Value,
                Username = msg.From?.Username ?? "",
                Role = "user",
                LanguageCode = "ru",
                PhoneNumber = phone
            };
            db.Profiles.Add(user);
        }
        else
        {
            user.PhoneNumber = phone;
        }

        await db.SaveChangesAsync(ct);

        await bot.SendMessage(
            msg.Chat.Id,
            "Спасибо! Номер подтверждён. Вернитесь в приложение.",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct
        );
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Telegram bot error");
        return Task.CompletedTask;
    }
}
