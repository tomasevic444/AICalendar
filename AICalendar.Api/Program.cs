// CalendarApp.Api/Program.cs
using AICalendar.Application.Interfaces;
using AICalendar.Infrastructure.Data;
using AICalendar.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("mongodb")
        ?? throw new InvalidOperationException("MongoDB connection string 'mongodb' not found.");
    options.DatabaseName = builder.Configuration.GetValue<string>("MongoDbSettings:DatabaseName")
        ?? "PersonalCalendarDb";
    options.UsersCollectionName = builder.Configuration.GetValue<string>("MongoDbSettings:UsersCollectionName") ?? "users";
    options.EventsCollectionName = builder.Configuration.GetValue<string>("MongoDbSettings:EventsCollectionName") ?? "events";
    options.EventParticipantsCollectionName = builder.Configuration.GetValue<string>("MongoDbSettings:EventParticipantsCollectionName") ?? "eventParticipants";
});

builder.Services.AddSingleton<CalendarMongoDbContext>();

builder.Services.AddControllers();
// --- API Versioning Setup ---
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // Adds api-supported-versions header
    // Example: Read version from URL segment, header, or media type
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"),
        new MediaTypeApiVersionReader("x-api-version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    // Format for Swagger/OpenAPI: /swagger/v1/swagger.json
    options.GroupNameFormat = "'v'VVV";
    // Substitute API version in route templates
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Example: Add a swagger doc for each discovered API version
    // This requires IApiVersionDescriptionProvider, which you get from AddVersionedApiExplorer
    // You might need to resolve IApiVersionDescriptionProvider in the lambda using a service provider
    // For now, a basic setup is fine. We can refine Swagger later.
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AICalendar API", Version = "v1" });

    // TODO: Add JWT Authentication to Swagger UI later
});
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Configure Swagger UI to list different API versions if you have multiple
    app.UseSwaggerUI(options =>
    {
        // Example: if you have an IApiVersionDescriptionProvider injected or resolved:
        // foreach (var description in provider.ApiVersionDescriptions)
        // {
        //    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        // }
        // For a single version for now:
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AICalendar API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Create MongoDB indexes on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<CalendarMongoDbContext>();
        await dbContext.CreateIndexesAsync();
        Console.WriteLine("MongoDB indexes creation process completed on startup.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating MongoDB indexes.");
    }
}

app.Run();