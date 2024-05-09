namespace api;

public sealed record ForecastEvent(DateTime Date, int TemperatureC, string? Summary);
