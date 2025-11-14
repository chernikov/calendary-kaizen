using System.Net;
using System.Text.Json;
using CalendaryKaizen.Models;
using CalendaryKaizen.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Functions;

public class UploadImagesFunction
{
    private readonly IStorageService _storageService;
    private readonly ILogger<UploadImagesFunction> _logger;
    private readonly HttpClient _httpClient;

    public UploadImagesFunction(
        IStorageService storageService,
        ILogger<UploadImagesFunction> logger)
    {
        _storageService = storageService;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    [Function("UploadImages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("UploadImages function triggered");

        try
        {
            // Читаємо запит
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<UploadImagesRequest>(requestBody, options);

            if (data == null || string.IsNullOrEmpty(data.UserId) || data.ImageUrls == null || data.ImageUrls.Count == 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Invalid request data. UserId (chatId) and ImageUrls are required."));
                return badResponse;
            }

            var chatId = data.UserId; // UserId використовується як chatId з Telegram
            _logger.LogInformation("Processing image upload for chat: {ChatId}, {ImageCount} images",
                chatId, data.ImageUrls.Count);

            // Завантажити та зберегти кожне зображення в chatId/upload/
            var uploadedUrls = new List<string>();
            int index = 1;
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var batchId = Guid.NewGuid().ToString("N").Substring(0, 8); // Короткий унікальний ID

            foreach (var imageUrl in data.ImageUrls)
            {
                try
                {
                    _logger.LogInformation("Downloading image from: {ImageUrl}", imageUrl);
                    var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);

                    // Check if an image with the same size already exists
                    if (await _storageService.IsImageSizeExistsAsync(chatId, imageBytes.Length))
                    {
                        _logger.LogInformation("Skipping image from {ImageUrl} as an equivalent size image already exists", imageUrl);
                        continue;
                    }

                    // Зберегти в chatId/upload/image_{timestamp}_{batchId}_{N}.jpg
                    var fileName = $"image_{timestamp}_{batchId}_{index:D3}.jpg";
                    var uploadedUrl = await _storageService.UploadUserImageAsync(chatId, imageBytes, fileName);
                    uploadedUrls.Add(uploadedUrl);

                    _logger.LogInformation("Image uploaded: {FileName}", fileName);
                    index++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download image from {ImageUrl}", imageUrl);
                    // Продовжуємо з наступним зображенням
                }
            }

            if (uploadedUrls.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Failed to download any images"));
                return errorResponse;
            }

            _logger.LogInformation("Successfully uploaded {Count} images", uploadedUrls.Count);

            // Отримати список всіх завантажених файлів з розмірами
            var allImages = await _storageService.GetUploadedImagesWithSizeAsync(chatId);

            // Оновити index.md
            var indexContent = $"""
                # Chat {chatId}

                ## Uploaded Images

                - Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                - Count: {allImages.Count}

                """;

            await _storageService.UpdateIndexAsync(chatId, indexContent);

            // Повернути результат з списком файлів і розмірами
            var result = new UploadImagesResponse
            {
                ArchiveUrl = "", // Архів буде створений при CreateAndTrainModel
                ImageCount = allImages.Count,
                UploadedImages = allImages.Select(img => new UploadedImageInfo
                {
                    FileName = img.fileName,
                    SizeBytes = img.size,
                    Url = img.url
                }).ToList()
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<UploadImagesResponse>.Ok(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UploadImages function");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
            return errorResponse;
        }
    }
}
