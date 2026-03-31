using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
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

        // ========================= STAFF =========================
        public List<IStaff> GetStaff()
        {
            List<IStaff> staffList = new();
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
                    string statusStr = reader.IsDBNull(8) ? "AVAILABLE" : reader.GetString(8);
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare SaveStaff: {ex.Message}"); }
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare UpdateStaff: {ex.Message}"); }
        }

        // ========================= SHIFTS =========================
        public List<Shift> GetShifts()
        {
            List<Shift> shiftList = new();
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
                    string statusStr = reader.IsDBNull(5) ? "SCHEDULED" : reader.GetString(5);

                    Enum.TryParse<ShiftStatus>(statusStr, true, out ShiftStatus shiftStatus);
                    var appointedStaff = allStaff.FirstOrDefault(s => s.StaffID == staffId);

                    if (appointedStaff != null)
                        shiftList.Add(new Shift(shiftId, appointedStaff, location, startTime, endTime, shiftStatus));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare GetShifts: {ex.Message}"); }
            return shiftList;
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare AddNewShift: {ex.Message}"); }
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare UpdateShift: {ex.Message}"); }
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare DeleteShift: {ex.Message}"); }
        }

        public bool IsStaffWorkingDuring(int staffId, DateTime start, DateTime end)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*)
                    FROM Shifts
                    WHERE staff_id = @StaffId
                      AND start_time < @EndTime
                      AND end_time > @StartTime
                      AND status IN ('SCHEDULED', 'ACTIVE')";
                AddParameter(cmd, "@StaffId", staffId);
                AddParameter(cmd, "@StartTime", start);
                AddParameter(cmd, "@EndTime", end);

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare IsStaffWorkingDuring: {ex.Message}");
                return false;
            }
        }

        // ========================= SWAP REQUESTS =========================
        public int CreateShiftSwapRequest(ShiftSwapRequest request)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
                    VALUES (@ShiftId, @RequesterId, @ColleagueId, @RequestedAt, @Status);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                AddParameter(cmd, "@ShiftId", request.ShiftId);
                AddParameter(cmd, "@RequesterId", request.RequesterId);
                AddParameter(cmd, "@ColleagueId", request.ColleagueId);
                AddParameter(cmd, "@RequestedAt", request.RequestedAt);
                AddParameter(cmd, "@Status", request.Status.ToString());

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare CreateShiftSwapRequest: {ex.Message}");
                return 0;
            }
        }

        public List<ShiftSwapRequest> GetPendingSwapRequestsForColleague(int colleagueId)
        {
            var items = new List<ShiftSwapRequest>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT swap_id, shift_id, requester_id, colleague_id, requested_at, status
                    FROM ShiftSwapRequests
                    WHERE colleague_id = @ColleagueId AND status = 'PENDING'
                    ORDER BY requested_at DESC";
                AddParameter(cmd, "@ColleagueId", colleagueId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    Enum.TryParse<ShiftSwapRequestStatus>(r.GetString(5), true, out var st);
                    items.Add(new ShiftSwapRequest
                    {
                        SwapId = r.GetInt32(0),
                        ShiftId = r.GetInt32(1),
                        RequesterId = r.GetInt32(2),
                        ColleagueId = r.GetInt32(3),
                        RequestedAt = r.GetDateTime(4),
                        Status = st
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Eroare GetPendingSwapRequestsForColleague: {ex.Message}"); }
            return items;
        }

        public ShiftSwapRequest? GetShiftSwapRequestById(int swapId)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT swap_id, shift_id, requester_id, colleague_id, requested_at, status
                    FROM ShiftSwapRequests
                    WHERE swap_id = @SwapId";
                AddParameter(cmd, "@SwapId", swapId);

                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                Enum.TryParse<ShiftSwapRequestStatus>(r.GetString(5), true, out var st);
                return new ShiftSwapRequest
                {
                    SwapId = r.GetInt32(0),
                    ShiftId = r.GetInt32(1),
                    RequesterId = r.GetInt32(2),
                    ColleagueId = r.GetInt32(3),
                    RequestedAt = r.GetDateTime(4),
                    Status = st
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare GetShiftSwapRequestById: {ex.Message}");
                return null;
            }
        }

        public bool UpdateShiftSwapRequestStatus(int swapId, string status)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE ShiftSwapRequests SET status = @Status WHERE swap_id = @SwapId;";
                AddParameter(cmd, "@Status", status);
                AddParameter(cmd, "@SwapId", swapId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare UpdateShiftSwapRequestStatus: {ex.Message}");
                return false;
            }
        }

        public bool ReassignShiftToStaff(int shiftId, int newStaffId)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Shifts SET staff_id = @StaffId WHERE shift_id = @ShiftId;";
                AddParameter(cmd, "@StaffId", newStaffId);
                AddParameter(cmd, "@ShiftId", shiftId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eroare ReassignShiftToStaff: {ex.Message}");
                return false;
            }
        }

        public void AddNotification(int recipientStaffId, string title, string message)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Notifications (recipient_staff_id, title, message, created_at, is_read)
                    VALUES (@RecipientId, @Title, @Message, @CreatedAt, 0)";
                AddParameter(cmd, "@RecipientId", recipientStaffId);
                AddParameter(cmd, "@Title", title);
                AddParameter(cmd, "@Message", message);
                AddParameter(cmd, "@CreatedAt", DateTime.UtcNow);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification fallback -> To:{recipientStaffId} | {title} | {message}");
                System.Diagnostics.Debug.WriteLine($"Eroare AddNotification: {ex.Message}");
            }
        }

        // ========================= APPOINTMENTS (kept) =========================
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

        // ========================= UTILS =========================
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
            int startOrdinal = reader.GetOrdinal("start_time");
            int endOrdinal = reader.GetOrdinal("end_time");
            int patientOrdinal = reader.GetOrdinal("patient_id");
            int statusOrdinal = reader.GetOrdinal("status");
            int doctorNameOrdinal = hasDoctorName ? reader.GetOrdinal("DoctorName") : -1;

            DateTime startDt = reader.IsDBNull(startOrdinal) ? DateTime.Now : reader.GetDateTime(startOrdinal);
            DateTime endDt = reader.IsDBNull(endOrdinal) ? startDt : reader.GetDateTime(endOrdinal);
            int patId = reader.IsDBNull(patientOrdinal) ? 0 : reader.GetInt32(patientOrdinal);

            return new Appointment
            {
                Id = reader.GetInt32(reader.GetOrdinal("appointment_id")),
                DoctorId = reader.GetInt32(reader.GetOrdinal("doctor_id")),
                DoctorName = hasDoctorName && !reader.IsDBNull(doctorNameOrdinal) ? reader.GetString(doctorNameOrdinal) : "",
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