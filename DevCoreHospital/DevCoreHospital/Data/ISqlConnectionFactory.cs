using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Data;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}