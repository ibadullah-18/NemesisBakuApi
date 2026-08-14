using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using NemesisBakuApi.Data;
using NemesisBakuApi.Data.Interceptors;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.HealthChecks;
using NemesisBakuApi.Middlewares;
using NemesisBakuApi.Services.Implementations;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;
using NemesisBakuApi.Validations;

var builder = WebApplication.CreateBuilder(args);

const long maxUploadRequestBytes =
    60L * 1024 * 1024;

const long outputCacheSizeBytes =
    25L * 1024 * 1024;

const long maximumCachedResponseBytes =
    1024L * 1024;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        maxUploadRequestBytes;

    options.Limits.KeepAliveTimeout =
        TimeSpan.FromMinutes(2);

    options.Limits.RequestHeadersTimeout =
        TimeSpan.FromSeconds(20);
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        maxUploadRequestBytes;

    options.MemoryBufferThreshold =
        64 * 1024;

    options.ValueCountLimit = 1024;
    options.MultipartHeadersCountLimit = 32;

    options.MultipartHeadersLengthLimit =
        16 * 1024;
});

builder.Services.AddControllers();

builder.Services.AddOutputCache(options =>
{
    options.SizeLimit = outputCacheSizeBytes;

    options.MaximumBodySize =
        maximumCachedResponseBytes;

    options.AddPolicy(
        ProductCacheTags.ProductListsPolicy,
        policy => policy
            .Expire(TimeSpan.FromSeconds(30))
            .Tag(ProductCacheTags.Tag));

    options.AddPolicy(
        ProductCacheTags.FilterOptionsPolicy,
        policy => policy
            .Expire(TimeSpan.FromMinutes(5))
            .Tag(ProductCacheTags.Tag));
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.Configure<WhatsAppSettings>(
    builder.Configuration.GetSection("WhatsApp"));

builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("Cloudinary"));

builder.Services.Configure<DeliverySettings>(
    builder.Configuration.GetSection("Delivery"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection(
        TelegramSettings.SectionName));

builder.Services.Configure<AuthenticationCleanupSettings>(
    builder.Configuration.GetSection(
        AuthenticationCleanupSettings.SectionName));

builder.Services.Configure<EmailAnnouncementWorkerSettings>(
    builder.Configuration.GetSection(
        EmailAnnouncementWorkerSettings.SectionName));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "NemesisBaku API",
            Version = "v1"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT token daxil edin"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType
                                    .SecurityScheme,

                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

builder.Services.AddScoped<
    ProductCacheInvalidationInterceptor>();

builder.Services.AddDbContext<AppDbContext>(
    (serviceProvider, options) =>
    {
        options.UseSqlServer(
    builder.Configuration
        .GetConnectionString(
            "DefaultConnection"));

        options.AddInterceptors(
            serviceProvider.GetRequiredService<
                ProductCacheInvalidationInterceptor>());
    });

builder.Services
    .AddIdentity<AppUser, IdentityRole<Guid>>(
        options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;

            options.Password
                .RequireNonAlphanumeric = false;

            options.User.RequireUniqueEmail = true;

            options.Lockout.AllowedForNewUsers = true;

            options.Lockout.MaxFailedAccessAttempts =
                5;

            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(5);
        })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddSingleton<OtpCodeHasher>();

builder.Services.AddScoped<
    IFileService,
    CloudinaryFileService>();

builder.Services.AddScoped<
    IAuditLogService,
    AuditLogService>();

builder.Services.AddScoped<
    IEmailService,
    EmailService>();

builder.Services.AddScoped<
    IEmailTemplateService,
    EmailTemplateService>();

builder.Services.AddValidatorsFromAssemblyContaining<
    ProductCreateDtoValidator>();

builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddHttpClient();

builder.Services.AddHttpClient<
    ITelegramBotService,
    TelegramBotService>();

builder.Services.AddScoped<
    ITelegramOrderNotificationOutbox,
    TelegramOrderNotificationOutbox>();

builder.Services.AddHostedService<
    TelegramOrderNotificationWorker>();

builder.Services.AddHostedService<
    TelegramWebhookSetupService>();

builder.Services.AddHostedService<
    AuthenticationDataCleanupWorker>();

builder.Services.AddHostedService<
    EmailAnnouncementWorker>();

var jwtSettings =
    builder.Configuration.GetSection("Jwt");

var jwtKey = jwtSettings["Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT Key konfiqurasiya edilməyib.");
}

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    jwtSettings["Issuer"],

                ValidAudience =
                    jwtSettings["Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "CorsPolicy",
        policy =>
        {
            policy
                .WithOrigins(
                    "https://nemesisbaku.az",
                    "https://www.nemesisbaku.az",
                    "http://localhost:3000",
                    "http://localhost:3001",
                    "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;

    options.Providers.Add<
        BrotliCompressionProvider>();

    options.Providers.Add<
        GzipCompressionProvider>();
});

builder.Services.Configure<
    BrotliCompressionProviderOptions>(
    options =>
    {
        options.Level =
            CompressionLevel.Fastest;
    });

builder.Services.Configure<
    GzipCompressionProviderOptions>(
    options =>
    {
        options.Level =
            CompressionLevel.Fastest;
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        "auth",
        context =>
            RateLimitPartition
                .GetFixedWindowLimiter(
                    GetClientIdentifier(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window =
                            TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

    options.AddPolicy(
        "otp",
        context =>
            RateLimitPartition
                .GetFixedWindowLimiter(
                    GetClientIdentifier(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window =
                            TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

    options.OnRejected = async (
        context,
        cancellationToken) =>
    {
        context.HttpContext.Response.ContentType =
            "application/json; charset=utf-8";

        var response =
            ApiResponse<string>.Fail(
                "Çox sayda sorğu göndərildi. " +
                "Bir qədər sonra yenidən cəhd edin.");

        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web)),
            cancellationToken);
    };
});

builder.Services.Configure<
    ForwardedHeadersOptions>(
    options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

var app = builder.Build();

app.UseForwardedHeaders();

app.UseMiddleware<ExceptionMiddleware>();

app.UseResponseCompression();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("CorsPolicy");

app.UseOutputCache();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db =
        services.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();

    await DbSeeder.SeedRolesAsync(services);
}

app.Run();

static string GetClientIdentifier(
    HttpContext context)
{
    return context.Connection
        .RemoteIpAddress?
        .ToString()
        ?? "unknown";
}