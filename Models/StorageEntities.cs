using Azure;
using Azure.Data.Tables;

namespace CalendaryKaizen.Models;

// Сутність для Table Storage - Training
public class TrainingEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // UserId
    public string RowKey { get; set; } = string.Empty; // TrainingId (ReplicateId)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string ReplicateId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty; // ReplicateId моделі (owner/name)
    public string Status { get; set; } = string.Empty; // starting, processing, succeeded, failed
    public string? Version { get; set; } // Версія моделі після тренування
    public string? ArchiveUrl { get; set; } // URL архіву з зображеннями
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// Сутність для Table Storage - Generation
public class GenerationEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // UserId
    public string RowKey { get; set; } = string.Empty; // GenerationId (Guid)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string ReplicateId { get; set; } = string.Empty; // Prediction ID від Replicate
    public string TrainingId { get; set; } = string.Empty; // RowKey від TrainingEntity
    public string ModelVersion { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int? Seed { get; set; }
    public int? OutputSeed { get; set; } // Реальний seed з логів
    public string Status { get; set; } = string.Empty; // processing, succeeded, failed
    public string? ImageUrl { get; set; } // URL зображення в Blob Storage
    public string? ReplicateImageUrl { get; set; } // URL від Replicate
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// Повідомлення для черги Telegram
public class TelegramMessage
{
    public string UserId { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
    public string MessageType { get; set; } = "text"; // text, image, training_complete, generation_complete
    public Dictionary<string, string>? Metadata { get; set; }
}
