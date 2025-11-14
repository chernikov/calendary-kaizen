namespace CalendaryKaizen.Models;

// Запит на створення та тренування моделі
public class CreateAndTrainRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ArchiveUrl { get; set; } = string.Empty;
    public string ModelDescription { get; set; } = "User model";
    public string TriggerWord { get; set; } = "TOK";
    public int Steps { get; set; } = 1000;
}

// Запит на генерацію зображення
public class GenerateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string TrainingId { get; set; } = string.Empty; // RowKey від TrainingEntity
    public string Prompt { get; set; } = string.Empty;
    public int? Seed { get; set; }
    public string AspectRatio { get; set; } = "1:1";
    public int NumInferenceSteps { get; set; } = 28;
}

// Запит на завантаження зображень
public class UploadImagesRequest
{
    public string UserId { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new(); // URLs зображень від Telegram
}

// Відповіді
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

public class CreateAndTrainResponse
{
    public string TrainingId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class GenerateResponse
{
    public string GenerationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class UploadImagesResponse
{
    public string ArchiveUrl { get; set; } = string.Empty;
    public int ImageCount { get; set; }
    public List<UploadedImageInfo> UploadedImages { get; set; } = new();
}

public class UploadedImageInfo
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
}

// Запит на перевірку статусу тренування
public class GetTrainingStatusRequest
{
    public string UserId { get; set; } = string.Empty;
    public string TrainingId { get; set; } = string.Empty;
}

public class GetTrainingStatusResponse
{
    public string TrainingId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Version { get; set; }
}
