using CalendaryKaizen.Models;

namespace CalendaryKaizen.Services;

public interface IReplicateService
{
    Task<CreateModelResponse> CreateModelAsync(string modelName, string description);
    Task<TrainModelResponse> TrainModelAsync(string destination, TrainModelRequestInput input);
    Task<GenerateImageResponse> GenerateImageAsync(string version, GenerateImageInput input);
    Task<TrainingStatusResponse> GetTrainingStatusAsync(string replicateId);
    Task CancelTrainingAsync(string replicateId);
}
