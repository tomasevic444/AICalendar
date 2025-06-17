// CalendarApp.Api/Program.cs
using AICalendar.Application.Interfaces;
using AICalendar.Infrastructure.Data;
using AICalendar.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

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
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AICalendar API", Version = "v1" });

    // --- Add this section for JWT Authentication to Swagger UI ---
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter into field the word 'Bearer' following by space and JWT",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey, // Using ApiKey for Bearer token input
        Scheme = "Bearer" // The scheme name, "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer" // This Id must match the Id in AddSecurityDefinition
                },
                Scheme = "oauth2", 
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>() // List of scopes (if any, empty for simple Bearer)
        }
    });
    // --- End JWT Authentication Swagger UI section ---
});
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// --- JWT Authentication Setup ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true; // Optional: saves the token in HttpContext.Features
    options.RequireHttpsMetadata = builder.Environment.IsProduction(); // Enforce HTTPS in production
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true, // Checks token expiration
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured."))),
        ClockSkew = TimeSpan.Zero // Optional: remove default 5-min leeway on expiration
    };
});

builder.Services.AddAuthorization(); // Ensure Authorization services are added
// --- End JWT Authentication Setup ---

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