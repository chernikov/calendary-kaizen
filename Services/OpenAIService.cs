using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CalendaryKaizen.Services;

public class OpenAIService : IOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIService> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    private const string BaseUrl = "https://api.openai.com/v1";

    public OpenAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _apiKey = configuration["OpenAIApiKey"] ?? throw new InvalidOperationException("OpenAIApiKey not configured");
        _model = configuration["OpenAIModel"] ?? "gpt-5-mini";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> EnhancePromptForFluxAsync(string userPrompt, string triggerWord = "TOK")
    {
        _logger.LogInformation("Enhancing prompt for FLUX with trigger word: {TriggerWord}", triggerWord);

        var systemMessage = $"""
            You are an expert at writing prompts for FLUX image generation models.

            Your task is to enhance user prompts to work optimally with FLUX, while incorporating the trigger word "{triggerWord}" naturally into the prompt.

            Guidelines:
            1. The trigger word "{triggerWord}" MUST be included in the enhanced prompt
            2. Make the prompt detailed and descriptive
            3. Include relevant art style, lighting, composition details
            4. Keep it concise but effective (max 200 words)
            5. Focus on visual elements and artistic quality
            6. Return ONLY the enhanced prompt text, nothing else

            Example:
            User: "a portrait"
            Enhanced: "A detailed portrait of {triggerWord}, professional photography, soft studio lighting, shallow depth of field, 85mm lens, photorealistic, high quality"
            """;

        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7,
            max_tokens = 300
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}/chat/completions", httpContent);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;

            _logger.LogError("OpenAI API error {StatusCode}: {ErrorContent}", statusCode, errorContent);

            if (statusCode == 401)
            {
                throw new InvalidOperationException(
                    $"OpenAI API authentication failed (401 Unauthorized). " +
                    $"Please verify that OpenAIApiKey is correctly configured. " +
                    $"Error: {errorContent}");
            }

            throw new InvalidOperationException(
                $"OpenAI API request failed with status {statusCode}. " +
                $"Response: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

        if (result?.Choices == null || result.Choices.Length == 0)
        {
            throw new InvalidOperationException("OpenAI API returned empty response");
        }

        var enhancedPrompt = result.Choices[0].Message.Content.Trim();

        _logger.LogInformation("Prompt enhanced successfully. Original length: {OriginalLength}, Enhanced length: {EnhancedLength}",
            userPrompt.Length, enhancedPrompt.Length);

        return enhancedPrompt;
    }

    private class OpenAIResponse
    {
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
    }

    private class Choice
    {
        public Message Message { get; set; } = new();
    }

    private class Message
    {
        public string Content { get; set; } = string.Empty;
    }
}
