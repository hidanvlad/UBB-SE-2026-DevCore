using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.Data
{
    public class DatabaseManager
    {
        public string ConnectionString { get; set; }

        public DatabaseManager(string connectionString)
        {
            this.ConnectionString = connectionString;
        }


        // STAFF CRUD
        public List<IStaff> GetStaff()
        {
            List<IStaff> staffList = new List<IStaff>();
            try
            {
                using var connection = GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT staff_id, role, first_name, last_name, contact_info, 
                           is_available, license_number, specialization, status, certification 
                    FROM Staff";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string role = reader.GetString(1);
                    string firstName = reader.GetString(2);
                    string lastName = reader.GetString(3);
                    string contactInfo = reader.IsDBNull(4) ? "" : reader.GetString(4);
                    bool isAvailable = reader.GetBoolean(5);
                    string license = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    string special = reader.IsDBNull(7) ? "" : reader.GetString(7);
                    string statusStr = reader.IsDBNull(8) ? "Available" : reader.GetString(8);
                    string cert = reader.IsDBNull(9) ? "" : reader.GetString(9);

                    Enum.TryParse<DoctorStatus>(statusStr, true, out DoctorStatus docStatus);

                    if (role == "Doctor")
                        staffList.Add(new Doctor(id, firstName, lastName, contactInfo, isAvailable, special, license, docStatus));
                    else if (role == "Pharmacist")
                        staffList.Add(new Pharmacyst(id, firstName, lastName, contactInfo, isAvailable, cert));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare GetStaff: {ex.Message}"); }
            return staffList;
        }

        public void SaveStaff(List<IStaff> staffList)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                foreach (var staff in staffList)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE Staff SET 
                            first_name = @FirstName, 
                            last_name = @LastName, 
                            contact_info = @ContactInfo, 
                            is_available = @IsAvailable, 
                            license_number = @License, 
                            specialization = @Specialization, 
                            status = @Status, 
                            certification = @Certification
                        WHERE staff_id = @Id";
                    AddParameter(cmd, "@FirstName", staff.FirstName);
                    AddParameter(cmd, "@LastName", staff.LastName);
                    AddParameter(cmd, "@ContactInfo", staff.ContactInfo);
                    AddParameter(cmd, "@IsAvailable", staff.Available);
                    AddParameter(cmd, "@License", staff is Doctor doc ? doc.LicenseNumber : DBNull.Value);
                    AddParameter(cmd, "@Specialization", staff is Doctor doc2 ? doc2.Specialization : DBNull.Value);
                    AddParameter(cmd, "@Status", staff is Doctor doc3 ? doc3.DoctorStatus.ToString() : DBNull.Value);
                    AddParameter(cmd, "@Certification", staff is Pharmacyst ph ? ph.Certification : DBNull.Value);
                    AddParameter(cmd, "@Id", staff.StaffID);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare SaveStaff: {ex.Message}");
            }
        }


        public void UpdateStaff(IStaff staff)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                                UPDATE Staff SET 
                                    first_name = @FirstName, 
                                    last_name = @LastName, 
                                    contact_info = @ContactInfo, 
                                    is_available = @IsAvailable, 
                                    license_number = @License, 
                                    specialization = @Specialization, 
                                    status = @Status, 
                                    certification = @Certification
                                WHERE staff_id = @Id";
                AddParameter(cmd, "@FirstName", staff.FirstName);
                AddParameter(cmd, "@LastName", staff.LastName);
                AddParameter(cmd, "@ContactInfo", staff.ContactInfo);
                AddParameter(cmd, "@IsAvailable", staff.Available);
                AddParameter(cmd, "@License", staff is Doctor doc ? doc.LicenseNumber : DBNull.Value);
                AddParameter(cmd, "@Specialization", staff is Doctor doc2 ? doc2.Specialization : DBNull.Value);
                AddParameter(cmd, "@Status", staff is Doctor doc3 ? doc3.DoctorStatus.ToString() : DBNull.Value);
                AddParameter(cmd, "@Certification", staff is Pharmacyst ph ? ph.Certification : DBNull.Value);
                AddParameter(cmd, "@Id", staff.StaffID);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare la actualizarea personalului: {ex.Message}");
            }
        }


        // SHIFTS CRUD
        public List<Shift> GetShifts()
        {
            List<Shift> shiftList = new List<Shift>();
            var allStaff = GetStaff();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT shift_id, staff_id, location, start_time, end_time, status FROM Shifts";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int shiftId = reader.GetInt32(0);
                    int staffId = reader.GetInt32(1);
                    string location = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    DateTime startTime = reader.GetDateTime(3);
                    DateTime endTime = reader.GetDateTime(4);
                    string statusStr = reader.IsDBNull(5) ? "Scheduled" : reader.GetString(5);

                    Enum.TryParse<ShiftStatus>(statusStr, true, out ShiftStatus shiftStatus);
                    var appointedStaff = allStaff.FirstOrDefault(s => s.StaffID == staffId);

                    if (appointedStaff != null)
                        shiftList.Add(new Shift(shiftId, appointedStaff, location, startTime, endTime, shiftStatus));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare GetShifts: {ex.Message}"); }
            return shiftList;
        }

        public bool AddShift(Shift shift)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Shifts (staff_id, location, start_time, end_time, status)
                    VALUES (@staff_id, @location, @start_time, @end_time, @status)";

                var pStaffId = cmd.CreateParameter();
                pStaffId.ParameterName = "@staff_id";
                pStaffId.Value = shift.AppointedStaff.StaffID;
                cmd.Parameters.Add(pStaffId);

                var pLocation = cmd.CreateParameter();
                pLocation.ParameterName = "@location";
                pLocation.Value = shift.Location ?? string.Empty;
                cmd.Parameters.Add(pLocation);

                var pStart = cmd.CreateParameter();
                pStart.ParameterName = "@start_time";
                pStart.Value = shift.StartTime;
                cmd.Parameters.Add(pStart);

                var pEnd = cmd.CreateParameter();
                pEnd.ParameterName = "@end_time";
                pEnd.Value = shift.EndTime;
                cmd.Parameters.Add(pEnd);

                var pStatus = cmd.CreateParameter();
                pStatus.ParameterName = "@status";
                pStatus.Value = shift.Status.ToString();
                cmd.Parameters.Add(pStatus);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare la AddShift: {ex.Message}");
                return false;
            }
        }


        public void AddNewShift(Shift newShift)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
            INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active) 
            VALUES (@StaffId, @Location, @StartTime, @EndTime, @Status, @IsActive)";

                AddParameter(cmd, "@StaffId", newShift.AppointedStaff.StaffID);
                AddParameter(cmd, "@Location", newShift.Location);
                AddParameter(cmd, "@StartTime", newShift.StartTime);
                AddParameter(cmd, "@EndTime", newShift.EndTime);
                AddParameter(cmd, "@Status", newShift.Status.ToString());
                AddParameter(cmd, "@IsActive", true);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare la crearea turei noi: {ex.Message}");
            }
        }

        public void UpdateShift(Shift shift)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Shifts SET 
                        staff_id = @StaffId, 
                        location = @Location, 
                        start_time = @StartTime, 
                        end_time = @EndTime, 
                        status = @Status
                    WHERE shift_id = @Id";
                AddParameter(cmd, "@StaffId", shift.AppointedStaff.StaffID);
                AddParameter(cmd, "@Location", shift.Location);
                AddParameter(cmd, "@StartTime", shift.StartTime);
                AddParameter(cmd, "@EndTime", shift.EndTime);
                AddParameter(cmd, "@Status", shift.Status.ToString());
                AddParameter(cmd, "@Id", shift.Id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare la actualizarea turei: {ex.Message}");
            }
        }

        public void DeleteShift(int shiftId)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Shifts WHERE shift_id = @Id";
                AddParameter(cmd, "@Id", shiftId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare la stergerea turei: {ex.Message}");
            }
        }

        public void SaveShifts(List<Shift> shiftList)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                foreach (var shift in shiftList)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE Shifts SET 
                            staff_id = @StaffId, 
                            location = @Location, 
                            start_time = @StartTime, 
                            end_time = @EndTime, 
                            status = @Status
                        WHERE shift_id = @Id";
                    AddParameter(cmd, "@StaffId", shift.AppointedStaff.StaffID);
                    AddParameter(cmd, "@Location", shift.Location);
                    AddParameter(cmd, "@StartTime", shift.StartTime);
                    AddParameter(cmd, "@EndTime", shift.EndTime);
                    AddParameter(cmd, "@Status", shift.Status.ToString());
                    AddParameter(cmd, "@Id", shift.Id);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare SaveShifts: {ex.Message}");
            }
        }


        // MEDICINES
        public int GetMedicinesSold(int pharmacistStaffId, int month, int year)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM PharmacyHandover
                    WHERE PharmacistID = @staffId AND MONTH(HandoverDate) = @month AND YEAR(HandoverDate) = @year";

                AddParameter(command, "@staffId", pharmacistStaffId);
                AddParameter(command, "@month", month);
                AddParameter(command, "@year", year);

                var result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch { return 150; }
        }


        // APPOINTMENTS CRUD
        public async Task<List<Appointment>> GetUpcomingAppointmentsAsync(int doctorId, DateTime fromDate, DateTime toDate, int skip, int take)
        {
            var items = new List<Appointment>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT a.appointment_id, a.doctor_id, s.first_name + ' ' + s.last_name AS DoctorName, 
                       a.patient_id, a.start_time, a.end_time, a.status
                FROM Appointments a
                INNER JOIN Staff s ON s.staff_id = a.doctor_id
                WHERE a.doctor_id = @DocId AND a.start_time >= @From AND a.start_time < @To
                ORDER BY a.start_time 
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            AddParameter(cmd, "@DocId", doctorId);
            AddParameter(cmd, "@From", fromDate);
            AddParameter(cmd, "@To", toDate);
            AddParameter(cmd, "@Skip", skip);
            AddParameter(cmd, "@Take", take);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) items.Add(MapReaderToAppointment(reader, true));
            return items;
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT appointment_id, doctor_id, patient_id, start_time, end_time, status FROM Appointments WHERE appointment_id = @Id;";
            AddParameter(cmd, "@Id", appointmentId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapReaderToAppointment(reader, false);
            return null;
        }

        public async Task<List<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var items = new List<Appointment>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT appointment_id, doctor_id, patient_id, start_time, end_time, status FROM Appointments WHERE doctor_id = @DocId ORDER BY start_time;";
            AddParameter(cmd, "@DocId", doctorId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) items.Add(MapReaderToAppointment(reader, false));
            return items;
        }

        public async Task AddAppointmentAsync(int patientId, int doctorId, DateTime startTime, DateTime endTime)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status) VALUES (@PatId, @DocId, @Start, @End, 'Scheduled');";

            AddParameter(cmd, "@PatId", patientId);
            AddParameter(cmd, "@DocId", doctorId);
            AddParameter(cmd, "@Start", startTime);
            AddParameter(cmd, "@End", endTime);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAppointmentStatusAsync(int appointmentId, string status)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Appointments SET status = @Status WHERE appointment_id = @Id;";
            AddParameter(cmd, "@Status", status); AddParameter(cmd, "@Id", appointmentId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetActiveAppointmentsCountAsync(int doctorId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Appointments WHERE doctor_id = @DocId AND status = 'Scheduled';";
            AddParameter(cmd, "@DocId", doctorId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }


        // DOCTOR
        public async Task UpdateDoctorStatusAsync(int doctorId, string status)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Staff SET status = @Status WHERE staff_id = @DocId AND role = 'Doctor';";
            AddParameter(cmd, "@Status", status); AddParameter(cmd, "@DocId", doctorId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var items = new List<(int, string)>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT staff_id, first_name + ' ' + last_name FROM Staff WHERE role = 'Doctor' ORDER BY first_name;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add((reader.GetInt32(0), reader.GetString(1)));

            return items;
        }


        // UTILS
        internal DbConnection GetConnection()
        {
            var connectionFactory = new SqlConnectionFactory(ConnectionString);
            return connectionFactory.Create();
        }

        private void AddParameter(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private Appointment MapReaderToAppointment(DbDataReader reader, bool hasDoctorName)
        {
            DateTime startDt = reader.GetDateTime(reader.GetOrdinal("start_time"));
            DateTime endDt = reader.GetDateTime(reader.GetOrdinal("end_time"));
            int patId = reader.GetInt32(reader.GetOrdinal("patient_id"));
            int statusOrdinal = reader.GetOrdinal("status");

            return new Appointment
            {
                Id = reader.GetInt32(reader.GetOrdinal("appointment_id")),
                DoctorId = reader.GetInt32(reader.GetOrdinal("doctor_id")),
                DoctorName = hasDoctorName ? reader.GetString(reader.GetOrdinal("DoctorName")) : "",
                PatientName = "PAT-" + patId,
                Date = startDt.Date,
                StartTime = startDt.TimeOfDay,
                EndTime = endDt.TimeOfDay,
                Status = reader.IsDBNull(statusOrdinal) ? "Scheduled" : reader.GetString(statusOrdinal),
                Type = "",
                Location = ""
            };
        }
    }
}