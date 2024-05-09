# Part 6 - Aspire

## Installation

Aspire is currently in preview and is installed optionally. [Install .NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=dotnet-cli%2Cunix#install-net-aspire).

```sh
dotnet workload update
dotnet workload install aspire
dotnet workload list
```

In the `src` folder, run the following

```sh
# Create an apphost - this will run our app
dotnet new aspire-apphost -o apphost
dotnet sln add apphost

# apphost needs to see our service
dotnet add apphost/apphost.csproj reference api

# Some defaults
# Create a shared project - this comes in handy for multiple projects
dotnet new aspire-servicedefaults -o servicedefaults
dotnet sln add servicedefaults
# Ensure api can use defaults
dotnet add api/api.csproj reference servicedefaults
```

Add `builder.AddServiceDefaults();` to `Program.cs` to get the defaults.

Finally, let's run our project. In `src` type `dotnet run --project apphost`.

## 6.1 Custom metrics

OpetTelemetry is using a different system from Prometheus.
We need a new package in `api`:

```sh
dotnet add package System.Diagnostics.DiagnosticSource
```

## 6.2 Entity Framework telemetry
```
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore --version 1.0.0-beta.11
```