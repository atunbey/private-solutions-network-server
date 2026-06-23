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
            ValidateAudience = true
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
