var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AICalendar_ApiService>("apiservice");

builder.AddProject<Projects.AICalendar_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.AICalendar_Api>("aicalendar-api");

builder.Build().Run();
