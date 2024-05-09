using messages;
using Rebus.Handlers;

namespace api;

internal sealed class ForecastHandler(
    WeatherForecastContext context,
    ILogger<ForecastHandler> logger
) : IHandleMessages<ForecastEvent>
, IHandleMessages<ForecastEvent2>
{
    public async Task Handle(ForecastEvent message)
    {
        logger.LogInformation("Received forecast event: {Date} {TemperatureC} {Summary}", message.Date, message.TemperatureC, message.Summary);

        var date = message.Date.Date.ToUniversalTime();
        var existing = context.Forecasts
            .FirstOrDefault(f => f.Date.Date == date.Date);
        if (existing is not null)
        {
            logger.LogInformation("Updating forecast for {Date}", date);
            existing.TemperatureC = message.TemperatureC;
            existing.Summary = message.Summary;
        }
        else
        {
            logger.LogInformation("Adding new forecast for {Date}", date);
            context.Forecasts.Add(new WeatherForecastEntity
            {
                Date = date,
                TemperatureC = message.TemperatureC,
                Summary = message.Summary
            });
        }
        context.SaveChanges();

        PrometheusMetrics.ForecastHandled();
    }

    public async Task Handle(ForecastEvent2 message)
    {
        logger.LogInformation("Received forecast event: {Date} {TemperatureC} {Summary} {Location}", message.Date, message.TemperatureC, message.Summary, message.Location);

        var date = message.Date.Date.ToUniversalTime();
        var existing = context.Forecasts
            .FirstOrDefault(f => f.Date.Date == date.Date &&
            f.Location == message.Location);
        if (existing is not null)
        {
            logger.LogInformation("Updating forecast for {Date}", date);
            existing.TemperatureC = message.TemperatureC;
            existing.Summary = message.Summary;
        }
        else
        {
            logger.LogInformation("Adding new forecast for {Date}", date);
            context.Forecasts.Add(new WeatherForecastEntity
            {
                Date = date,
                TemperatureC = message.TemperatureC,
                Summary = message.Summary,
                Location = message.Location
            });
        }
        await context.SaveChangesAsync();

        PrometheusMetrics.ForecastHandled();
    }
}
