using DevCoreHospital.Configuration;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string? connectionString = null)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? AppSettings.ConnectionString
            : connectionString;
    }

    public SqlConnection Create() => new SqlConnection(_connectionString);
}