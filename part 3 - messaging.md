# Part 3 - messaging

We will use RabbitMQ and Rebus for messaging.

# 3.1 Docker Compose

Add a new service to `docker-compose.yml` file:

```
  rabbitmq:
    image: rabbitmq:3.13.1-management
    container_name: rabbitmq
    ports:
      - 5672:5672
      - 15672:15672
    volumes:
      - ~/.docker-conf/rabbitmq/data/:/var/lib/rabbitmq/
      - ~/.docker-conf/rabbitmq/log/:/var/log/rabbitmq
```

And run `docker compose up -d` to start it.
Admin interface is available on http://localhost:15672. User name and password
are `guest`.

# 3.2 Add Rebus

Add the following libraries to `api` project:

```sh
dotnet add package Rebus.RabbitMq
dotnet add package Rebus.Serilog
dotnet add package Rebus.ServiceProvider
```

Add a new connection string to `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMq": "amqp://localhost"
  }
}
```

Configure Rebus with the following configuration:

```csharp
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
```

Create a new file `ForecastEvent.cs` and create our first event:

```csharp
public sealed record ForecastEvent(DateTime Date, int TemperatureC, string? Summary);
```

Finally, create `ForecastHandler.cs` and add handler:

```csharp
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
```

Now we have to publish some messages. Let's add a temporary endpoint for testing:

```csharp
app.MapPost("/forecast/publish-random", async (IBus bus) =>
{
    await bus.Send(new ForecastEvent(
        DateTime.Now.AddDays(Random.Shared.Next(0, 10)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)]
    ));
    return "Published";
});
```

Navigate to Swagger UI and trigger random publishing. Observe console log.

# 3.3 Shared library and multiple publishers

Let's move messages into a dedicated library. Run the following in `src/`:

```sh
dotnet new classlib -o messages
dotnet sln add messages
cd api
dotnet add reference ../messages
```

Then:

- remove `Class1.cs` from the newly created project
- Move `ForecastEvent.cs` to messages project
  - Change namespace to `messages` for the event
  - Fix references

Let's alter our `ForecastHandler.cs` to store messages in DB:

```csharp
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
```

It is time to introduce another publisher. In `src` run the following:

```sh
dotnet new console -o publisher
dotnet sln add publisher
cd publisher
dotnet add reference ../messages
dotnet add package Rebus.RabbitMq
dotnet add package Rebus.Serilog
dotnet add package Rebus.ServiceProvider
```

Change `Program.cs` to:

```csharp
using messages;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;

var services = new ServiceCollection();
services.AddRebus(c => c
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq("amqp://localhost", "ForecastQueue"))
    .Routing(r => r.TypeBased().Map<ForecastEvent>("ForecastQueue"))
);

using var provider = services.BuildServiceProvider();
provider.StartRebus();

var bus = provider.GetService<IBus>()!;

while (true)
{
    await bus.Send(new ForecastEvent(DateTime.Now, 11, "Test"));
    Console.WriteLine("Event published");

    await Task.Delay(1000);
}
```

We can now run `publisher` in parallel with `dotnet run`.

# 3.4 Event versioning

Business requirements have changed:

- `Summary` is now a required field
- `Location` is a a new required field

Let's introduce a new event:

```csharp
public sealed record ForecastEvent2(DateTime Date, int TemperatureC, string Summary, string Location);
```

Add a new interface to `ForecastHandler`: `, IHandleMessages<ForecastEvent2>`
with implementation:

```csharp
    public async Task Handle(ForecastEvent2 message)
    {
        logger.LogInformation("Received forecast event: {Date} {TemperatureC} {Summary}", message.Date, message.TemperatureC, message.Summary);

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
    }
```

Add `Bogus` package to `api` with `dotnet add package Bogus`.

Change the routing in `Program.cs` to:

```csharp
            .Routing(r => r.TypeBased()
                .Map<ForecastEvent>("ForecastQueue")
                .Map<ForecastEvent2>("ForecastQueue")
            )
```

Finally, let's alter the random publish method:

```csharp
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
```

# 3.5 Deployment

Before we deploy, let's add a healthcheck for a new dependency. First, add the
package `dotnet add package AspNetCore.HealthChecks.Rabbitmq` with:

```sh
dotnet add package dotnet add package AspNetCore.HealthChecks.Rabbitmq
```

Then, chain `.AddRabbitMQ()` to the existing healthchecks:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WeatherForecastContext>()
    .AddRabbitMQ();
```

To ensure healthcheck works properly, we need to register a connection outside of
Rebus:

```csharp
builder.Services.AddSingleton<IConnection>(c =>
{
    return new ConnectionFactory()
    {
        Uri = new Uri(builder.Configuration.GetConnectionString("RabbitMq"))
    }.CreateConnection();
});
```

If we deploy the app now, it will fail healthchecks. It is time to deploy RabbitMQ to `kind`.
We will use Helm:

```sh
	helm upgrade --install rabbitmq bitnami/rabbitmq \
		--set auth.password=RabbitMqPassword \
		--set auth.erlangCookie=M8pbajEbtndUplgF
```

Change `manifests/api.yml` to include additional environment variable:

```yaml
- name: CUSTOMCONNSTR_RabbitMq
  value: "amqp://user:RabbitMqPassword@rabbitmq.default.svc.cluster.local"
```

Deploying the app with `kubectl apply -f manifests` should work as expected.
