using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=postgres;Port=5432;Database=private_solutions_network;Username=balena;Password=balena";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var authority = builder.Configuration["Jwt:Authority"];
var audience = builder.Configuration["Jwt:Audience"] ?? "account";
var externalAuthority = "https://psnadmin.atun-bey.com/realms/private-solutions-network";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(authority),
            ValidateAudience = true,
            IssuerValidator = (issuer, token, parameters) =>
            {
                if (IssuerMatches(issuer, authority) || IssuerMatches(issuer, externalAuthority))
                {
                    return issuer;
                }

                throw new SecurityTokenInvalidIssuerException($"The issuer '{issuer}' is invalid");
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "policy-api" }));
app.MapGet("/api/policy/healthz", () => Results.Ok(new { status = "ok", service = "policy-api" }));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static bool IssuerMatches(string? left, string? right)
{
    if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
    {
        return false;
    }

    return string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
