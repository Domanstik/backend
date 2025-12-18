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
        // Генерируем имя: timestamp_filename.jpg
        var fileName = $"{DateTime.UtcNow.Ticks}_{file.FileName}";

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