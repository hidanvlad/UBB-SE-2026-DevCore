using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Tests.Repositories;

public class SqlTestFixture : IDisposable
{
    public string ConnectionString { get; }

    public SqlTestFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        ConnectionString = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("TestDatabase")
            .GetString()!;
    }

    public SqlConnection OpenConnection()
    {
        var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public int InsertStaff(SqlConnection conn, string role, string firstName, string lastName,
        string specialization = "", string status = "Available", bool isAvailable = true,
        string certification = "", int yearsExp = 1)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO Staff ([role], department, first_name, last_name, contact_info, is_available,
                specialization, [status], license_number, years_of_experience, certification)
            OUTPUT INSERTED.staff_id
            VALUES (@Role, @Dept, @First, @Last, @Contact, @Avail, @Spec, @Status, @Lic, @Exp, @Cert)", conn);
        cmd.Parameters.AddWithValue("@Role", role);
        cmd.Parameters.AddWithValue("@Dept", role == "Doctor" ? "Test Dept" : "Pharmacy");
        cmd.Parameters.AddWithValue("@First", firstName);
        cmd.Parameters.AddWithValue("@Last", lastName);
        cmd.Parameters.AddWithValue("@Contact", $"{firstName.ToLower()}@test.local");
        cmd.Parameters.AddWithValue("@Avail", isAvailable);
        cmd.Parameters.AddWithValue("@Spec", string.IsNullOrEmpty(specialization) ? DBNull.Value : (object)specialization);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@Lic", $"LIC-{Guid.NewGuid():N}");
        cmd.Parameters.AddWithValue("@Exp", yearsExp);
        cmd.Parameters.AddWithValue("@Cert", string.IsNullOrEmpty(certification) ? DBNull.Value : (object)certification);
        return (int)cmd.ExecuteScalar()!;
    }

    public int InsertShift(SqlConnection conn, int staffId, string location, DateTime start, DateTime end, string status = "SCHEDULED")
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO Shifts (staff_id, [location], start_time, end_time, [status], is_active)
            OUTPUT INSERTED.shift_id
            VALUES (@StaffId, @Location, @Start, @End, @Status, 1)", conn);
        cmd.Parameters.AddWithValue("@StaffId", staffId);
        cmd.Parameters.AddWithValue("@Location", location);
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);
        cmd.Parameters.AddWithValue("@Status", status);
        return (int)cmd.ExecuteScalar()!;
    }

    public int InsertAppointment(SqlConnection conn, int patientId, int doctorId, DateTime start, DateTime end, string status = "Scheduled")
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, [status])
            OUTPUT INSERTED.appointment_id
            VALUES (@PatId, @DocId, @Start, @End, @Status)", conn);
        cmd.Parameters.AddWithValue("@PatId", patientId);
        cmd.Parameters.AddWithValue("@DocId", doctorId);
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);
        cmd.Parameters.AddWithValue("@Status", status);
        return (int)cmd.ExecuteScalar()!;
    }

    public int InsertHangout(SqlConnection conn, string title, DateTime date)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO Hangouts (title, description, date_time, max_staff)
            OUTPUT INSERTED.hangout_id
            VALUES (@Title, @Desc, @Date, @Max)", conn);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Desc", "Test hangout");
        cmd.Parameters.AddWithValue("@Date", date);
        cmd.Parameters.AddWithValue("@Max", 10);
        return (int)cmd.ExecuteScalar()!;
    }

    public void InsertHangoutParticipant(SqlConnection conn, int hangoutId, int staffId)
    {
        using var cmd = new SqlCommand("INSERT INTO Hangout_Participants (hangout_id, staff_id) VALUES (@HId, @SId)", conn);
        cmd.Parameters.AddWithValue("@HId", hangoutId);
        cmd.Parameters.AddWithValue("@SId", staffId);
        cmd.ExecuteNonQuery();
    }

    public int CountNotificationsForStaff(SqlConnection conn, int staffId, string title)
    {
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM Notifications WHERE recipient_staff_id = @Id AND title = @Title", conn);
        cmd.Parameters.AddWithValue("@Id", staffId);
        cmd.Parameters.AddWithValue("@Title", title);
        return (int)cmd.ExecuteScalar()!;
    }

    public string? GetShiftStatus(SqlConnection conn, int shiftId)
    {
        using var cmd = new SqlCommand("SELECT [status] FROM Shifts WHERE shift_id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", shiftId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public int? GetShiftStaffId(SqlConnection conn, int shiftId)
    {
        using var cmd = new SqlCommand("SELECT staff_id FROM Shifts WHERE shift_id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", shiftId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : (int?)Convert.ToInt32(result);
    }

    public string? GetAppointmentStatus(SqlConnection conn, int appointmentId)
    {
        using var cmd = new SqlCommand("SELECT [status] FROM Appointments WHERE appointment_id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", appointmentId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public string? GetStaffStatus(SqlConnection conn, int staffId)
    {
        using var cmd = new SqlCommand("SELECT [status] FROM Staff WHERE staff_id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", staffId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public void DeleteNotificationsByStaff(SqlConnection conn, int staffId)
        => Execute(conn, "DELETE FROM Notifications WHERE recipient_staff_id = @Id", staffId);

    public void DeleteSwapRequestsByShift(SqlConnection conn, int shiftId)
        => Execute(conn, "DELETE FROM ShiftSwapRequests WHERE shift_id = @Id", shiftId);

    public void DeleteSwapRequest(SqlConnection conn, int swapId)
        => Execute(conn, "DELETE FROM ShiftSwapRequests WHERE swap_id = @Id", swapId);

    public void DeleteShift(SqlConnection conn, int shiftId)
        => Execute(conn, "DELETE FROM Shifts WHERE shift_id = @Id", shiftId);

    public void DeleteAppointment(SqlConnection conn, int appointmentId)
        => Execute(conn, "DELETE FROM Appointments WHERE appointment_id = @Id", appointmentId);

    public void DeleteAppointmentsByDoctor(SqlConnection conn, int doctorId)
        => Execute(conn, "DELETE FROM Appointments WHERE doctor_id = @Id", doctorId);

    public void DeleteHangoutParticipants(SqlConnection conn, int hangoutId)
        => Execute(conn, "DELETE FROM Hangout_Participants WHERE hangout_id = @Id", hangoutId);

    public void DeleteHangout(SqlConnection conn, int hangoutId)
        => Execute(conn, "DELETE FROM Hangouts WHERE hangout_id = @Id", hangoutId);

    public void DeleteStaff(SqlConnection conn, int staffId)
        => Execute(conn, "DELETE FROM Staff WHERE staff_id = @Id", staffId);

    private static void Execute(SqlConnection conn, string sql, int id)
    {
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() { }
}
