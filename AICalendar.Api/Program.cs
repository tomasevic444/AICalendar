// CalendarApp.Api/Program.cs
using AICalendar.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// --- TEMPORARY DEBUGGING ---
Console.WriteLine("--- API Configuration Sources ---");
foreach (var source in builder.Configuration.Sources)
{
    Console.WriteLine($"Source: {source.GetType().Name}");
}
Console.WriteLine("--- API Configuration Values (ConnectionStrings section) ---");
var connectionStringsSection = builder.Configuration.GetSection("ConnectionStrings");
foreach (var kvp in connectionStringsSection.AsEnumerable(makePathsRelative: true)) // makePathsRelative for brevity
{
    Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
}
string? debugMongoConnString = builder.Configuration.GetConnectionString("mongodb");
Console.WriteLine($"DEBUG: Value from GetConnectionString(\"mongodb\"): '{debugMongoConnString}'");
// --- END TEMPORARY DEBUGGING ---

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

builder.Services.AddEndpointsApiExplorer(); 
builder.Services.AddSwaggerGen();      

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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