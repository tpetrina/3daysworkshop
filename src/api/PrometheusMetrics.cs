using System.Diagnostics.Metrics;
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

    static Meter meter = new Meter("Workshop.Api", "1.0");
    static Counter<long> counter = meter.CreateCounter<long>(
        name: "api.forecast_handled.count",
        description: "Number of forecasts handled");

    public static void ForecastHandled()
    {
        ForecastHandling.Inc();
        counter.Add(1);
    }
}
