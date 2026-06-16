using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CityCare.Api.Extensions;
using CityCare.Api.Services;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minio;

// Désactiver le mapping automatique des claims JWT
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDbContext<CityCareDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCityCareSwagger(builder.Configuration);

// Services métier
builder.Services.AddHttpClient();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<GeocodeService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<KeycloakService>();

// Stockage de fichiers (MinIO / S3)
var minioOptions = builder.Configuration.GetSection("Minio").Get<MinioOptions>() ?? new MinioOptions();
builder.Services.AddSingleton(minioOptions);
builder.Services.AddSingleton<IMinioClient>(_ =>
    new MinioClient()
        .WithEndpoint(minioOptions.Endpoint)
        .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
        .WithSSL(minioOptions.UseSSL)
        .Build());
builder.Services.AddScoped<PhotoStorageService>();

// Authentification
var keycloakUrl = builder.Configuration["Keycloak:Url"];
var keycloakRealm = builder.Configuration["Keycloak:Realm"];

if (string.IsNullOrWhiteSpace(keycloakUrl) || string.IsNullOrWhiteSpace(keycloakRealm))
    throw new InvalidOperationException("Keycloak:Url et Keycloak:Realm doivent être configurés.");

var keycloakIssuer = $"{keycloakUrl.TrimEnd('/')}/realms/{keycloakRealm}";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakIssuer;
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidIssuers = new[]
            {
                keycloakIssuer,
                $"http://localhost:8080/realms/{keycloakRealm}",
                $"http://keycloak:8080/realms/{keycloakRealm}"
            },
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };

        // Mapping des rôles (Keycloak)
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var roles = context.Principal
                        .FindAll("roles")
                        .Select(c => c.Value.ToLower())
                        .Distinct();

                    foreach (var role in roles)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                    // Ajouter le "mainRole" si présent
                    var mainRole = context.Principal.FindFirst("mainRole")?.Value;
                    if (!string.IsNullOrEmpty(mainRole))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, mainRole.ToLower()));
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCityCareSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();