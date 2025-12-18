using Microsoft.AspNetCore.Http;

namespace StarMarathon.Application.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Загружает файл в облако и возвращает публичную ссылку.
    /// </summary>
    Task<string> UploadFileAsync(IFormFile file, string bucketName = "images");
}