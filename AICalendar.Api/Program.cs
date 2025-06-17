// CalendarApp.Api/Program.cs
using AICalendar.Application.Interfaces;
using AICalendar.Infrastructure.Data;
using AICalendar.Infrastructure.Services;

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

builder.Services.AddEndpointsApiExplorer(); 
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IUserService, UserService>();

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