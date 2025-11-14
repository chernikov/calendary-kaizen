using System.Net;
using System.Text.Json;
using CalendaryKaizen.Models;
using CalendaryKaizen.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Functions;

public class CreateAndTrainModelFunction
{
    private readonly IReplicateService _replicateService;
    private readonly IStorageService _storageService;
    private readonly ILogger<CreateAndTrainModelFunction> _logger;

    public CreateAndTrainModelFunction(
        IReplicateService replicateService,
        IStorageService storageService,
        ILogger<CreateAndTrainModelFunction> logger)
    {
        _replicateService = replicateService;
        _storageService = storageService;
        _logger = logger;
    }

    [Function("CreateAndTrainModel")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("CreateAndTrainModel function triggered");

        try
        {
            // Читаємо запит
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request body: {RequestBody}", requestBody);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var data = JsonSerializer.Deserialize<CreateAndTrainRequest>(requestBody, options);

            if (data == null || string.IsNullOrEmpty(data.UserId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Invalid request data. UserId (chatId) is required."));
                return badResponse;
            }

            var chatId = data.UserId; // UserId використовується як chatId з Telegram
            _logger.LogInformation("Processing request for chat: {ChatId}", chatId);

            // Крок 0a: Перевірити чи є активне тренування
            var lastTrainingId = await _storageService.GetLastTrainingIdAsync(chatId);
            if (!string.IsNullOrEmpty(lastTrainingId))
            {
                var lastTraining = await _storageService.GetTrainingAsync(chatId, lastTrainingId);
                if (lastTraining != null && 
                    lastTraining.Status != "succeeded" && 
                    lastTraining.Status != "failed" && 
                    lastTraining.Status != "canceled")
                {
                    _logger.LogWarning("Training already in progress for chat {ChatId}: {TrainingId}, status: {Status}", 
                        chatId, lastTrainingId, lastTraining.Status);
                    
                    var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflictResponse.WriteAsJsonAsync(ApiResponse<object>.Fail(
                        $"Training is already in progress (ID: {lastTrainingId}, Status: {lastTraining.Status}). " +
                        $"Please wait for it to complete before starting a new training."));
                    return conflictResponse;
                }
            }

            // Крок 0b: Створити архів з папки chatId/upload/
            string archiveUrl;
            try
            {
                archiveUrl = await _storageService.CreateArchiveFromUploadAsync(chatId);
                _logger.LogInformation("Archive created from upload folder: {ArchiveUrl}", archiveUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create archive from upload folder for chat {ChatId}", chatId);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.Fail($"Failed to create archive: {ex.Message}"));
                return errorResponse;
            }

            // Крок 1: Створення моделі в Replicate
            var randomDigits = Random.Shared.Next(100, 999);
            var modelName = $"telegram_flux_{chatId}_{randomDigits}";

            var createResponse = await _replicateService.CreateModelAsync(modelName, data.ModelDescription);
            var replicateId = $"{createResponse.Owner}/{createResponse.Name}";

            _logger.LogInformation("Model created: {ReplicateId}", replicateId);

            // Крок 2: Запуск тренування
            var trainingInput = new TrainModelRequestInput
            {
                InputImages = archiveUrl,
                TriggerWord = data.TriggerWord,
                Steps = data.Steps,
                AutocaptionPrefix = $"a photo of {data.TriggerWord}",
                LoraRank = 16,
                Optimizer = "adamw8bit",
                BatchSize = 1,
                Resolution = "512,768,1024",
                Autocaption = true,
                LearningRate = 0.0004,
                WandbProject = "flux_train_replicate",
                WandbSaveInterval = 100,
                WandbSampleInterval = 100,
                CaptionDropoutRate = 0.05,
                CacheLatentsToDisk = false
            };

            var trainingResponse = await _replicateService.TrainModelAsync(replicateId, trainingInput);

            _logger.LogInformation("Training started: {TrainingId}", trainingResponse.Id);

            // Крок 3: Зберегти в Table Storage
            var training = new TrainingEntity
            {
                PartitionKey = chatId,
                RowKey = trainingResponse.Id,
                ReplicateId = trainingResponse.Id,
                ModelId = replicateId,
                Status = trainingResponse.Status,
                ArchiveUrl = archiveUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _storageService.SaveTrainingAsync(training);

            // Крок 4: Оновити index.md
            var trainingInfo = $"""

                ## Training

                - Training ID: {trainingResponse.Id}
                - Model ID: {replicateId}
                - Status: {trainingResponse.Status}
                - Archive: {archiveUrl}
                - Trigger Word: {data.TriggerWord}
                - Steps: {data.Steps}
                - Started: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

                """;

            await _storageService.AppendToIndexAsync(chatId, trainingInfo);

            // Крок 5: Повернути результат
            var result = new CreateAndTrainResponse
            {
                TrainingId = trainingResponse.Id,
                ModelId = replicateId,
                Status = trainingResponse.Status
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<CreateAndTrainResponse>.Ok(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateAndTrainModel function");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
            return errorResponse;
        }
    }
}
