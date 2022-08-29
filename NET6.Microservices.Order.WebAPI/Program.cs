using Microsoft.Extensions.DependencyInjection;
using NET6.WebAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the IStartupFilter using the helper method
AddPathBaseFilter(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddPathBaseFilter(WebApplicationBuilder builder)
{
    // Fetch the PathBaseSettings section from configuration
    var config = builder.Configuration.GetSection("PathBaseSettings");

    // Bind the config section to PathBaseSettings using IOptions
    builder.Services.Configure<PathBaseSettings>(config);

    // Register the startup filter
    builder.Services.AddTransient<IStartupFilter, PathBaseStartupFilter>();
}

public partial class Program { }


