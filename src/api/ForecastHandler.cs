using Rebus.Handlers;

namespace api;

internal sealed class ForecastHandler(
    ILogger<ForecastHandler> logger
) : IHandleMessages<ForecastEvent>
{
    public async Task Handle(ForecastEvent message)
    {
        logger.LogInformation("Received forecast event: {Date} {TemperatureC} {Summary}", message.Date, message.TemperatureC, message.Summary);
    }
}
