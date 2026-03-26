namespace DevCoreHospital.Data;

/// <summary>
/// Database access coordinator for repositories. In-memory seeds use this for future SQL wiring.
/// </summary>
public sealed class DatabaseManager
{
    public ISqlConnectionFactory ConnectionFactory { get; }

    public DatabaseManager(ISqlConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory;
    }
}
