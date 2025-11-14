using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CalendaryKaizen.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Services;

public class ReplicateService : IReplicateService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateService> _logger;
    private readonly string _apiKey;
    private readonly string _owner;
    private readonly string _trainerModel;
    private readonly string _trainerVersion;
    private readonly string _webhookUrl;

    private const string BaseUrl = "https://api.replicate.com/v1";

    public ReplicateService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ReplicateService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _apiKey = configuration["ReplicateApiKey"] ?? throw new InvalidOperationException("ReplicateApiKey not configured");
        _owner = configuration["ReplicateOwner"] ?? throw new InvalidOperationException("ReplicateOwner not configured");
        _trainerModel = configuration["ReplicateTrainerModel"] ?? throw new InvalidOperationException("ReplicateTrainerModel not configured");
        _trainerVersion = configuration["ReplicateTrainerVersion"] ?? throw new InvalidOperationException("ReplicateTrainerVersion not configured");
        _webhookUrl = configuration["WebhookUrl"] ?? "";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<CreateModelResponse> CreateModelAsync(string modelName, string description)
    {
        _logger.LogInformation("Creating model: {ModelName}", modelName);

        var request = new CreateModelRequest
        {
            Owner = _owner,
            Name = modelName,
            Description = description,
            Visibility = "private",
            Hardware = "cpu"
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/models", request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            
            _logger.LogError("Replicate API error {StatusCode}: {ErrorContent}", statusCode, errorContent);
            
            if (statusCode == 401)
            {
                throw new InvalidOperationException(
                    $"Replicate API authentication failed (401 Unauthorized). " +
                    $"Please verify that ReplicateApiKey is correctly configured in Azure Key Vault. " +
                    $"API Key format should start with 'r8_'. Error: {errorContent}");
            }
            
            throw new InvalidOperationException(
                $"Replicate API request failed with status {statusCode}. " +
                $"Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateModelResponse>();
        if (result == null)
            throw new InvalidOperationException("Failed to deserialize CreateModelResponse");

        _logger.LogInformation("Model created successfully: {Owner}/{Name}", result.Owner, result.Name);
        return result;
    }

    public async Task<TrainModelResponse> TrainModelAsync(string destination, TrainModelRequestInput input)
    {
        _logger.LogInformation("Starting training for model: {Destination}", destination);

        var request = new TrainModelRequest
        {
            Destination = destination,
            Input = input,
            Webhook = string.IsNullOrEmpty(_webhookUrl) ? null : _webhookUrl
        };

        var url = $"{BaseUrl}/models/{_trainerModel}/versions/{_trainerVersion}/trainings";
        var response = await _httpClient.PostAsJsonAsync(url, request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            
            _logger.LogError("Replicate API training error {StatusCode}: {ErrorContent}", statusCode, errorContent);
            
            if (statusCode == 401)
            {
                throw new InvalidOperationException(
                    $"Replicate API authentication failed (401 Unauthorized). " +
                    $"Please verify that ReplicateApiKey is correctly configured in Azure Key Vault. " +
                    $"API Key format should start with 'r8_'. Error: {errorContent}");
            }
            
            throw new InvalidOperationException(
                $"Replicate API training request failed with status {statusCode}. " +
                $"URL: {url}, Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<TrainModelResponse>();
        if (result == null)
            throw new InvalidOperationException("Failed to deserialize TrainModelResponse");

        _logger.LogInformation("Training started successfully. Training ID: {TrainingId}, Status: {Status}",
            result.Id, result.Status);
        return result;
    }

    public async Task<GenerateImageResponse> GenerateImageAsync(string version, GenerateImageInput input)
    {
        _logger.LogInformation("Generating image with version: {Version}, prompt: {Prompt}", version, input.Prompt);

        var request = new GenerateImageRequest
        {
            Version = version,
            Input = input
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/predictions")
        {
            Content = JsonContent.Create(request)
        };

        // Додаємо заголовок Prefer: wait для синхронного отримання результату
        httpRequest.Headers.Add("Prefer", "wait");

        var response = await _httpClient.SendAsync(httpRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            
            _logger.LogError("Replicate API image generation error {StatusCode}: {ErrorContent}", statusCode, errorContent);
            
            if (statusCode == 401)
            {
                throw new InvalidOperationException(
                    $"Replicate API authentication failed (401 Unauthorized). " +
                    $"Please verify that ReplicateApiKey is correctly configured in Azure Key Vault. " +
                    $"API Key format should start with 'r8_'. Error: {errorContent}");
            }
            
            throw new InvalidOperationException(
                $"Replicate API image generation request failed with status {statusCode}. " +
                $"Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<GenerateImageResponse>();
        if (result == null)
            throw new InvalidOperationException("Failed to deserialize GenerateImageResponse");

        _logger.LogInformation("Image generated successfully. Prediction ID: {PredictionId}, Status: {Status}",
            result.Id, result.Status);
        return result;
    }

    public async Task<TrainingStatusResponse> GetTrainingStatusAsync(string replicateId)
    {
        _logger.LogInformation("Getting training status for: {ReplicateId}", replicateId);

        var response = await _httpClient.GetAsync($"{BaseUrl}/predictions/{replicateId}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TrainingStatusResponse>();
        if (result == null)
            throw new InvalidOperationException("Failed to deserialize TrainingStatusResponse");

        _logger.LogInformation("Training status: {Status}", result.Status);
        return result;
    }

    public async Task CancelTrainingAsync(string replicateId)
    {
        _logger.LogInformation("Cancelling training: {ReplicateId}", replicateId);

        var response = await _httpClient.PostAsync($"{BaseUrl}/predictions/{replicateId}/cancel", null);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Training cancelled successfully");
    }
}
