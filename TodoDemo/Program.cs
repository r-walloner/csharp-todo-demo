using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TodoDemo.Database;

var builder = WebApplication.CreateBuilder(args);

// Listen on configured port or default to 8080
// Scaleway serverless containers set the PORT environment variable.
// Once the port defined this way is bound, Scaleway considers the container healthy.
// On platforms where PORT is not set (e.g. locally), we default to 8080
int port = int.TryParse(builder.Configuration["PORT"], out var p) ? p : 8080;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});


// ---- Services ----
// ------------------

// Configure db context
var connectionString = builder.Configuration.GetConnectionString("TodoDb");
builder.Services.AddDbContext<TodoDbContext>(options =>
{
    options.UseNpgsql(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        // detailed errors in development mode
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks().AddDbContextCheck<TodoDbContext>(name: "database", tags: ["ready"]);

var app = builder.Build();


// ---- Migration -----
// --------------------

if (args.Contains("--migrate"))
{
    // Run database migrations and exit if the --migrate argument is passed to the app.
    // This is used by the one-shot container that runs migrations before starting the main app container.
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    
    app.Logger.LogInformation("Migrating database...");
    await dbContext.Database.MigrateAsync();
    app.Logger.LogInformation("Database migration complete.");

    return;
}


// ---- HTTP request pipeline ----
// -------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// app.UseHttpsRedirection();

// app.UseAuthorization();

app.MapControllers();

// Add health check endpoints for "application live" and "application ready to server requests"
app.MapHealthChecks("/health/live", new()
{
    // Don't perform any checks, just return OK as soon as the app is running
    Predicate = (check) => false
});
app.MapHealthChecks("/health/ready", new()
{
    // Only return OK if all checks with tag "ready" are healthy (e.g., database)
    Predicate = (check) => check.Tags.Contains("ready")
});

app.Run();
