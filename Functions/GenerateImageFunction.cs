using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CalendaryKaizen.Models;
using CalendaryKaizen.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Functions;

public class GenerateImageFunction
{
    private readonly IReplicateService _replicateService;
    private readonly IStorageService _storageService;
    private readonly ILogger<GenerateImageFunction> _logger;

    public GenerateImageFunction(
        IReplicateService replicateService,
        IStorageService storageService,
        ILogger<GenerateImageFunction> logger)
    {
        _replicateService = replicateService;
        _storageService = storageService;
        _logger = logger;
    }

    [Function("GenerateImage")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("GenerateImage function triggered");

        try
        {
            // Читаємо запит
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<GenerateRequest>(requestBody);

            if (data == null || string.IsNullOrEmpty(data.UserId) || string.IsNullOrEmpty(data.TrainingId) || string.IsNullOrEmpty(data.Prompt))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Invalid request data"));
                return badResponse;
            }

            _logger.LogInformation("Processing generation request for user: {UserId}, training: {TrainingId}",
                data.UserId, data.TrainingId);

            // Отримати training з бази даних
            var training = await _storageService.GetTrainingAsync(data.UserId, data.TrainingId);

            if (training == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Training not found"));
                return notFoundResponse;
            }

            if (training.Status != "succeeded" || string.IsNullOrEmpty(training.Version))
            {
                var badStateResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badStateResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Training is not completed or failed"));
                return badStateResponse;
            }

            // Створити запис для генерації
            var generationId = Guid.NewGuid().ToString();
            var generation = new GenerationEntity
            {
                PartitionKey = data.UserId,
                RowKey = generationId,
                TrainingId = data.TrainingId,
                ModelVersion = training.Version,
                Prompt = data.Prompt,
                Seed = data.Seed,
                Status = "processing",
                CreatedAt = DateTime.UtcNow
            };

            await _storageService.SaveGenerationAsync(generation);

            // Генерація зображення
            var imageInput = new GenerateImageInput
            {
                Model = "dev",
                Prompt = data.Prompt,
                Seed = data.Seed,
                LoraScale = 1.0,
                NumOutputs = 1,
                AspectRatio = data.AspectRatio,
                OutputFormat = "jpg",
                GuidanceScale = 3.5,
                OutputQuality = 90,
                PromptStrength = 0.8,
                ExtraLoraScale = 1.0,
                NumInferenceSteps = data.NumInferenceSteps
            };

            var generateResponse = await _replicateService.GenerateImageAsync(training.Version, imageInput);

            _logger.LogInformation("Image generation completed. Prediction ID: {PredictionId}, Status: {Status}",
                generateResponse.Id, generateResponse.Status);

            // Оновити generation entity
            generation.ReplicateId = generateResponse.Id;
            generation.Status = generateResponse.Status;

            if (generateResponse.Status == "succeeded" && generateResponse.Output.Count > 0)
            {
                var replicateImageUrl = generateResponse.Output[0];
                generation.ReplicateImageUrl = replicateImageUrl;

                // Витягти seed з логів
                var seed = ExtractSeedFromLogs(generateResponse.Logs);
                if (seed.HasValue)
                {
                    generation.OutputSeed = seed.Value;
                }

                // Завантажити зображення з Replicate
                using var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(replicateImageUrl);

                // Зберегти зображення в chatId/generated/
                var chatId = data.UserId;
                var imageUrl = await _storageService.SaveGeneratedImageAsync(chatId, imageBytes, generationId);
                generation.ImageUrl = imageUrl;
                generation.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Image saved to blob storage: {ImageUrl}", imageUrl);

                // Зберегти промпт в текстовий файл
                await _storageService.SavePromptAsync(chatId, data.Prompt, generationId);

                // Оновити index.md
                var generationInfo = $"""

                    ### Generation {generationId}

                    - Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                    - Prompt: {data.Prompt}
                    - Seed: {generation.OutputSeed}
                    - Image: {imageUrl}

                    """;

                await _storageService.AppendToIndexAsync(chatId, generationInfo);

                // Відправити повідомлення в Telegram
                await _storageService.SendTelegramMessageAsync(new TelegramMessage
                {
                    UserId = data.UserId,
                    ImageUrl = imageUrl,
                    Text = $"🎨 Зображення згенеровано!\n\nPrompt: {data.Prompt}\nSeed: {generation.OutputSeed}",
                    MessageType = "generation_complete",
                    Metadata = new Dictionary<string, string>
                    {
                        ["GenerationId"] = generationId,
                        ["Seed"] = generation.OutputSeed?.ToString() ?? ""
                    }
                });
            }
            else
            {
                generation.Status = "failed";
                generation.CompletedAt = DateTime.UtcNow;
            }

            await _storageService.UpdateGenerationAsync(generation);

            // Повернути результат
            var result = new GenerateResponse
            {
                GenerationId = generationId,
                Status = generation.Status
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<GenerateResponse>.Ok(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateImage function");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
            return errorResponse;
        }
    }

    private int? ExtractSeedFromLogs(string logs)
    {
        if (string.IsNullOrEmpty(logs))
            return null;

        var match = Regex.Match(logs, @"Using seed:\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var seed))
        {
            return seed;
        }

        return null;
    }
}
