using api;
using messages;
using Microsoft.EntityFrameworkCore;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Serilog;
using RabbitMQ.Client;
using Hangfire;
using Hangfire.PostgreSql;

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
    .AddDbContextCheck<WeatherForecastContext>()
    .AddRabbitMQ();

builder.Services.AddSingleton<IConnection>(c =>
{
    return new ConnectionFactory()
    {
        Uri = new Uri(builder.Configuration.GetConnectionString("RabbitMq"))
    }.CreateConnection();
});
builder.Services
    .AddRebus(configure =>
    {
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq");
        return configure
            .Logging(l => l.Serilog())
            .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, "ForecastQueue"))
            .Routing(r => r.TypeBased()
                .Map<ForecastEvent>("ForecastQueue")
                .Map<ForecastEvent2>("ForecastQueue")
            )
        ;
    })
    .AddRebusHandler<ForecastHandler>();

var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection");
var useHangfire = !string.IsNullOrEmpty(hangfireConnectionString);
if (useHangfire)
{
    logger.Information("Adding Hangfire: " + hangfireConnectionString + ".");

    builder.Services
        .AddHangfire(configure =>
        {
            configure
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(b => b.UseNpgsqlConnection(hangfireConnectionString));
        })
        .AddHangfireServer();
}

builder.Services.AddScoped<IForecastProcessorService, ForecastProcessorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

if (useHangfire)
{
    app.UseHangfireDashboard();
    // app.UseHangfireDashboard("/hangfire", new DashboardOptions
    // {
    //     DashboardTitle = "Hangfire Dashboard",
    //     PrefixPath = "/api",
    //     Authorization = new[]
    //     {
    //             new AnonymousAuthorizationFilter()
    //         }
    // });
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
    await bus.Send(new ForecastEvent2(
        DateTime.Now.AddDays(Random.Shared.Next(0, 10)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)],
        new Bogus.DataSets.Address().City()
    ));
    return "Published";
});
app.MapPost("/forecast/process", () =>
{
    app.Logger.LogInformation("Processing");
    var jobId = Guid.NewGuid().ToString("N");
    BackgroundJob.Schedule<IForecastProcessorService>(svc => svc.ProcessForecastsAsync2(jobId), DateTime.Now.AddMinutes(1));
    return $"Enqueued {jobId}";
});
app.MapPost("/forecast/process2", () =>
{
    app.Logger.LogInformation("Processing");
    var jobId = Guid.NewGuid().ToString("N");
    BackgroundJob.Schedule<IForecastProcessorService>(svc => svc.ProcessForecastsAsync2(jobId), DateTime.Now.AddSeconds(1));
    return $"Enqueued {jobId}";
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

    RecurringJob.AddOrUpdate("hello-world", () => Console.WriteLine("Hello World!"), Cron.Minutely);
    app.Run();
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
