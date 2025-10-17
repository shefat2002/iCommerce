using iCommerceAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services to the container.

//builder.Services.AddControllers();
builder.Services.AddControllers(options =>
{
    options.OutputFormatters.RemoveType<SystemTextJsonOutputFormatter>();
    options.OutputFormatters.Add(new SystemTextJsonOutputFormatter(new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = null,
        WriteIndented = true,
        TypeInfoResolver = JsonSerializerOptions.Default.TypeInfoResolver
    }));
});

// Add Jwt Authentication Services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["keyCloak:Authority"];
    options.Audience = builder.Configuration["keyCloak:Audience"];
    options.RequireHttpsMetadata = false; //True in production
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "preferred_username", // Adjust based on Keycloak configuration
        RoleClaimType = "role", // Adjust based on Keycloak configuration
        ValidateIssuer = true, // Ensure the token is from a trusted issuer
        ValidIssuer = builder.Configuration["keyCloak:Authority"], // Keycloak server URL
        ValidateAudience = true, // Ensure the token is intended for this audience
        ValidAudience = builder.Configuration["keyCloak:Audience"], // Your API's audience
    };
    options.Events = new JwtBearerEvents // Handle token validation events
    {
        OnTokenValidated = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity &&
            context.Principal.HasClaim(c => c.Type == "resource_access")) // Check for resource_access claim
            {
                // Example: Add custom claims or perform additional validation
                // identity.AddClaim(new Claim("custom-claim", "value"));
                var resourceAccess = JsonDocument.Parse(context.Principal.FindFirst("resource_access")!.Value); // Parse the resource_access claim
                var clientRoles = resourceAccess.RootElement.GetProperty(builder.Configuration["keyCloak:ClientId"]).GetProperty("roles"); // Extract roles for the specific client
                foreach (var role in clientRoles.EnumerateArray()) // Iterate through roles
                {
                    identity.AddClaim(new Claim("roles", role.GetString()!)); // Add roles to ClaimsIdentity
                }
            }
            return Task.CompletedTask;
        }
    };
});

// Add Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("admin"));
});

//Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
