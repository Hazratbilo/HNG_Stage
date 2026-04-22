using Microsoft.Data.Sqlite;

namespace HNG_Stage_1.Services
{
    public class DatabaseSchemaInitializer
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public DatabaseSchemaInitializer(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db";
            var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
            var databasePath = Path.IsPathRooted(dataSource)
                ? dataSource
                : Path.Combine(_environment.ContentRootPath, dataSource);

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? _environment.ContentRootPath);

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync(cancellationToken);

            var existingTableName = await GetProfilesTableNameAsync(connection, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingTableName))
            {
                await NormalizeExistingProfilesTableAsync(connection, existingTableName, cancellationToken);
            }

            await CreateProfilesTableAsync(connection, cancellationToken);
            await CreateIndexesAsync(connection, cancellationToken);
        }

        private static async Task<string?> GetProfilesTableNameAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT name
                FROM sqlite_master
                WHERE type = 'table'
                  AND lower(name) = 'profiles'
                LIMIT 1;
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result?.ToString();
        }

        private static async Task NormalizeExistingProfilesTableAsync(
            SqliteConnection connection,
            string existingTableName,
            CancellationToken cancellationToken)
        {
            var columns = await GetColumnNamesAsync(connection, existingTableName, cancellationToken);
            var requiresRebuild = existingTableName != "profiles"
                || columns.Contains("SampleSize")
                || columns.Contains("AgeGroup")
                || columns.Contains("CountryId")
                || columns.Contains("GenderProbability")
                || columns.Contains("CountryProbability")
                || !columns.Contains("country_name")
                || !columns.Contains("age_group");

            if (!requiresRebuild)
            {
                return;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await ExecuteNonQueryAsync(connection, transaction, "DROP TABLE IF EXISTS profiles_stage2_temp;", cancellationToken);
            await ExecuteNonQueryAsync(connection, transaction, CreateProfilesTableSql("profiles_stage2_temp"), cancellationToken);

            var sourceTableSqlName = QuoteIdentifier(existingTableName);
            var copySql = $"""
                INSERT INTO profiles_stage2_temp (
                    id,
                    name,
                    gender,
                    gender_probability,
                    age,
                    age_group,
                    country_id,
                    country_name,
                    country_probability,
                    created_at
                )
                SELECT
                    {ResolveSourceColumn(columns, "id", "Id")} AS id,
                    lower(trim({ResolveSourceColumn(columns, "name", "Name")})) AS name,
                    lower(trim({ResolveSourceColumn(columns, "gender", "Gender")})) AS gender,
                    {ResolveSourceColumn(columns, "gender_probability", "GenderProbability")} AS gender_probability,
                    {ResolveSourceColumn(columns, "age", "Age")} AS age,
                    lower(trim({ResolveSourceColumn(columns, "age_group", "AgeGroup")})) AS age_group,
                    upper(trim({ResolveSourceColumn(columns, "country_id", "CountryId")})) AS country_id,
                    COALESCE({ResolveOptionalSourceColumn(columns, "country_name", "CountryName")}, upper(trim({ResolveSourceColumn(columns, "country_id", "CountryId")}))) AS country_name,
                    {ResolveSourceColumn(columns, "country_probability", "CountryProbability")} AS country_probability,
                    COALESCE({ResolveOptionalSourceColumn(columns, "created_at", "CreatedAt")}, '1970-01-01T00:00:00Z') AS created_at
                FROM {sourceTableSqlName};
                """;

            await ExecuteNonQueryAsync(connection, transaction, copySql, cancellationToken);
            await ExecuteNonQueryAsync(connection, transaction, $"DROP TABLE {sourceTableSqlName};", cancellationToken);
            await ExecuteNonQueryAsync(connection, transaction, "ALTER TABLE profiles_stage2_temp RENAME TO profiles;", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private static async Task<HashSet<string>> GetColumnNamesAsync(
            SqliteConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }

        private static async Task CreateProfilesTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = CreateProfilesTableSql("profiles");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task CreateIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            var commands = new[]
            {
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_profiles_name ON profiles(name);",
                "CREATE INDEX IF NOT EXISTS IX_profiles_gender_age_group_country_id ON profiles(gender, age_group, country_id);",
                "CREATE INDEX IF NOT EXISTS IX_profiles_age ON profiles(age);",
                "CREATE INDEX IF NOT EXISTS IX_profiles_created_at ON profiles(created_at);"
            };

            foreach (var sql in commands)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private static string CreateProfilesTableSql(string tableName) => $"""
            CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)} (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                gender TEXT NOT NULL,
                gender_probability REAL NOT NULL,
                age INTEGER NOT NULL,
                age_group TEXT NOT NULL,
                country_id TEXT NOT NULL,
                country_name TEXT NOT NULL,
                country_probability REAL NOT NULL,
                created_at TEXT NOT NULL
            );
            """;

        private static string ResolveSourceColumn(HashSet<string> columns, params string[] candidates)
        {
            var columnName = candidates.FirstOrDefault(columns.Contains);
            if (columnName == null)
            {
                throw new InvalidOperationException("Unable to map the existing profiles schema.");
            }

            return QuoteIdentifier(columnName);
        }

        private static string ResolveOptionalSourceColumn(HashSet<string> columns, params string[] candidates)
        {
            var columnName = candidates.FirstOrDefault(columns.Contains);
            return columnName == null ? "NULL" : QuoteIdentifier(columnName);
        }

        private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

        private static async Task ExecuteNonQueryAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
