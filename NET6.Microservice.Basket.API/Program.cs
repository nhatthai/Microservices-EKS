using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using NET6.Microservice.Basket.API;
using NET6.Microservice.Basket.API.Infrastructure.Filters;
using NET6.Microservice.Basket.API.Infrastructure.Repositories;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Core.PathBases;
using Serilog;
using Serilog.Exceptions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(
    options =>
    {
        options.Filters.Add(typeof(HttpGlobalExceptionFilter));
        options.Filters.Add(typeof(ValidateModelStateFilter));
    }
).AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var configuration = builder.Configuration;

//create the logger and setup your sinks, filters and properties
Log.Logger = new LoggerConfiguration()
    .Enrich.WithExceptionDetails()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("Environment", configuration.GetValue<string>("Environment"))
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddOptions();
builder.Services.AddOptions<Auth0Settings>();
builder.Services.Configure<BasketSettings>(configuration);
builder.Services.Configure<Auth0Settings>(configuration.GetSection("Auth0"));

// Add services to the container.
builder.Services.AddCors(c =>
{
    c.AddPolicy("AllowOrigin", builder =>
        builder.WithOrigins(configuration.GetValue<string>("OriginWeb"))
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
    );
});

var auth0Configuration = configuration.GetSection("Auth0").Get<Auth0Settings>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Auth0", builder =>
    {
        builder.AllowAnyOrigin().WithOrigins(auth0Configuration.Authority)
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "eShop - Basket HTTP API",
        Version = "v1",
        Description = "The Basket Service HTTP API"
    });

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows()
        {
            ClientCredentials = new OpenApiOAuthFlow()
            {
                TokenUrl = new Uri($"{auth0Configuration.Authority}/oauth/token"),
                Scopes = new Dictionary<string, string>()
            }
        },
        In = ParameterLocation.Header,
        Name = HeaderNames.Authorization
    });

    options.OperationFilter<AuthorizeCheckOperationFilter>();
});



builder.Services.AddSingleton<ConnectionMultiplexer>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<BasketSettings>>().Value;
    var configuration = ConfigurationOptions.Parse(settings.ConnectionString, true);

    configuration.ResolveDns = true;

    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient<IBasketRepository, RedisBasketRepository>();

ConfigureAuthService(builder.Services, auth0Configuration);

OpenTelemetryStartup.InitOpenTelemetryTracing(
    builder.Services, configuration, "BasketAPI", Array.Empty<string>(), builder.Environment);

// Add the IStartupFilter using the helper method
PathBaseStartup.AddPathBaseFilter(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(setup =>
    {
        // Required in order to send audience to Auth0 as it expects
        setup.UseRequestInterceptor("(req) => { if (req.url.indexOf('oauth/token') > 0 && req.body) req.body += '&audience=" + auth0Configuration.Audience + "'; return req; }");

        setup.SwaggerEndpoint("/swagger/v1/swagger.json", "Basket.API V1");
        setup.OAuthClientId(auth0Configuration.ClientId);
        setup.OAuthClientSecret(auth0Configuration.ClientSecret);
        //setup.OAuthAppName("Basket Swagger UI");
        setup.EnableDeepLinking();
    });
}

app.UseCors("AllowOrigin");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("Auth0");

app.MapControllers();

app.Run();

static void ConfigureAuthService(IServiceCollection services, Auth0Settings configuration)
{
    // prevent from mapping "sub" claim to name identifier.
    //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

    }).AddJwtBearer(options =>
    {
        options.Authority = configuration.Authority;
        options.Audience = configuration.Audience;
    });
}