using System.Text;
using System.Text.Json.Serialization;
using GymManagement.Application;
using GymManagement.Application.Interfaces;
using GymManagement.Infrastructure;
using GymManagement.Infrastructure.Data.Seeders;
using GymManagement.Api.Services;
using GymManagement.Api.Middleware;
using GymManagement.Api.Filters;
using GymManagement.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Enums travel as their names ("Cash", "Lbp"), not as integers. The frontend already
    // types them as string literals, and without this converter those requests fail to
    // bind. Names also survive someone inserting a new enum member in the middle, which
    // ordinals do not.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration with JWT support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gym Management API",
        Version = "v1",
        Description = "API for Gym Client Management System"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Infrastructure and Application layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Add CurrentUserService
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Refuse to start on a missing, weak or previously-committed secret. Runs before anything
// that depends on those secrets, so the failure is a clear startup message rather than a
// confusing error at first login.
foreach (var warning in SecurityStartupChecks.Validate(builder.Configuration, builder.Environment))
{
    Log.Warning("Security: {Warning}", warning);
}

// JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;
var key = Encoding.UTF8.GetBytes(jwtSecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    // The owner. Everything that changes what the gym charges, reveals what it has
    // earned, or can quietly reduce recorded income.
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

    // The desk. Reception runs the day - find a member, take a payment, add somebody,
    // freeze a membership - and the owner can do all of that too, so both roles pass.
    //
    // The line reception must not cross is money leaving the record. Payments are
    // append-only, so a desk that cannot reverse structurally cannot make money vanish
    // from the till, which is most of why the owner wanted this system. Everything else
    // reception is refused - revenue history, the audit trail, prices, accounts - follows
    // from the same idea: they operate the gym, they do not audit it.
    options.AddPolicy("AdminOrStaff", policy => policy.RequireRole("Admin", "Staff"));

    options.AddPolicy("ClientOnly", policy => policy.RequireRole("Client"));
    options.AddPolicy("AdminOrClient", policy => policy.RequireRole("Admin", "Client"));
});


// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Seed database
await DataSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gym Management API v1");
    });
}

app.UseSerilogRequestLogging();

// Global exception handling
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
