using api;
using Microsoft.EntityFrameworkCore;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
// Register Serilog
builder.Logging.AddSerilog(logger);

builder.Services.AddDbContext<WeatherForecastContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    logger.Information($"Adding DbContext: {connectionString}");
    options.UseNpgsql(connectionString);
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WeatherForecastContext>();

builder.Services
    .AddRebus(configure =>
    {
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq");
        return configure
            .Logging(l => l.Serilog())
            .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, "ForecastQueue"))
            .Routing(r => r.TypeBased().Map<ForecastEvent>("ForecastQueue"))
        ;
    })
    .AddRebusHandler<ForecastHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/forecasts", (WeatherForecastContext context) =>
{
    return context.Forecasts.ToList();
});
app.MapPost("/forecast", (WeatherForecastEntity forecast, WeatherForecastContext context) =>
{
    context.Forecasts.Add(forecast);
    context.SaveChanges();
    return forecast;
});
app.MapPost("/forecast/publish-random", async (IBus bus) =>
{
    await bus.Send(new ForecastEvent(
        DateTime.Now.AddDays(Random.Shared.Next(0, 10)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)]
    ));
    return "Published";
});

app.MapHealthChecks("/health");

if (args.Length > 0)
{
    switch (args[0])
    {
        case "--migrate":
            {
                app.Logger.LogInformation("Migrating database");
                using var scope = app.Services.CreateScope();
                using var context = scope.ServiceProvider.GetService<WeatherForecastContext>();
                context?.Database.Migrate();
                return;
            }
    }
}
else
{
    app.Run();
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
