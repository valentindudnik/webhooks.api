using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;
using Webhooks.Infrastructure.Extensions;
using Webhooks.Infrastructure.Middlewares;
using Webhooks.Infrastructure.Profiles;
using Webhooks.Infrastructure.Validators;
using Webhooks.Models.Dtos;
using Webhooks.RabbitMQ.Client.Extensions;
using Webhooks.RabbitMQ.Models.Configurations;

using Webhooks.Services.Interfaces.Producers;
using Webhooks.Services.Producers;

const string swaggerTitle = "Webhooks Api";
const string swaggerVersion = "v1";
const string swaggerUrl = "/swagger/v1/swagger.json";
const string swaggerName = "Webhooks Api v1";
const string swaggerMediaType = "application/json";
const string swaggerAuthorizationName = "Authorization";
const string swaggerBearerFormat = "JWT";

const string healthzPath = "/healthz";
const string healthzApiPath = "/api/healthz";
const string healthzReadyPath = "/healthz/ready";
const string healthzLivePath = "/healthz/live";
const string healthzServiceUrlParamName = "HealthCheck:ServiceUrl";

const string rabbitMQSectionName = "RabbitMQ";

var builder = WebApplication.CreateBuilder(args);

// Configure Swagger Attributes
builder.Services.AddControllers(configure => {
    configure.Filters.Add(new ProducesAttribute(swaggerMediaType));
    configure.Filters.Add(new ProducesResponseTypeAttribute(typeof(ErrorInfoDto), StatusCodes.Status400BadRequest));
    configure.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
    configure.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
    configure.Filters.Add(new ProducesResponseTypeAttribute(typeof(ErrorInfoDto), StatusCodes.Status404NotFound));
    configure.Filters.Add(new ProducesResponseTypeAttribute(typeof(ErrorInfoDto), StatusCodes.Status500InternalServerError));
}).AddJsonOptions(configure => configure.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
  .AddNewtonsoftJson(configure => configure.SerializerSettings.Converters.Add(new StringEnumConverter()));

// Configure Api Versioning
builder.Services.AddApiVersioning();

// Configure AutoMapper
builder.Services.AddAutoMapper(configure => configure.AddProfile<InvoiceProfile>());

// Configure FluentValidation
builder.Services.AddFluentValidation(configure => configure.RegisterValidatorsFromAssemblyContaining<InvoiceValidator>());

// Configure DI
builder.Services.ConfigureRepositories(builder.Configuration);
builder.Services.ConfigureServices();

// Configure Health Checks
builder.Services.AddHealthChecks()
                .AddUrlGroup(new Uri($"{builder.Configuration[healthzServiceUrlParamName]}{healthzApiPath}"));

// Configure ApplicationInsights
builder.Services.AddApplicationInsightsTelemetry();

// Configure Authentification
// TODO: TEMP

// Configure RabbitMQ
var rabbitMQConfiguration = builder.Configuration.GetSection(rabbitMQSectionName).Get<RabbitMQConfiguration>();
builder.Services.ConfigureRabbitMQClient(rabbitMQConfiguration);
builder.Services.AddSingleton<IInvoiceProducer, InvoiceProducer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(configure => {
    // TODO: TEMP
    //configure.SwaggerDoc(swaggerVersion, new OpenApiInfo
    //{
    //    Title = swaggerTitle,
    //    Version = swaggerVersion
    //});

    //configure.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    //{
    //    In = ParameterLocation.Header,
    //    Name = swaggerAuthorizationName,
    //    Type = SecuritySchemeType.Http,
    //    BearerFormat = swaggerBearerFormat,
    //    Scheme = JwtBearerDefaults.AuthenticationScheme
    //});

    //configure.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new OpenApiReference
    //            {
    //                Type=ReferenceType.SecurityScheme,
    //                Id=JwtBearerDefaults.AuthenticationScheme
    //            }
    //        },
    //        Array.Empty<string>()
    //    }
    //});
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

// TODO: TEMP
//app.UseAuthentication();

//app.UseAuthorization();

app.MapHealthChecks(healthzPath);

app.MapHealthChecks(healthzReadyPath, new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});

app.MapHealthChecks(healthzLivePath, new HealthCheckOptions
{
    Predicate = _ => false
});

app.Map(healthzApiPath, configuration => configuration.Use(async (context, next) =>
{
    await context.Response.WriteAsync(HealthStatus.Healthy.ToString());
    await next(context);
}));

app.UseSwagger();

app.UseSwaggerUI(opts => opts.SwaggerEndpoint(swaggerUrl, swaggerName));

app.MapControllers();

app.Run();
