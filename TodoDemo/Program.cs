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

var app = builder.Build();


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

app.Run();
