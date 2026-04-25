using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Tests.Repositories;

public class SqlTestFixture : IDisposable
{
    public string ConnectionString { get; }

    public SqlTestFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var configuredConnectionString = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("TestDatabase")
            .GetString()!;

        ConnectionString = ResolveConnectionString(configuredConnectionString);
    }

    private static string ResolveConnectionString(string configuredConnectionString)
    {
        var configuredBuilder = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            ConnectTimeout = 3
        };

        var candidates = new List<string>
        {
            configuredBuilder.ConnectionString
        };

        if (!string.Equals(configuredBuilder.InitialCatalog, "DevCoreHospital", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackBuilder = new SqlConnectionStringBuilder(configuredBuilder.ConnectionString)
            {
                InitialCatalog = "DevCoreHospital"
            };
            candidates.Add(fallbackBuilder.ConnectionString);
        }

        AddFallback(candidates, configuredBuilder, "localhost\\SQLEXPRESS", "HospitalDatabase");
        AddFallback(candidates, configuredBuilder, "localhost\\SQLEXPRESS", "DevCoreHospital");
        AddFallback(candidates, configuredBuilder, ".\\SQLEXPRESS", "HospitalDatabase");
        AddFallback(candidates, configuredBuilder, ".\\SQLEXPRESS", "DevCoreHospital");

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (CanOpenConnection(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Unable to open any configured SQL test connection. Checked: " + string.Join(" | ", candidates));
    }

    private static bool CanOpenConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddFallback(List<string> candidates, SqlConnectionStringBuilder template, string dataSource, string database)
    {
        var builder = new SqlConnectionStringBuilder(template.ConnectionString)
        {
            DataSource = dataSource,
            InitialCatalog = database
        };
        candidates.Add(builder.ConnectionString);
    }

    public SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public int InsertStaff(SqlConnection connection, string role, string firstName, string lastName,
        string specialization = "", string status = "Available", bool isAvailable = true,
        string certification = "", int yearsExp = 1)
    {
        using var command = new SqlCommand(@"
            INSERT INTO Staff ([role], department, first_name, last_name, contact_info, is_available,
                specialization, [status], license_number, years_of_experience, certification)
            OUTPUT INSERTED.staff_id
            VALUES (@Role, @Dept, @First, @Last, @Contact, @Avail, @Spec, @Status, @Lic, @Exp, @Cert)", connection);
        command.Parameters.AddWithValue("@Role", role);
        command.Parameters.AddWithValue("@Dept", role == "Doctor" ? "Test Dept" : "Pharmacy");
        command.Parameters.AddWithValue("@First", firstName);
        command.Parameters.AddWithValue("@Last", lastName);
        command.Parameters.AddWithValue("@Contact", $"{firstName.ToLower()}@test.local");
        command.Parameters.AddWithValue("@Avail", isAvailable);
        command.Parameters.AddWithValue("@Spec", string.IsNullOrEmpty(specialization) ? DBNull.Value : (object)specialization);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@Lic", $"LIC-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("@Exp", yearsExp);
        command.Parameters.AddWithValue("@Cert", string.IsNullOrEmpty(certification) ? DBNull.Value : (object)certification);
        return (int)command.ExecuteScalar()!;
    }

    public int InsertShift(SqlConnection connection, int staffId, string location, DateTime start, DateTime end, string status = "SCHEDULED")
    {
        using var command = new SqlCommand(@"
            INSERT INTO Shifts (staff_id, [location], start_time, end_time, [status], is_active)
            OUTPUT INSERTED.shift_id
            VALUES (@StaffId, @Location, @Start, @End, @Status, 1)", connection);
        command.Parameters.AddWithValue("@StaffId", staffId);
        command.Parameters.AddWithValue("@Location", location);
        command.Parameters.AddWithValue("@Start", start);
        command.Parameters.AddWithValue("@End", end);
        command.Parameters.AddWithValue("@Status", status);
        return (int)command.ExecuteScalar()!;
    }

    public int InsertAppointment(SqlConnection connection, int patientId, int doctorId, DateTime start, DateTime end, string status = "Scheduled")
    {
        using var command = new SqlCommand(@"
            INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, [status])
            OUTPUT INSERTED.appointment_id
            VALUES (@PatId, @DocId, @Start, @End, @Status)", connection);
        command.Parameters.AddWithValue("@PatId", patientId);
        command.Parameters.AddWithValue("@DocId", doctorId);
        command.Parameters.AddWithValue("@Start", start);
        command.Parameters.AddWithValue("@End", end);
        command.Parameters.AddWithValue("@Status", status);
        return (int)command.ExecuteScalar()!;
    }

    public int InsertHangout(SqlConnection connection, string title, DateTime date)
    {
        using var command = new SqlCommand(@"
            INSERT INTO Hangouts (title, description, date_time, max_staff)
            OUTPUT INSERTED.hangout_id
            VALUES (@Title, @Desc, @Date, @Max)", connection);
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Desc", "Test hangout");
        command.Parameters.AddWithValue("@Date", date);
        command.Parameters.AddWithValue("@Max", 10);
        return (int)command.ExecuteScalar()!;
    }

    public void InsertHangoutParticipant(SqlConnection connection, int hangoutId, int staffId)
    {
        using var command = new SqlCommand("INSERT INTO Hangout_Participants (hangout_id, staff_id) VALUES (@HId, @SId)", connection);
        command.Parameters.AddWithValue("@HId", hangoutId);
        command.Parameters.AddWithValue("@SId", staffId);
        command.ExecuteNonQuery();
    }

    public int CountNotificationsForStaff(SqlConnection connection, int staffId, string title)
    {
        using var command = new SqlCommand("SELECT COUNT(*) FROM Notifications WHERE recipient_staff_id = @Id AND title = @Title", connection);
        command.Parameters.AddWithValue("@Id", staffId);
        command.Parameters.AddWithValue("@Title", title);
        return (int)command.ExecuteScalar()!;
    }

    public string? GetShiftStatus(SqlConnection connection, int shiftId)
    {
        using var command = new SqlCommand("SELECT [status] FROM Shifts WHERE shift_id = @Id", connection);
        command.Parameters.AddWithValue("@Id", shiftId);
        var result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public int? GetShiftStaffId(SqlConnection connection, int shiftId)
    {
        using var command = new SqlCommand("SELECT staff_id FROM Shifts WHERE shift_id = @Id", connection);
        command.Parameters.AddWithValue("@Id", shiftId);
        var result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : (int?)Convert.ToInt32(result);
    }

    public string? GetAppointmentStatus(SqlConnection connection, int appointmentId)
    {
        using var command = new SqlCommand("SELECT [status] FROM Appointments WHERE appointment_id = @Id", connection);
        command.Parameters.AddWithValue("@Id", appointmentId);
        var result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public string? GetStaffStatus(SqlConnection connection, int staffId)
    {
        using var command = new SqlCommand("SELECT [status] FROM Staff WHERE staff_id = @Id", connection);
        command.Parameters.AddWithValue("@Id", staffId);
        var result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public void DeleteNotificationsByStaff(SqlConnection connection, int staffId)
        => Execute(connection, "DELETE FROM Notifications WHERE recipient_staff_id = @Id", staffId);

    public void DeleteSwapRequestsByShift(SqlConnection connection, int shiftId)
        => Execute(connection, "DELETE FROM ShiftSwapRequests WHERE shift_id = @Id", shiftId);

    public void DeleteSwapRequest(SqlConnection connection, int swapRequestId)
        => Execute(connection, "DELETE FROM ShiftSwapRequests WHERE swap_id = @Id", swapRequestId);

    public void DeleteShift(SqlConnection connection, int shiftId)
        => Execute(connection, "DELETE FROM Shifts WHERE shift_id = @Id", shiftId);

    public void DeleteAppointment(SqlConnection connection, int appointmentId)
        => Execute(connection, "DELETE FROM Appointments WHERE appointment_id = @Id", appointmentId);

    public void DeleteAppointmentsByDoctor(SqlConnection connection, int doctorId)
        => Execute(connection, "DELETE FROM Appointments WHERE doctor_id = @Id", doctorId);

    public void DeleteHangoutParticipants(SqlConnection connection, int hangoutId)
        => Execute(connection, "DELETE FROM Hangout_Participants WHERE hangout_id = @Id", hangoutId);

    public void DeleteHangout(SqlConnection connection, int hangoutId)
        => Execute(connection, "DELETE FROM Hangouts WHERE hangout_id = @Id", hangoutId);

    public void DeleteStaff(SqlConnection connection, int staffId)
        => Execute(connection, "DELETE FROM Staff WHERE staff_id = @Id", staffId);


    public int InsertMedicalEvaluation(
        SqlConnection connection,
        int doctorId,
        int patientId,
        string diagnosis = "Test diagnosis",
        string notes = "Test notes",
        string meds = "TestMed",
        bool assumedRisk = false)
    {
        using var command = new SqlCommand(@"
            INSERT INTO Medical_Evaluations
                (doctor_id, patient_id, diagnosis, doctor_notes, medications, source, assumed_risk)
            OUTPUT INSERTED.evaluation_id
            VALUES (@DocId, @PatId, @Diag, @Notes, @Meds, @Source, @Risk)", connection);
        command.Parameters.AddWithValue("@DocId", doctorId);
        command.Parameters.AddWithValue("@PatId", patientId);
        command.Parameters.AddWithValue("@Diag", diagnosis);
        command.Parameters.AddWithValue("@Notes", notes);
        command.Parameters.AddWithValue("@Meds", meds);
        command.Parameters.AddWithValue("@Source", "TEST");
        command.Parameters.AddWithValue("@Risk", assumedRisk);
        return (int)command.ExecuteScalar()!;
    }

    public void DeleteMedicalEvaluation(SqlConnection connection, int evaluationId)
        => Execute(connection, "DELETE FROM Medical_Evaluations WHERE evaluation_id = @Id", evaluationId);

    public void DeleteMedicalEvaluationsByDoctor(SqlConnection connection, int doctorId)
        => Execute(connection, "DELETE FROM Medical_Evaluations WHERE doctor_id = @Id", doctorId);

    public int InsertAppointmentWithStatus(SqlConnection connection, int patientId, int doctorId, DateTime start, DateTime end, string status)
        => InsertAppointment(connection, patientId, doctorId, start, end, status);

    private static void Execute(SqlConnection connection, string sql, int id)
    {
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
    }

    public void Dispose() { }
}
