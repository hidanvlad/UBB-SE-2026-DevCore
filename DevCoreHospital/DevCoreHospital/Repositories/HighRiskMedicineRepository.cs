using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class HighRiskMedicineRepository : IHighRiskMedicineRepository
    {
        private readonly string connectionString;

        public HighRiskMedicineRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IReadOnlyList<(string MedicineName, string WarningMessage)> GetAllHighRiskMedicines()
        {
            var medicines = new List<(string, string)>();
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT medicine_name, warning_message FROM High_Risk_Medicines;",
                connection);

            using SqlDataReader reader = command.ExecuteReader();
            int medicineNameOrdinal = reader.GetOrdinal("medicine_name");
            int warningMessageOrdinal = reader.GetOrdinal("warning_message");
            while (reader.Read())
            {
                medicines.Add((
                    reader.IsDBNull(medicineNameOrdinal) ? string.Empty : reader.GetString(medicineNameOrdinal),
                    reader.IsDBNull(warningMessageOrdinal) ? string.Empty : reader.GetString(warningMessageOrdinal)));
            }
            return medicines;
        }
    }
}
