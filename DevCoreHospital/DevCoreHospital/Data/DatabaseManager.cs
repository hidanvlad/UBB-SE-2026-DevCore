using System.Collections.Generic;
using DevCoreHospital.Models;
using System.Linq;
using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Data
{
    public class DatabaseManager
    {
        public string connectionString { get; set; }

        public List<Staff> GetStaff()
        {
            var staffList = new List<Staff>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT Id, Name, Position FROM Staff", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var staff = new Staff
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                Name = reader.GetString(reader.GetOrdinal("Name")),
                                Position = reader.GetString(reader.GetOrdinal("Position"))
                            };
                            staffList.Add(staff);
                        }
                    }
                }
            }

            return staffList;
        }
    }
}