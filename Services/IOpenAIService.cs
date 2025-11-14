namespace CalendaryKaizen.Services;

public interface IOpenAIService
{
    Task<string> EnhancePromptForFluxAsync(string userPrompt, string triggerWord = "TOK");
}
