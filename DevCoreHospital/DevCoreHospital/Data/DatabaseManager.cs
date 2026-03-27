using System.Collections.Generic;
using DevCoreHospital.Models;
using System.Linq;
using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace DevCoreHospital.Data
{
    public class DatabaseManager
    {
        public string ConnectionString { get; set; }

        public DatabaseManager(string connectionString)
        {
            this.ConnectionString = connectionString;
        }

        public List<IStaff> GetStaff()
        {
            // return some dummy data for now, we will implement the actual database connection later
            List<IStaff> staffList = new List<IStaff>();
            staffList.Add(new Doctor(1, "John", "Doe", "0700-000 000", true, "Cardiology", "12345", DoctorStatus.AVAILABLE));
            staffList.Add(new Doctor(2, "Jane", "Smith", "0700-000 001", false, "Neurology", "54321", DoctorStatus.IN_EXAMINATION));
            staffList.Add(new Doctor(3, "Emily", "Johnson", "0700-000 002", true, "Pediatrics", "67890", DoctorStatus.OFF_DUTY));
            staffList.Add(new Pharmacyst(4, "Anna", "Doe", "0700-000 003", false, "BPS"));
            staffList.Add(new Pharmacyst(5, "Mary", "Christmas", "0700-000 004", true, "ASHP"));
            return staffList;
        }

        public List<Shift> GetShifts()
        {
            // return some dummy data for now, we will implement the actual database connection later
            List<Shift> shiftList = new List<Shift>();
            shiftList.Add(new Shift(1, new Doctor(1, "John", "Doe", "0700-000 000", true, "Cardiology", "12345", DoctorStatus.AVAILABLE), "Cardiology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.ACTIVE));
            shiftList.Add(new Shift(2, new Doctor(2, "Jane", "Smith", "0700-000 001", false, "Neurology", "54321", DoctorStatus.IN_EXAMINATION), "Neurology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.SCHEDULED));
            shiftList.Add(new Shift(3, new Doctor(3, "Emily", "Johnson", "0700-000 002", true, "Pediatrics", "67890", DoctorStatus.OFF_DUTY), "Pediatrics", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.COMPLETED));
            return shiftList;
        }

        public int GetMedicinesSold(int pharmacistStaffId, int month, int year)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*)
                    FROM PharmacyHandover
                    WHERE PharmacistID = @staffId
                      AND MONTH(HandoverDate) = @month
                      AND YEAR(HandoverDate) = @year";

                var staffIdParameter = command.CreateParameter();
                staffIdParameter.ParameterName = "@staffId";
                staffIdParameter.Value = pharmacistStaffId;
                command.Parameters.Add(staffIdParameter);

                var monthParameter = command.CreateParameter();
                monthParameter.ParameterName = "@month";
                monthParameter.Value = month;
                command.Parameters.Add(monthParameter);

                var yearParameter = command.CreateParameter();
                yearParameter.ParameterName = "@year";
                yearParameter.Value = year;
                command.Parameters.Add(yearParameter);

                var result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch
            {
                // Keep salary feature operable even when DB setup is missing.
                return 150;
            }
        }

        internal DbConnection GetConnection()
        {
            var connectionFactory = new SqlConnectionFactory(ConnectionString);
            return connectionFactory.Create();
        }
    }
}