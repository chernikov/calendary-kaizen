using System.Text.Json.Serialization;

namespace CalendaryKaizen.Models;

// Налаштування Replicate
public class ReplicateSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string TrainerModel { get; set; } = string.Empty;
    public string TrainerVersion { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
}

// Створення моделі
public class CreateModelRequest
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = "private";

    [JsonPropertyName("hardware")]
    public string Hardware { get; set; } = "cpu";
}

public class CreateModelResponse
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

// Тренування моделі
public class TrainModelRequest
{
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public TrainModelRequestInput Input { get; set; } = new();

    [JsonPropertyName("webhook")]
    public string? Webhook { get; set; }
}

public class TrainModelRequestInput
{
    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 1000;

    [JsonPropertyName("lora_rank")]
    public int LoraRank { get; set; } = 16;

    [JsonPropertyName("optimizer")]
    public string Optimizer { get; set; } = "adamw8bit";

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = "512,768,1024";

    [JsonPropertyName("autocaption")]
    public bool Autocaption { get; set; } = true;

    [JsonPropertyName("autocaption_prefix")]
    public string AutocaptionPrefix { get; set; } = "a photo of TOK";

    [JsonPropertyName("input_images")]
    public string InputImages { get; set; } = string.Empty;

    [JsonPropertyName("trigger_word")]
    public string TriggerWord { get; set; } = "TOK";

    [JsonPropertyName("learning_rate")]
    public double LearningRate { get; set; } = 0.0004;

    [JsonPropertyName("wandb_project")]
    public string WandbProject { get; set; } = "flux_train_replicate";

    [JsonPropertyName("wandb_save_interval")]
    public int WandbSaveInterval { get; set; } = 100;

    [JsonPropertyName("wandb_sample_interval")]
    public int WandbSampleInterval { get; set; } = 100;

    [JsonPropertyName("caption_dropout_rate")]
    public double CaptionDropoutRate { get; set; } = 0.05;

    [JsonPropertyName("cache_latents_to_disk")]
    public bool CacheLatentsToDisk { get; set; } = false;
}

public class TrainModelResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("urls")]
    public TrainingUrls Urls { get; set; } = new();
}

public class TrainingUrls
{
    [JsonPropertyName("get")]
    public string Get { get; set; } = string.Empty;

    [JsonPropertyName("cancel")]
    public string Cancel { get; set; } = string.Empty;
}

// Генерація зображень
public class GenerateImageRequest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public GenerateImageInput Input { get; set; } = new();
}

public class GenerateImageInput
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "dev";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("lora_scale")]
    public double LoraScale { get; set; } = 1.0;

    [JsonPropertyName("num_outputs")]
    public int NumOutputs { get; set; } = 1;

    [JsonPropertyName("aspect_ratio")]
    public string AspectRatio { get; set; } = "1:1";

    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = "jpg";

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 3.5;

    [JsonPropertyName("output_quality")]
    public int OutputQuality { get; set; } = 90;

    [JsonPropertyName("prompt_strength")]
    public double PromptStrength { get; set; } = 0.8;

    [JsonPropertyName("extra_lora_scale")]
    public double ExtraLoraScale { get; set; } = 1.0;

    [JsonPropertyName("num_inference_steps")]
    public int NumInferenceSteps { get; set; } = 28;
}

public class GenerateImageResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public List<string> Output { get; set; } = new();

    [JsonPropertyName("logs")]
    public string Logs { get; set; } = string.Empty;

    [JsonPropertyName("metrics")]
    public Dictionary<string, object>? Metrics { get; set; }
}

// WebHook
public class WebhookRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public WebhookOutput? Output { get; set; }

    [JsonPropertyName("logs")]
    public string Logs { get; set; } = string.Empty;

    [JsonPropertyName("metrics")]
    public Dictionary<string, object>? Metrics { get; set; }
}

public class WebhookOutput
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("weights")]
    public string Weights { get; set; } = string.Empty;
}

// Статус тренування
public class TrainingStatusResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public WebhookOutput? Output { get; set; }

    [JsonPropertyName("logs")]
    public string Logs { get; set; } = string.Empty;

    [JsonPropertyName("metrics")]
    public Dictionary<string, object>? Metrics { get; set; }
}
