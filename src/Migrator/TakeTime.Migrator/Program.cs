// =============================================================================
// TakeTime Database Migration Runner
// =============================================================================
// Responsibilities:
//   1. Create/update the TenantManagement database (shared catalog)
//   2. Create/update per-tenant databases for each microservice
//   3. Seed default tenant with Thai resort settings
//   4. Seed default admin user
//   5. Optionally seed sample data for development
// =============================================================================

using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Dapper;

namespace TakeTime.Migrator;

public class Program
{
    private static Microsoft.Extensions.Logging.ILogger _logger = null!;
    private static IConfiguration _configuration = null!;

    // Service databases that get created per-tenant
    private static readonly string[] TenantServiceDatabases =
    [
        "Reservation",
        "Payment",
        "Inventory",
        "HR",
        "CRM",
        "ChannelManager",
        "GuestExperience",
        "Accounting",
        "Notification",
        "Affiliate"
    ];

    // Mapping of migration files to which database they target
    // Migrations prefixed 001 target TenantManagement, 002-006 target per-tenant service DBs, 007 seeds TenantManagement
    private static readonly Dictionary<string, string> MigrationTargetDatabase = new()
    {
        ["001_CreateTenantManagementTables.sql"] = "TenantManagement",
        ["002_CreateReservationServiceTables.sql"] = "Reservation",
        ["003_CreatePaymentServiceTables.sql"] = "Payment",
        ["004_CreateInventoryServiceTables.sql"] = "Inventory",
        ["005_CreateHRServiceTables.sql"] = "HR",
        ["006_CreateCRMServiceTables.sql"] = "CRM",
        ["007_SeedDefaultTenant.sql"] = "TenantManagement",
        ["008_CreateChannelManagerServiceTables.sql"] = "ChannelManager",
        ["009_CreateGuestExperienceServiceTables.sql"] = "GuestExperience",
        ["010_CreateAccountingServiceTables.sql"] = "Accounting",
        ["011_CreateNotificationServiceTables.sql"] = "Notification",
        ["012_CreateAffiliateServiceTables.sql"] = "Affiliate",
        ["013_CreateSubscriptionTables.sql"] = "TenantManagement"
    };

    public static async Task<int> Main(string[] args)
    {
        // Build configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("TakeTime Database Migration Runner");
            _logger.LogInformation("========================================");

            var masterConnectionString = GetMasterConnectionString();
            var tenantManagementConnectionString = _configuration.GetConnectionString("TenantManagement")
                ?? throw new InvalidOperationException("ConnectionStrings:TenantManagement is required.");

            // Step 1: Ensure TenantManagement database exists
            _logger.LogInformation("Step 1: Creating TenantManagement database if not exists...");
            await EnsureDatabaseExistsAsync(masterConnectionString, "TakeTime_TenantManagement");

            // Step 2: Run TenantManagement migrations (001, 007)
            _logger.LogInformation("Step 2: Running TenantManagement migrations...");
            await RunMigrationsAsync(tenantManagementConnectionString, "TenantManagement");

            // Step 3: Seed default admin user
            _logger.LogInformation("Step 3: Seeding default admin user...");
            await SeedDefaultAdminUserAsync(tenantManagementConnectionString);

            // Step 4: Create per-tenant databases and run service migrations
            _logger.LogInformation("Step 4: Creating per-tenant service databases...");
            await CreatePerTenantDatabasesAsync(masterConnectionString, tenantManagementConnectionString);

            // Step 5: Optionally seed sample data
            var seedSampleData = _configuration.GetValue<bool>("Migrator:SeedSampleData", false);
            if (seedSampleData)
            {
                _logger.LogInformation("Step 5: Seeding sample data for development...");
                await SeedSampleDataAsync(masterConnectionString, tenantManagementConnectionString);
            }
            else
            {
                _logger.LogInformation("Step 5: Skipping sample data seeding (Migrator:SeedSampleData = false).");
            }

            _logger.LogInformation("========================================");
            _logger.LogInformation("Migration completed successfully!");
            _logger.LogInformation("========================================");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Migration failed with error: {Message}", ex.Message);
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Derives the master connection string (pointing to 'master' database) from the TenantManagement connection string.
    /// </summary>
    private static string GetMasterConnectionString()
    {
        var tenantMgmtCs = _configuration.GetConnectionString("TenantManagement")
            ?? throw new InvalidOperationException("ConnectionStrings:TenantManagement is required.");

        var builder = new SqlConnectionStringBuilder(tenantMgmtCs)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Creates a SQL Server database if it does not already exist.
    /// </summary>
    private static async Task EnsureDatabaseExistsAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @name",
            new { name = databaseName });

        if (exists == 0)
        {
            _logger.LogInformation("  Creating database: {Database}", databaseName);
            // Dapper does not parameterize DDL, so we use string interpolation carefully
            await connection.ExecuteAsync($"CREATE DATABASE [{databaseName}]");
            _logger.LogInformation("  Database created: {Database}", databaseName);

            // Brief pause to let SQL Server finish initialization
            await Task.Delay(2000);
        }
        else
        {
            _logger.LogInformation("  Database already exists: {Database}", databaseName);
        }
    }

    /// <summary>
    /// Runs all migrations targeting the specified database category.
    /// Uses a __MigrationHistory table to track which migrations have been applied.
    /// </summary>
    private static async Task RunMigrationsAsync(string connectionString, string databaseCategory)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Ensure migration history table exists
        await connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__MigrationHistory')
            BEGIN
                CREATE TABLE __MigrationHistory (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    MigrationName NVARCHAR(500) NOT NULL,
                    AppliedOn DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    Checksum NVARCHAR(64) NULL,
                    CONSTRAINT UQ_MigrationHistory_Name UNIQUE (MigrationName)
                );
            END");

        // Get list of migration files for this database category
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        var migrationFiles = GetMigrationFiles(migrationsDir, databaseCategory);

        foreach (var (fileName, filePath) in migrationFiles)
        {
            var alreadyApplied = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM __MigrationHistory WHERE MigrationName = @name",
                new { name = fileName });

            if (alreadyApplied > 0)
            {
                _logger.LogInformation("  [SKIP] {Migration} (already applied)", fileName);
                continue;
            }

            _logger.LogInformation("  [APPLY] {Migration}...", fileName);
            var sql = await File.ReadAllTextAsync(filePath);
            var checksum = ComputeChecksum(sql);

            // Split on GO statements for batch execution
            var batches = SplitSqlBatches(sql);
            foreach (var batch in batches)
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await connection.ExecuteAsync(batch, commandTimeout: 120);
                }
            }

            await connection.ExecuteAsync(
                "INSERT INTO __MigrationHistory (MigrationName, Checksum) VALUES (@name, @checksum)",
                new { name = fileName, checksum });

            _logger.LogInformation("  [DONE] {Migration}", fileName);
        }
    }

    /// <summary>
    /// Gets migration files matching the given database category, sorted by filename (prefix number).
    /// </summary>
    private static List<(string FileName, string FilePath)> GetMigrationFiles(string migrationsDir, string databaseCategory)
    {
        var result = new List<(string, string)>();

        if (!Directory.Exists(migrationsDir))
        {
            _logger.LogWarning("  Migrations directory not found: {Dir}", migrationsDir);
            return result;
        }

        var files = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            if (MigrationTargetDatabase.TryGetValue(fileName, out var target) && target == databaseCategory)
            {
                result.Add((fileName, filePath));
            }
        }

        return result;
    }

    /// <summary>
    /// Seeds the default admin user in the TenantManagement database.
    /// </summary>
    private static async Task SeedDefaultAdminUserAsync(string tenantManagementConnectionString)
    {
        var email = _configuration.GetValue<string>("Migrator:DefaultAdminEmail", "admin@taketime.app")!;
        var password = _configuration.GetValue<string>("Migrator:DefaultAdminPassword", "Admin@TakeTime123!")!;

        await using var connection = new SqlConnection(tenantManagementConnectionString);
        await connection.OpenAsync();

        // Check if Users table exists (created by migration 001)
        var tableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users'");

        if (tableExists == 0)
        {
            _logger.LogWarning("  Users table does not exist yet. Skipping admin user seed.");
            return;
        }

        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Users WHERE Email = @email",
            new { email });

        if (exists > 0)
        {
            _logger.LogInformation("  Default admin user already exists: {Email}", email);
            return;
        }

        // Hash password with PBKDF2
        var (hash, salt) = HashPassword(password);

        await connection.ExecuteAsync(@"
            INSERT INTO Users (Id, Email, PasswordHash, PasswordSalt, FirstName, LastName,
                               DisplayName, Role, IsActive, EmailConfirmed, CreatedAt, CreatedBy)
            VALUES (NEWID(), @Email, @PasswordHash, @PasswordSalt, N'System', N'Administrator',
                    N'System Admin', 'SuperAdmin', 1, 1, SYSUTCDATETIME(), 'Migrator')",
            new
            {
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt
            });

        _logger.LogInformation("  Default admin user created: {Email}", email);
    }

    /// <summary>
    /// Creates per-tenant service databases for all registered tenants.
    /// Reads tenant list from TenantManagement.Tenants, creates a database
    /// for each service per tenant, and runs the corresponding migration.
    /// </summary>
    private static async Task CreatePerTenantDatabasesAsync(string masterConnectionString, string tenantManagementConnectionString)
    {
        await using var tmConnection = new SqlConnection(tenantManagementConnectionString);
        await tmConnection.OpenAsync();

        // Check if Tenants table exists
        var tableExists = await tmConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Tenants'");

        if (tableExists == 0)
        {
            _logger.LogWarning("  Tenants table does not exist yet. Skipping per-tenant database creation.");
            return;
        }

        var tenants = (await tmConnection.QueryAsync<(Guid Id, string Identifier)>(
            "SELECT Id, Identifier FROM Tenants WHERE IsActive = 1"))
            .ToList();

        if (tenants.Count == 0)
        {
            _logger.LogInformation("  No active tenants found. Skipping per-tenant database creation.");
            return;
        }

        _logger.LogInformation("  Found {Count} active tenant(s). Creating service databases...", tenants.Count);

        foreach (var tenant in tenants)
        {
            _logger.LogInformation("  Processing tenant: {Identifier} ({Id})", tenant.Identifier, tenant.Id);

            foreach (var service in TenantServiceDatabases)
            {
                var dbName = $"TakeTime_{service}_{tenant.Identifier}";
                await EnsureDatabaseExistsAsync(masterConnectionString, dbName);

                // Build connection string for this tenant's service database
                var builder = new SqlConnectionStringBuilder(masterConnectionString)
                {
                    InitialCatalog = dbName
                };
                var serviceCs = builder.ConnectionString;

                // Run service-specific migrations
                await RunMigrationsAsync(serviceCs, service);
            }
        }
    }

    /// <summary>
    /// Seeds sample/development data by running scripts from the Scripts directory.
    /// </summary>
    private static async Task SeedSampleDataAsync(string masterConnectionString, string tenantManagementConnectionString)
    {
        var scriptsDir = Path.Combine(AppContext.BaseDirectory, "Scripts");
        if (!Directory.Exists(scriptsDir))
        {
            _logger.LogInformation("  No Scripts directory found. Skipping sample data.");
            return;
        }

        var scripts = Directory.GetFiles(scriptsDir, "*.sql").OrderBy(f => f).ToList();

        foreach (var scriptPath in scripts)
        {
            var fileName = Path.GetFileName(scriptPath);
            _logger.LogInformation("  Running seed script: {Script}", fileName);

            var sql = await File.ReadAllTextAsync(scriptPath);

            // Determine target: scripts with "tenant_management" in name go to TenantManagement DB,
            // otherwise run against TenantManagement by default
            var connectionString = tenantManagementConnectionString;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var batches = SplitSqlBatches(sql);
            foreach (var batch in batches)
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    try
                    {
                        await connection.ExecuteAsync(batch, commandTimeout: 120);
                    }
                    catch (SqlException ex)
                    {
                        // For seed scripts, log and continue (idempotent seeding may hit duplicates)
                        _logger.LogWarning("  Seed script warning in {Script}: {Message}", fileName, ex.Message);
                    }
                }
            }

            _logger.LogInformation("  Completed seed script: {Script}", fileName);
        }
    }

    /// <summary>
    /// Splits a SQL script on GO batch separators.
    /// </summary>
    private static List<string> SplitSqlBatches(string sql)
    {
        var batches = new List<string>();
        var lines = sql.Split('\n');
        var currentBatch = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("GO;", StringComparison.OrdinalIgnoreCase))
            {
                var batch = currentBatch.ToString().Trim();
                if (!string.IsNullOrEmpty(batch))
                {
                    batches.Add(batch);
                }
                currentBatch.Clear();
            }
            else
            {
                currentBatch.AppendLine(line);
            }
        }

        // Add final batch if any
        var lastBatch = currentBatch.ToString().Trim();
        if (!string.IsNullOrEmpty(lastBatch))
        {
            batches.Add(lastBatch);
        }

        return batches;
    }

    /// <summary>
    /// Computes a SHA-256 checksum for migration integrity verification.
    /// </summary>
    private static string ComputeChecksum(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Hashes a password using PBKDF2 with a random salt.
    /// </summary>
    private static (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var salt = Convert.ToBase64String(saltBytes);

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        var hash = Convert.ToBase64String(hashBytes);
        return (hash, salt);
    }
}
