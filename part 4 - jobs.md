# Part 4 - jobs

For jobs we will use Hangfire. There are other libraries and even a built-in
support in .NET.

# 4.1 Install Hangfire

Install packages to `api` by running the following in `src/api`

```sh
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql
```

Add another connection string to `appsettings.Development.json`:

```json
"HangfireConnection": "Host=localhost; Port=5432; Database=postgres; Username=postgres; Password=postgres"
```

Configure services:

```csharp
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
```

And map dashboard:

```csharp
if (useHangfire)
{
    app.UseHangfireDashboard();
}
```

Add a simple recurring job before `app.Run();`:

```csharp
RecurringJob.AddOrUpdate("hello-world", () => Console.WriteLine("Hello World!"), Cron.Minutely);
```

# 4.2 Adding a new job

Create a file `ForecastProcesorService.cs`:

```csharp
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
```

Register the service:

```csharp
builder.Services.AddScoped<IForecastProcessorService, ForecastProcessorService>();
```

We will simulate triggering job on demand with a custom endpoint:

```csharp
app.MapPost("/forecast/process", () =>
{
    app.Logger.LogInformation("Processing");
    var jobId = Guid.NewGuid().ToString("N");
    BackgroundJob.Schedule<IForecastProcessorService>(svc => svc.ProcessForecastsAsync(jobId), DateTime.Now.AddSeconds(1));
    return $"Enqueued {jobId}";
});
```

# 4.3 Versioning

Add a new processing method named `ProcessForecastsAsync2` to the `ForecastProcessorService`.
Add a new handler:

```csharp
app.MapPost("/forecast/process2", () =>
{
    app.Logger.LogInformation("Processing");
    var jobId = Guid.NewGuid().ToString("N");
    BackgroundJob.Schedule<IForecastProcessorService>(svc => svc.ProcessForecastsAsync2(jobId), DateTime.Now.AddSeconds(1));
    return $"Enqueued {jobId}";
});
```

Alter the current `/forecast/process` to use `AddMinute(1)` instead of seconds.
Reload project and trigger both.
