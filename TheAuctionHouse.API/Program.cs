using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TheAuctionHouse.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TheAuctionHouse API",
        Version = "v1",
        Description = "Auction platform APIs with JWT authentication. Use the Authorize button with a Bearer token."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token"
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

var dataProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
if (string.Equals(dataProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<InMemoryAppDbContext>(options =>
        options.UseInMemoryDatabase("TheAuctionHouseDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=auctionhouse.db";
    builder.Services.AddDbContext<InMemoryAppDbContext>(options =>
        options.UseSqlite(connectionString));
}

builder.Services.AddScoped<IAppUnitOfWork, InMemoryAppUnitOfWork>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IPortalUserService>(serviceProvider =>
{
    var unitOfWork = serviceProvider.GetRequiredService<IAppUnitOfWork>();
    var emailService = serviceProvider.GetRequiredService<IEmailService>();
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "super-secure-development-key-change-me-12345";
    return new PortalUserService(unitOfWork, emailService, jwtKey);
});

var jwtSecret = builder.Configuration["Jwt:Key"] ?? "super-secure-development-key-change-me-12345";
var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TheAuctionHouse",
    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TheAuctionHouseClient",
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
};

builder.Services.AddSingleton(tokenValidationParameters);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = tokenValidationParameters;
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InMemoryAppDbContext>();
    dbContext.Database.EnsureCreated();

    if (!dbContext.PortalUsers.Any())
    {
        var seedAdminName = builder.Configuration["SeedAdmin:Name"] ?? "System Admin";
        var seedAdminEmail = builder.Configuration["SeedAdmin:Email"] ?? "admin@auctionhouse.local";
        var seedAdminPassword = builder.Configuration["SeedAdmin:Password"] ?? "Admin@12345";

        var adminUser = new PortalUser
        {
            Name = seedAdminName,
            EmailId = seedAdminEmail,
            HashedPassword = HashPassword(seedAdminPassword),
            IsAdmin = true,
            IsBlocked = false,
            WalletBalence = 0,
            WalletBalenceBlocked = 0
        };

        dbContext.PortalUsers.Add(adminUser);
        dbContext.SaveChanges();

        dbContext.Wallets.Add(new Wallet
        {
            UserId = adminUser.Id,
            Amount = 0,
            BlockedAmount = 0
        });
        dbContext.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TheAuctionHouse API v1");
        options.RoutePrefix = string.Empty;
        options.EnablePersistAuthorization();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string HashPassword(string password)
{
    using var sha256 = SHA256.Create();
    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    return Convert.ToHexString(bytes);
}
