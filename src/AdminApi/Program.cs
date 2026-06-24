using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=postgres;Port=5432;Database=private_solutions_network;Username=balena;Password=balena";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var authority = builder.Configuration["Jwt:Authority"];
var audience = builder.Configuration["Jwt:Audience"] ?? "account";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.Authority = authority;
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity && context.SecurityToken is JwtSecurityToken jwt)
                {
                    AddRoles(identity, jwt, "realm_access", null);
                    AddRoles(identity, jwt, "resource_access", "psn-admin-portal");
                }

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(authority),
            ValidateAudience = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "preferred_username"
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "admin-api" }));
app.MapGet("/api/admin/healthz", () => Results.Ok(new { status = "ok", service = "admin-api" }));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddRoles(ClaimsIdentity identity, JwtSecurityToken jwt, string claimName, string? resourceClient)
{
    if (!jwt.Payload.TryGetValue(claimName, out var claimValue))
    {
        return;
    }

    using var document = JsonDocument.Parse(JsonSerializer.Serialize(claimValue));

    if (resourceClient is null)
    {
        if (document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                var roleName = role.GetString();
                if (!string.IsNullOrWhiteSpace(roleName) && !identity.HasClaim(ClaimTypes.Role, roleName))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }

        return;
    }

    if (!document.RootElement.TryGetProperty(resourceClient, out var clientNode))
    {
        return;
    }

    if (clientNode.TryGetProperty("roles", out var clientRoles) && clientRoles.ValueKind == JsonValueKind.Array)
    {
        foreach (var role in clientRoles.EnumerateArray())
        {
            var roleName = role.GetString();
            if (!string.IsNullOrWhiteSpace(roleName) && !identity.HasClaim(ClaimTypes.Role, roleName))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
            }
        }
    }
}
