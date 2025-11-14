using CalendaryKaizen.Models;

namespace CalendaryKaizen.Services;

public interface IStorageService
{
    // Training operations
    Task<TrainingEntity?> GetTrainingAsync(string userId, string trainingId);
    Task SaveTrainingAsync(TrainingEntity training);
    Task UpdateTrainingAsync(TrainingEntity training);

    // Generation operations
    Task<GenerationEntity?> GetGenerationAsync(string userId, string generationId);
    Task SaveGenerationAsync(GenerationEntity generation);
    Task UpdateGenerationAsync(GenerationEntity generation);

    // Blob operations - New structure: chatId/upload, chatId/generated, chatId/index.md
    Task<string> UploadUserImageAsync(string chatId, byte[] imageBytes, string fileName);
    Task<string> SaveGeneratedImageAsync(string chatId, byte[] imageBytes, string generationId);
    Task SavePromptAsync(string chatId, string prompt, string generationId);
    Task<string> DownloadImageAsync(string imageUrl);
    Task<string> CreateArchiveFromUploadAsync(string chatId);
    Task<List<string>> GetUploadedImagesAsync(string chatId);
    Task<List<(string fileName, long size, string url)>> GetUploadedImagesWithSizeAsync(string chatId);
    Task<bool> IsImageSizeExistsAsync(string chatId, long size);

    // Index.md operations
    Task UpdateIndexAsync(string chatId, string content);
    Task<string?> GetIndexAsync(string chatId);
    Task AppendToIndexAsync(string chatId, string content);
    Task<string?> GetLastTrainingIdAsync(string chatId);

    // Queue operations
    Task SendTelegramMessageAsync(TelegramMessage message);
}
