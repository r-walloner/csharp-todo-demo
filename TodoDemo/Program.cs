using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TodoDemo.Database;

var builder = WebApplication.CreateBuilder(args);


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
