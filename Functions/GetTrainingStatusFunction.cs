using System.Net;
using System.Text.Json;
using CalendaryKaizen.Models;
using CalendaryKaizen.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Functions;

public class GetTrainingStatusFunction
{
    private readonly IStorageService _storageService;
    private readonly IReplicateService _replicateService;
    private readonly ILogger<GetTrainingStatusFunction> _logger;

    public GetTrainingStatusFunction(
        IStorageService storageService,
        IReplicateService replicateService,
        ILogger<GetTrainingStatusFunction> logger)
    {
        _storageService = storageService;
        _replicateService = replicateService;
        _logger = logger;
    }

    [Function("GetTrainingStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("GetTrainingStatus function triggered");

        try
        {
            // Читаємо запит
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<GetTrainingStatusRequest>(requestBody, options);

            if (data == null || string.IsNullOrEmpty(data.UserId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("Invalid request data. UserId is required."));
                return badResponse;
            }

            var chatId = data.UserId;

            // Якщо TrainingId не вказано, знайти останнє тренування
            TrainingEntity? training;
            if (string.IsNullOrEmpty(data.TrainingId))
            {
                // Отримати всі тренування для користувача (потрібно додати метод в IStorageService)
                _logger.LogInformation("TrainingId not provided, looking for latest training for chat {ChatId}", chatId);
                
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.Fail("TrainingId is required. Please provide the training ID from /domodel command."));
                return badResponse;
            }
            else
            {
                training = await _storageService.GetTrainingAsync(chatId, data.TrainingId);
            }

            if (training == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(ApiResponse<object>.Fail($"Training {data.TrainingId} not found for user {chatId}"));
                return notFoundResponse;
            }

            // Якщо тренування ще не завершено, перевірити статус в Replicate
            if (training.Status != "succeeded" && training.Status != "failed" && training.Status != "canceled")
            {
                _logger.LogInformation("Training {TrainingId} is in progress, checking Replicate status", training.ReplicateId);
                
                try
                {
                    var replicateStatus = await _replicateService.GetTrainingStatusAsync(training.ReplicateId);
                    
                    // Оновити статус в базі
                    training.Status = replicateStatus.Status;
                    if (replicateStatus.Status == "succeeded")
                    {
                        training.Version = replicateStatus.Output?.Version;
                        training.CompletedAt = DateTime.UtcNow;
                    }
                    else if (replicateStatus.Status == "failed" || replicateStatus.Status == "canceled")
                    {
                        training.CompletedAt = DateTime.UtcNow;
                    }
                    
                    await _storageService.UpdateTrainingAsync(training);
                    _logger.LogInformation("Training status updated: {Status}", training.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get training status from Replicate");
                }
            }

            // Повернути результат
            var result = new GetTrainingStatusResponse
            {
                TrainingId = training.RowKey,
                ModelId = training.ModelId,
                Status = training.Status,
                CreatedAt = training.CreatedAt,
                CompletedAt = training.CompletedAt,
                Version = training.Version
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<GetTrainingStatusResponse>.Ok(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTrainingStatus function");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
            return errorResponse;
        }
    }
}
