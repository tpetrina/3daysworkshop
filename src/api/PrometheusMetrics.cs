using Prometheus;

static class PrometheusMetrics
{
    public static readonly Counter ForecastProcessing = Metrics
        .CreateCounter(
            name: "api_forecast_processing",
            help: "Number of forecasts processed");
    public static readonly Counter ForecastHandling = Metrics
        .CreateCounter(
            name: "api_forecast_handling",
            help: "Number of forecasts handled");
}
