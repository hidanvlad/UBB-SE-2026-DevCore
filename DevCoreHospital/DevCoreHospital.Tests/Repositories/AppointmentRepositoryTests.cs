using System;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class AppointmentRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public AppointmentRepositoryTests(SqlTestFixture database) => this.database = database;

    [Fact]
    public async Task GetUpcomingAppointmentsAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString)
                .GetAppointmentsInRangeAsync(1, DateTime.Today, DateTime.Today.AddDays(31), 0, 10));

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
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString)
                .AddAppointmentAsync(5, 10, DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), "Scheduled"));
    }

    [Fact]
    public async Task UpdateAppointmentStatusAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).UpdateAppointmentStatusAsync(1, "Completed"));

    [Fact]
    public async Task GetActiveAppointmentsCountForDoctorAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString)
                .GetAppointmentsCountForDoctorByStatusAsync(1, "Scheduled"));

    [Fact]
    public async Task UpdateDoctorStatusAsync_WhenConnectionFails_ThrowsException()
        => await Assert.ThrowsAnyAsync<Exception>(() =>
            new AppointmentRepository(InvalidConnectionString).UpdateDoctorStatusAsync(1, "AVAILABLE"));

    [Fact]
    public async Task GetAllDoctorsAsync_ReturnsDoctorInsertedInDatabase()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Alice", "GetAllDoc", "Cardiology");
        try
        {
            var result = await new AppointmentRepository(database.ConnectionString).GetAllDoctorsAsync();

            bool DoctorMatchesInserted((int DoctorId, string DoctorName) doctor) => doctor.DoctorId == doctorId;
            Assert.Contains(result, DoctorMatchesInserted);
        }
        finally
        {
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task GetAppointmentDetailsAsync_ReturnsCorrectAppointment()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Bob", "ApptDetails", "Neurology");
        var start = DateTime.Today.AddDays(1).AddHours(9);
        var appointmentId = database.InsertAppointment(connection, 999, doctorId, start, start.AddHours(1));
        try
        {
            var result = await new AppointmentRepository(database.ConnectionString).GetAppointmentDetailsAsync(appointmentId);

            Assert.NotNull(result);
            Assert.Equal(appointmentId, result!.Id);
            Assert.Equal(doctorId, result.DoctorId);
        }
        finally
        {
            database.DeleteAppointment(connection, appointmentId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task GetAppointmentsForAdminAsync_ReturnsAppointmentsForDoctor()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Carol", "ApptAdmin", "Oncology");
        var start = DateTime.Today.AddDays(2).AddHours(10);
        var appointmentId = database.InsertAppointment(connection, 888, doctorId, start, start.AddHours(1));
        try
        {
            var result = await new AppointmentRepository(database.ConnectionString).GetAppointmentsForAdminAsync(doctorId);

            bool AppointmentMatchesInserted(Appointment appointment) => appointment.Id == appointmentId;
            Assert.Contains(result, AppointmentMatchesInserted);
        }
        finally
        {
            database.DeleteAppointment(connection, appointmentId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task AddAppointmentAsync_InsertsAppointmentInDatabase()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Dave", "ApptAdd", "Cardiology");
        try
        {
            var repository = new AppointmentRepository(database.ConnectionString);
            var start = DateTime.Today.AddDays(3).AddHours(11);
            var end = DateTime.Today.AddDays(3).AddHours(12);
            await repository.AddAppointmentAsync(777, doctorId, start, end, "Scheduled");

            var all = await repository.GetAppointmentsForAdminAsync(doctorId);
            bool AppointmentBelongsToDoctor(Appointment appointment) => appointment.DoctorId == doctorId;
            Assert.Contains(all, AppointmentBelongsToDoctor);
        }
        finally
        {
            database.DeleteAppointmentsByDoctor(connection, doctorId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task UpdateAppointmentStatusAsync_ChangesStatus()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Eve", "ApptUpdate", "Endocrinology");
        var start = DateTime.Today.AddDays(4).AddHours(14);
        var appointmentId = database.InsertAppointment(connection, 111, doctorId, start, start.AddHours(1));
        try
        {
            await new AppointmentRepository(database.ConnectionString).UpdateAppointmentStatusAsync(appointmentId, "Completed");

            Assert.Equal("Completed", database.GetAppointmentStatus(connection, appointmentId));
        }
        finally
        {
            database.DeleteAppointment(connection, appointmentId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task GetActiveAppointmentsCountForDoctorAsync_ReturnsCorrectCount()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Frank", "ApptCount", "Cardiology");
        var start = DateTime.Today.AddDays(5).AddHours(9);
        var appointmentOneId = database.InsertAppointment(connection, 222, doctorId, start, start.AddHours(1), "Scheduled");
        var appointmentTwoId = database.InsertAppointment(connection, 333, doctorId, start.AddHours(2), start.AddHours(3), "Scheduled");
        var appointmentThreeId = database.InsertAppointment(connection, 444, doctorId, start.AddHours(4), start.AddHours(5), "Completed");
        try
        {
            Assert.Equal(2, await new AppointmentRepository(database.ConnectionString)
                .GetAppointmentsCountForDoctorByStatusAsync(doctorId, "Scheduled"));
        }
        finally
        {
            database.DeleteAppointment(connection, appointmentOneId);
            database.DeleteAppointment(connection, appointmentTwoId);
            database.DeleteAppointment(connection, appointmentThreeId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task GetUpcomingAppointmentsAsync_ReturnsAppointmentsInDateRange()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Grace", "ApptUpcoming", "Neurology");
        var start = DateTime.Today.AddDays(2).AddHours(8);
        var appointmentId = database.InsertAppointment(connection, 555, doctorId, start, start.AddHours(1));
        try
        {
            var result = await new AppointmentRepository(database.ConnectionString)
                .GetAppointmentsInRangeAsync(doctorId, DateTime.Today, DateTime.Today.AddDays(31), 0, 100);

            bool AppointmentMatchesInserted(Appointment appointment) => appointment.Id == appointmentId;
            Assert.Contains(result, AppointmentMatchesInserted);
        }
        finally
        {
            database.DeleteAppointment(connection, appointmentId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public async Task UpdateDoctorStatusAsync_UpdatesStaffStatusInDatabase()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Henry", "ApptDocStatus", "Cardiology", status: "Available");
        try
        {
            await new AppointmentRepository(database.ConnectionString).UpdateDoctorStatusAsync(doctorId, "In_Examination");

            Assert.Equal("In_Examination", database.GetStaffStatus(connection, doctorId));
        }
        finally
        {
            database.DeleteStaff(connection, doctorId);
        }
    }
}
