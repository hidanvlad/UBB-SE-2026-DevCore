using System.Collections.Generic;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class PharmacyHandoverRepository : IPharmacyHandoverRepository
    {
        private readonly string connectionString;

        public PharmacyHandoverRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IReadOnlyList<PharmacyHandover> GetAllPharmacyHandovers()
        {
            var handovers = new List<PharmacyHandover>();

            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT PharmacistID, HandoverDate FROM PharmacyHandover;",
                connection);

            using SqlDataReader reader = command.ExecuteReader();
            int pharmacistOrdinal = reader.GetOrdinal("PharmacistID");
            int dateOrdinal = reader.GetOrdinal("HandoverDate");
            while (reader.Read())
            {
                handovers.Add(new PharmacyHandover
                {
                    PharmacistId = reader.GetInt32(pharmacistOrdinal),
                    HandoverDate = reader.GetDateTime(dateOrdinal),
                });
            }
            return handovers;
        }
    }
}
