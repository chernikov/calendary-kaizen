using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CalendaryKaizen.Models;
using CalendaryKaizen.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Functions;

public class ReplicateWebHookFunction
{
    private readonly IStorageService _storageService;
    private readonly ILogger<ReplicateWebHookFunction> _logger;

    public ReplicateWebHookFunction(
        IStorageService storageService,
        ILogger<ReplicateWebHookFunction> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    [Function("ReplicateWebHook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        _logger.LogInformation("ReplicateWebHook function triggered");

        try
        {
            // Читаємо webhook payload
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var webhook = JsonSerializer.Deserialize<WebhookRequest>(requestBody);

            if (webhook == null || string.IsNullOrEmpty(webhook.Id))
            {
                _logger.LogWarning("Invalid webhook payload");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                return badResponse;
            }

            _logger.LogInformation("Processing webhook for ID: {Id}, Status: {Status}", webhook.Id, webhook.Status);

            // Спробуємо знайти training
            var training = await FindTrainingByReplicateIdAsync(webhook.Id);

            if (training != null)
            {
                await ProcessTrainingWebHookAsync(training, webhook);
            }
            else
            {
                _logger.LogWarning("Training not found for webhook ID: {Id}", webhook.Id);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            return errorResponse;
        }
    }

    private async Task<TrainingEntity?> FindTrainingByReplicateIdAsync(string replicateId)
    {
        // Оскільки Table Storage не підтримує запити по RowKey напряму без PartitionKey,
        // в реальному сценарії потрібно зберігати mapping у окремій таблиці
        // Або додати secondary index
        // Для простоти, це спрощена реалізація
        _logger.LogWarning("Finding training by ReplicateId requires full table scan - consider adding index");
        return null;

        // TODO: Implement proper lookup using secondary index or mapping table
    }

    private async Task ProcessTrainingWebHookAsync(TrainingEntity training, WebhookRequest webhook)
    {
        _logger.LogInformation("Processing training webhook for user {UserId}, training {TrainingId}",
            training.PartitionKey, training.RowKey);

        // Оновити статус
        training.Status = webhook.Status;

        // Якщо успішно завершено, витягти версію
        if (webhook.Status == "succeeded" && webhook.Output?.Version != null)
        {
            var versionParts = webhook.Output.Version.Split(':');
            if (versionParts.Length == 2)
            {
                training.Version = versionParts[1];
                training.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Training completed successfully. Version: {Version}", training.Version);

                // Відправити повідомлення в Telegram
                await _storageService.SendTelegramMessageAsync(new TelegramMessage
                {
                    UserId = training.PartitionKey,
                    Text = $"✅ Тренування завершено!\n\n" +
                           $"Модель: {training.ModelId}\n" +
                           $"Версія: {training.Version}\n\n" +
                           $"Тепер ви можете генерувати зображення!",
                    MessageType = "training_complete",
                    Metadata = new Dictionary<string, string>
                    {
                        ["TrainingId"] = training.RowKey,
                        ["ModelVersion"] = training.Version ?? ""
                    }
                });
            }
        }
        else if (webhook.Status == "failed")
        {
            training.CompletedAt = DateTime.UtcNow;

            _logger.LogError("Training failed for user {UserId}", training.PartitionKey);

            // Відправити повідомлення про помилку
            await _storageService.SendTelegramMessageAsync(new TelegramMessage
            {
                UserId = training.PartitionKey,
                Text = "❌ Тренування не вдалося. Спробуйте ще раз або зверніться до підтримки.",
                MessageType = "training_complete"
            });
        }

        // Оновити в базі
        await _storageService.UpdateTrainingAsync(training);
    }
}
