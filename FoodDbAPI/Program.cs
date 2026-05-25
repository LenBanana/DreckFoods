using System.Security.Claims;
using System.Data;
using System.Data.Common;
using System.Text;
using FoodDbAPI.Data;
using FoodDbAPI.Models.Settings;
using FoodDbAPI.Services;
using FoodDbAPI.Services.AI.Abstractions;
using FoodDbAPI.Services.AI.Agent;
using FoodDbAPI.Services.AI.Providers.OpenAI;
using FoodDbAPI.Services.AI.Sessions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using IFddbScrapingService = FoodDbAPI.Services.Interfaces.IFddbScrapingService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FoodDbAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            []
        }
    });
});

// Add Entity Framework
builder.Services.AddDbContext<FoodDbContext>((_, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);

// Add Mail Settings
builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("FrontendSettings"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// Add AI Settings
builder.Services.Configure<AISettings>(builder.Configuration.GetSection("AISettings"));
builder.Services.AddScoped<IAIProvider, OpenAIProvider>();
builder.Services.AddSingleton<IMealSessionStore, InMemoryMealSessionStore>();
builder.Services.AddHostedService(sp =>
    (InMemoryMealSessionStore)sp.GetRequiredService<IMealSessionStore>());
builder.Services.AddScoped<IMealAgent, MealAgent>();

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
            NameClaimType = ClaimTypes.NameIdentifier,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.CanImportData, policy => policy.RequireRole(
            AppRoles.DataEditor,
            AppRoles.Admin
        )
    );

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder =>
        {
            policyBuilder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(60));
        });
});

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IWeightService, WeightService>();
builder.Services.AddScoped<IFddbEditorService, FddbEditorService>();
builder.Services.AddHttpClient<IFddbScrapingService, FddbScrapingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:139.0) Gecko/20100101 Firefox/139.0");
    client.DefaultRequestHeaders.Add("Accept",
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "de,en-US;q=0.7,en;q=0.3");
    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    client.DefaultRequestHeaders.Add("DNT", "1");
});
builder.Services.AddScoped<IFoodSearchService, FoodSearchService>();
builder.Services.AddScoped<IFoodEntryService, FoodEntryService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<IFoodService, FoodService>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IDataImportService, DataImportService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await InitializeDatabaseAsync(app.Services);

app.Run();

static async Task InitializeDatabaseAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<FoodDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");

    await BaselineLegacyEnsureCreatedSchemaAsync(context, logger);
    await context.Database.MigrateAsync();
}

static async Task BaselineLegacyEnsureCreatedSchemaAsync(FoodDbContext context, ILogger logger)
{
    var connection = context.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
        await connection.OpenAsync();

    try
    {
        var hasInitialSchema = await TableExistsAsync(connection, "Users") &&
                               await TableExistsAsync(connection, "FoodEntries") &&
                               await TableExistsAsync(connection, "FddbFoods");

        if (!hasInitialSchema)
            return;

        logger.LogInformation("Reconciling EF migration history with the current database schema.");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32) NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            );");

        await EnsureMigrationHistoryEntryAsync(context,
            "20250603145542_InitialCreate",
            "9.0.5");

        var hasMealSchema = await TableExistsAsync(connection, "Meals") &&
                            await TableExistsAsync(connection, "MealItems") &&
                            await ColumnExistsAsync(connection, "FoodEntries", "FddbFoodId") &&
                            await ColumnExistsAsync(connection, "FddbFoods", "Ean");
        if (hasMealSchema)
        {
            await EnsureMigrationHistoryEntryAsync(context,
                "20250608013529_MealCreation",
                "9.0.5");
        }

        var hasNutritionExpansionSchema = await TableExistsAsync(connection, "Meals") &&
                                         await ColumnTypeContainsAsync(connection, "Meals", "Description", "text") &&
                                         await ColumnExistsAsync(connection, "FoodEntries", "Caffeine") &&
                                         await ColumnExistsAsync(connection, "FoodEntries", "Salt") &&
                                         await ColumnExistsAsync(connection, "FddbFoodNutritions", "CaffeineUnit") &&
                                         await ColumnExistsAsync(connection, "FddbFoodNutritions", "CaffeineValue");
        if (hasNutritionExpansionSchema)
        {
            await EnsureMigrationHistoryEntryAsync(context,
                "20260524143956_RemoveMealDescriptionLengthLimit",
                "9.0.5");
        }

        var hasServingsSchema = await ColumnExistsAsync(connection, "FddbFoods", "ServingsJson");
        if (hasServingsSchema)
        {
            await EnsureMigrationHistoryEntryAsync(context,
                "20260524194305_AddServingsJsonToFddbFood",
                "9.0.5");
        }

        var hasServingHistorySchema = await ColumnExistsAsync(connection, "FoodEntries", "ServingName") &&
                                      await TableExistsAsync(connection, "MealPortionLogs");
        if (hasServingHistorySchema)
        {
            await EnsureMigrationHistoryEntryAsync(context,
                "20260525131654_AddServingHistorySupport",
                "9.0.5");
        }
    }
    finally
    {
        if (shouldCloseConnection)
            await connection.CloseAsync();
    }
}

static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @tableName
        );";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "@tableName";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);

    var result = await command.ExecuteScalarAsync();
    return result is true || (result is bool boolResult && boolResult);
}

static async Task<bool> ColumnExistsAsync(DbConnection connection, string tableName, string columnName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName
        );";

    var tableParameter = command.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    command.Parameters.Add(tableParameter);

    var columnParameter = command.CreateParameter();
    columnParameter.ParameterName = "@columnName";
    columnParameter.Value = columnName;
    command.Parameters.Add(columnParameter);

    var result = await command.ExecuteScalarAsync();
    return result is true || (result is bool boolResult && boolResult);
}

static async Task<bool> ColumnTypeContainsAsync(DbConnection connection, string tableName, string columnName, string expectedFragment)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT data_type
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName;";

    var tableParameter = command.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    command.Parameters.Add(tableParameter);

    var columnParameter = command.CreateParameter();
    columnParameter.ParameterName = "@columnName";
    columnParameter.Value = columnName;
    command.Parameters.Add(columnParameter);

    var result = await command.ExecuteScalarAsync();
    return result is string dataType &&
           dataType.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase);
}

static Task EnsureMigrationHistoryEntryAsync(FoodDbContext context, string migrationId, string productVersion)
{
    return context.Database.ExecuteSqlRawAsync(@"
        INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
        SELECT {0}, {1}
        WHERE NOT EXISTS (
            SELECT 1 FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" = {0}
        );", migrationId, productVersion);
}