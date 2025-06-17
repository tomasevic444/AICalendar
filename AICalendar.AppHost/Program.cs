using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var mongoConnectionStringFromAppHostSecrets = builder.Configuration.GetConnectionString("mongodb")
                                    ?? throw new InvalidOperationException("CRITICAL: MongoDB connection string 'mongodb' NOT FOUND in AppHost's User Secrets. Verify the AppHost's secrets.json file.");

var calendarApi = builder.AddProject<Projects.AICalendar_Api>("aicalendarapi") // Ensure Projects.AICalendar_Api is correct
                         .WithEnvironment("ConnectionStrings__mongodb", mongoConnectionStringFromAppHostSecrets); // This is your working solution

builder.Build().Run();