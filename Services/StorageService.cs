using System.IO.Compression;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using CalendaryKaizen.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CalendaryKaizen.Services;

public class StorageService : IStorageService
{
    private readonly TableClient _trainingsTable;
    private readonly TableClient _generationsTable;
    private readonly BlobContainerClient _dataBlobContainer;
    private readonly QueueClient _telegramQueue;
    private readonly HttpClient _httpClient;
    private readonly ILogger<StorageService> _logger;

    public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();

        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage not configured");

        // Initialize Table clients
        var tableServiceClient = new TableServiceClient(connectionString);
        _trainingsTable = tableServiceClient.GetTableClient("Trainings");
        _generationsTable = tableServiceClient.GetTableClient("Generations");

        _trainingsTable.CreateIfNotExists();
        _generationsTable.CreateIfNotExists();

        // Initialize Blob client - one container with chatId-based structure
        var blobServiceClient = new BlobServiceClient(connectionString);
        _dataBlobContainer = blobServiceClient.GetBlobContainerClient("data");
        _dataBlobContainer.CreateIfNotExists();

        // Initialize Queue client
        _telegramQueue = new QueueClient(connectionString, "telegram-notifications");
        _telegramQueue.CreateIfNotExists();
    }

    // Training operations
    public async Task<TrainingEntity?> GetTrainingAsync(string userId, string trainingId)
    {
        try
        {
            var response = await _trainingsTable.GetEntityAsync<TrainingEntity>(userId, trainingId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveTrainingAsync(TrainingEntity training)
    {
        await _trainingsTable.AddEntityAsync(training);
        _logger.LogInformation("Training saved: {TrainingId} for user {UserId}", training.RowKey, training.PartitionKey);
    }

    public async Task UpdateTrainingAsync(TrainingEntity training)
    {
        await _trainingsTable.UpdateEntityAsync(training, training.ETag);
        _logger.LogInformation("Training updated: {TrainingId} for user {UserId}", training.RowKey, training.PartitionKey);
    }

    // Generation operations
    public async Task<GenerationEntity?> GetGenerationAsync(string userId, string generationId)
    {
        try
        {
            var response = await _generationsTable.GetEntityAsync<GenerationEntity>(userId, generationId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveGenerationAsync(GenerationEntity generation)
    {
        await _generationsTable.AddEntityAsync(generation);
        _logger.LogInformation("Generation saved: {GenerationId} for user {UserId}", generation.RowKey, generation.PartitionKey);
    }

    public async Task UpdateGenerationAsync(GenerationEntity generation)
    {
        await _generationsTable.UpdateEntityAsync(generation, generation.ETag);
        _logger.LogInformation("Generation updated: {GenerationId} for user {UserId}", generation.RowKey, generation.PartitionKey);
    }

    // Blob operations - New structure
    public async Task<string> UploadUserImageAsync(string chatId, byte[] imageBytes, string fileName)
    {
        var blobPath = $"{chatId}/upload/{fileName}";
        var blobClient = _dataBlobContainer.GetBlobClient(blobPath);

        await using var stream = new MemoryStream(imageBytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        _logger.LogInformation("User image uploaded: {BlobPath}", blobPath);
        return blobClient.Uri.ToString();
    }

    public async Task<string> SaveGeneratedImageAsync(string chatId, byte[] imageBytes, string generationId)
    {
        var fileName = $"{generationId}.jpg";
        var blobPath = $"{chatId}/generated/{fileName}";
        var blobClient = _dataBlobContainer.GetBlobClient(blobPath);

        await using var stream = new MemoryStream(imageBytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        _logger.LogInformation("Generated image saved: {BlobPath}", blobPath);
        return blobClient.Uri.ToString();
    }

    public async Task SavePromptAsync(string chatId, string prompt, string generationId)
    {
        var fileName = $"{generationId}_prompt.txt";
        var blobPath = $"{chatId}/generated/{fileName}";
        var blobClient = _dataBlobContainer.GetBlobClient(blobPath);

        var promptBytes = System.Text.Encoding.UTF8.GetBytes(prompt);
        await using var stream = new MemoryStream(promptBytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        _logger.LogInformation("Prompt saved: {BlobPath}", blobPath);
    }

    public async Task<string> DownloadImageAsync(string imageUrl)
    {
        _logger.LogInformation("Downloading image from: {ImageUrl}", imageUrl);
        var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
        return Convert.ToBase64String(imageBytes);
    }

    public async Task<string> CreateArchiveFromUploadAsync(string chatId)
    {
        _logger.LogInformation("Creating archive from upload folder for chat {ChatId}", chatId);

        var uploadPrefix = $"{chatId}/upload/";
        var blobs = _dataBlobContainer.GetBlobsAsync(prefix: uploadPrefix);

        await using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
        {
            int index = 0;
            await foreach (var blobItem in blobs)
            {
                var blobClient = _dataBlobContainer.GetBlobClient(blobItem.Name);
                var downloadInfo = await blobClient.DownloadAsync();

                var fileName = Path.GetFileName(blobItem.Name);
                var entry = archive.CreateEntry(fileName);

                await using var entryStream = entry.Open();
                await downloadInfo.Value.Content.CopyToAsync(entryStream);

                index++;
            }

            if (index == 0)
            {
                throw new InvalidOperationException($"No images found in {uploadPrefix}");
            }

            _logger.LogInformation("Archive created with {ImageCount} images", index);
        }

        // Зберегти архів в корені chatId
        archiveStream.Position = 0;
        var archiveName = $"{chatId}/archive_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        var archiveBlobClient = _dataBlobContainer.GetBlobClient(archiveName);
        await archiveBlobClient.UploadAsync(archiveStream, overwrite: true);

        _logger.LogInformation("Archive uploaded: {ArchiveName}", archiveName);
        return archiveBlobClient.Uri.ToString();
    }

    public async Task<List<string>> GetUploadedImagesAsync(string chatId)
    {
        var uploadPrefix = $"{chatId}/upload/";
        var imageUrls = new List<string>();

        var blobs = _dataBlobContainer.GetBlobsAsync(prefix: uploadPrefix);
        await foreach (var blobItem in blobs)
        {
            var blobClient = _dataBlobContainer.GetBlobClient(blobItem.Name);
            imageUrls.Add(blobClient.Uri.ToString());
        }

        _logger.LogInformation("Found {Count} uploaded images for chat {ChatId}", imageUrls.Count, chatId);
        return imageUrls;
    }

    public async Task<List<(string fileName, long size, string url)>> GetUploadedImagesWithSizeAsync(string chatId)
    {
        var uploadPrefix = $"{chatId}/upload/";
        var images = new List<(string fileName, long size, string url)>();

        var blobs = _dataBlobContainer.GetBlobsAsync(prefix: uploadPrefix);
        await foreach (var blobItem in blobs)
        {
            var blobClient = _dataBlobContainer.GetBlobClient(blobItem.Name);
            var fileName = Path.GetFileName(blobItem.Name);
            var size = blobItem.Properties.ContentLength ?? 0;
            images.Add((fileName, size, blobClient.Uri.ToString()));
        }

        _logger.LogInformation("Found {Count} uploaded images with sizes for chat {ChatId}", images.Count, chatId);
        return images;
    }

    public async Task<bool> IsImageSizeExistsAsync(string chatId, long size)
    {
        var uploadPrefix = $"{chatId}/upload/";
        var blobs = _dataBlobContainer.GetBlobsAsync(prefix: uploadPrefix);
        await foreach (var blobItem in blobs)
        {
            if (blobItem.Properties.ContentLength == size)
            {
                return true;
            }
        }
        return false;
    }

    // Index.md operations
    public async Task UpdateIndexAsync(string chatId, string content)
    {
        var blobPath = $"{chatId}/index.md";
        var blobClient = _dataBlobContainer.GetBlobClient(blobPath);

        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        await using var stream = new MemoryStream(contentBytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        _logger.LogInformation("Index updated for chat {ChatId}", chatId);
    }

    public async Task<string?> GetIndexAsync(string chatId)
    {
        try
        {
            var blobPath = $"{chatId}/index.md";
            var blobClient = _dataBlobContainer.GetBlobClient(blobPath);

            var downloadInfo = await blobClient.DownloadAsync();
            using var reader = new StreamReader(downloadInfo.Value.Content);
            var content = await reader.ReadToEndAsync();

            _logger.LogInformation("Index retrieved for chat {ChatId}", chatId);
            return content;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Index not found for chat {ChatId}", chatId);
            return null;
        }
    }

    public async Task AppendToIndexAsync(string chatId, string content)
    {
        var existingContent = await GetIndexAsync(chatId) ?? "";
        var newContent = existingContent + "\n" + content;
        await UpdateIndexAsync(chatId, newContent);

        _logger.LogInformation("Content appended to index for chat {ChatId}", chatId);
    }

    // Queue operations
    public async Task SendTelegramMessageAsync(TelegramMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        await _telegramQueue.SendMessageAsync(json);

        _logger.LogInformation("Telegram message queued for user {UserId}", message.UserId);
    }
}
