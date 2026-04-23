using System;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class AppointmentRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public AppointmentRepositoryTests(SqlTestFixture db) => this.db = db;

    [Fact]
    public async Task GetUpcomingAppointmentsAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).GetUpcomingAppointmentsAsync(1, DateTime.Today, 0, 10));

    [Fact]
    public async Task GetAllDoctorsAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).GetAllDoctorsAsync());

    [Fact]
    public async Task GetAppointmentDetailsAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).GetAppointmentDetailsAsync(1));

    [Fact]
    public async Task GetAppointmentsForAdminAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).GetAppointmentsForAdminAsync(1));

    [Fact]
    public async Task AddAppointmentAsync_WhenConnectionFails_ThrowsException()
    {
        var appt = new Appointment
        {
            Id = 1, DoctorId = 10, PatientName = "PAT-5", Date = DateTime.Today,
            StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0),
            Status = "Scheduled", Type = string.Empty, Location = string.Empty,
        };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).AddAppointmentAsync(appt));
    }

    [Fact]
    public async Task UpdateAppointmentStatusAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).UpdateAppointmentStatusAsync(1, "Completed"));

    [Fact]
    public async Task GetActiveAppointmentsCountForDoctorAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).GetActiveAppointmentsCountForDoctorAsync(1));

    [Fact]
    public async Task UpdateDoctorStatusAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).UpdateDoctorStatusAsync(1, "AVAILABLE"));

    [Fact]
    public async Task GetAllDoctorsAsync_ReturnsDoctorInsertedInDatabase()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Alice", "GetAllDoc", "Cardiology");
        try
        {
            var result = await new AppointmentRepository(db.ConnectionString).GetAllDoctorsAsync();

            Assert.Contains(result, d => d.DoctorId == doctorId);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task GetAppointmentDetailsAsync_ReturnsCorrectAppointment()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Bob", "ApptDetails", "Neurology");
        var start = DateTime.Today.AddDays(1).AddHours(9);
        var apptId = db.InsertAppointment(conn, 999, doctorId, start, start.AddHours(1));
        try
        {
            var result = await new AppointmentRepository(db.ConnectionString).GetAppointmentDetailsAsync(apptId);

            Assert.NotNull(result);
            Assert.Equal(apptId, result!.Id);
            Assert.Equal(doctorId, result.DoctorId);
        }
        finally
        {
            db.DeleteAppointment(conn, apptId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task GetAppointmentsForAdminAsync_ReturnsAppointmentsForDoctor()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Carol", "ApptAdmin", "Oncology");
        var start = DateTime.Today.AddDays(2).AddHours(10);
        var apptId = db.InsertAppointment(conn, 888, doctorId, start, start.AddHours(1));
        try
        {
            var result = await new AppointmentRepository(db.ConnectionString).GetAppointmentsForAdminAsync(doctorId);

            Assert.Contains(result, a => a.Id == apptId);
        }
        finally
        {
            db.DeleteAppointment(conn, apptId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task AddAppointmentAsync_InsertsAppointmentInDatabase()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Dave", "ApptAdd", "Cardiology");
        try
        {
            var repo = new AppointmentRepository(db.ConnectionString);
            await repo.AddAppointmentAsync(new Appointment
            {
                DoctorId = doctorId, PatientName = "PAT-777",
                Date = DateTime.Today.AddDays(3),
                StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0),
                Status = "Scheduled", Type = string.Empty, Location = string.Empty,
            });

            var all = await repo.GetAppointmentsForAdminAsync(doctorId);
            Assert.Contains(all, a => a.DoctorId == doctorId);
        }
        finally
        {
            db.DeleteAppointmentsByDoctor(conn, doctorId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task UpdateAppointmentStatusAsync_ChangesStatus()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Eve", "ApptUpdate", "Endocrinology");
        var start = DateTime.Today.AddDays(4).AddHours(14);
        var apptId = db.InsertAppointment(conn, 111, doctorId, start, start.AddHours(1));
        try
        {
            await new AppointmentRepository(db.ConnectionString).UpdateAppointmentStatusAsync(apptId, "Completed");

            Assert.Equal("Completed", db.GetAppointmentStatus(conn, apptId));
        }
        finally
        {
            db.DeleteAppointment(conn, apptId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task GetActiveAppointmentsCountForDoctorAsync_ReturnsCorrectCount()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Frank", "ApptCount", "Cardiology");
        var start = DateTime.Today.AddDays(5).AddHours(9);
        var appt1 = db.InsertAppointment(conn, 222, doctorId, start, start.AddHours(1), "Scheduled");
        var appt2 = db.InsertAppointment(conn, 333, doctorId, start.AddHours(2), start.AddHours(3), "Scheduled");
        var appt3 = db.InsertAppointment(conn, 444, doctorId, start.AddHours(4), start.AddHours(5), "Completed");
        try
        {
            Assert.Equal(2, await new AppointmentRepository(db.ConnectionString).GetActiveAppointmentsCountForDoctorAsync(doctorId));
        }
        finally
        {
            db.DeleteAppointment(conn, appt1);
            db.DeleteAppointment(conn, appt2);
            db.DeleteAppointment(conn, appt3);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task GetUpcomingAppointmentsAsync_ReturnsAppointmentsInDateRange()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Grace", "ApptUpcoming", "Neurology");
        var start = DateTime.Today.AddDays(2).AddHours(8);
        var apptId = db.InsertAppointment(conn, 555, doctorId, start, start.AddHours(1));
        try
        {
            var result = await new AppointmentRepository(db.ConnectionString).GetUpcomingAppointmentsAsync(doctorId, DateTime.Today, 0, 100);

            Assert.Contains(result, a => a.Id == apptId);
        }
        finally
        {
            db.DeleteAppointment(conn, apptId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public async Task UpdateDoctorStatusAsync_UpdatesStaffStatusInDatabase()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Henry", "ApptDocStatus", "Cardiology", status: "Available");
        try
        {
            await new AppointmentRepository(db.ConnectionString).UpdateDoctorStatusAsync(doctorId, "In_Examination");

            Assert.Equal("In_Examination", db.GetStaffStatus(conn, doctorId));
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
        }
    }
}
