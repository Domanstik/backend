using Microsoft.AspNetCore.Http;
using StarMarathon.Application.Interfaces;
using Supabase;

namespace StarMarathon.Infrastructure.Services;

public class SupabaseStorageService : IFileStorageService
{
    private readonly Client _client;

    public SupabaseStorageService(Client client)
    {
        _client = client;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string bucketName = "images")
    {
        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
        // Достаем расширение (например, ".png")
        var extension = Path.GetExtension(file.FileName);

        // Генерируем 100% безопасное имя: e5f6-7890.png
        var fileName = $"{Guid.NewGuid()}{extension}";

        // Преобразуем файл в байты
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        // Загружаем в Supabase Storage
        await _client.Storage.From(bucketName).Upload(bytes, fileName);

        // Получаем публичную ссылку
        return _client.Storage.From(bucketName).GetPublicUrl(fileName);
    }
}