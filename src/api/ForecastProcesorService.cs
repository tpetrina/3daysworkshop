namespace api;

interface IForecastProcessorService
{
    Task ProcessForecastsAsync(string jobId);
}

sealed class ForecastProcessorService(
    ILogger<ForecastProcessorService> logger
) : IForecastProcessorService
{
    public async Task ProcessForecastsAsync(string jobId)
    {
        logger.LogInformation("Job {JobId}: Processing forecasts...", jobId);
    }
}