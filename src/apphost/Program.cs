var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.api>("api")
    .WithExternalHttpEndpoints();

builder.Build().Run();
