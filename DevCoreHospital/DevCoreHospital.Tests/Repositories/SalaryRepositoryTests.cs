using System;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class SalaryRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;
    private const string InvalidConnectionString = "InvalidConnectionString";
    private const int FallbackMedicinesSoldCount = 150;

    public SalaryRepositoryTests(SqlTestFixture database) => this.database = database;

    [Fact]
    public void GetShiftHoursFromDb_WhenConnectionFails_ReturnsZero()
        => Assert.Equal(0.0, new SalaryRepository(InvalidConnectionString).GetShiftHoursFromDb(1));

    [Fact]
    public void GetMedicinesSold_WhenConnectionFails_ReturnsFallbackCount()
        => Assert.Equal(FallbackMedicinesSoldCount, new SalaryRepository(InvalidConnectionString).GetMedicinesSold(1, 5, 2026));

    [Fact]
    public void GetMedicinesSold_WhenConnectionFails_ReturnsFallbackCount_ForAnyStaff()
    {
        var repository = new SalaryRepository(InvalidConnectionString);

        Assert.Equal(FallbackMedicinesSoldCount, repository.GetMedicinesSold(10, 1, 2025));
        Assert.Equal(FallbackMedicinesSoldCount, repository.GetMedicinesSold(99, 12, 2024));
    }

    [Fact]
    public void DidStaffParticipateInHangout_WhenConnectionFails_ReturnsFalse()
        => Assert.False(new SalaryRepository(InvalidConnectionString).DidStaffParticipateInHangout(1, 5, 2026));

    [Fact]
    public void DidStaffParticipateInHangout_WhenConnectionFails_ReturnsFalse_ForAnyStaff()
    {
        var repository = new SalaryRepository(InvalidConnectionString);

        Assert.False(repository.DidStaffParticipateInHangout(10, 1, 2025));
        Assert.False(repository.DidStaffParticipateInHangout(99, 12, 2024));
    }

    [Fact]
    public void GetShiftHoursFromDb_ReturnsCorrectHours()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Alice", "SalaryHours", "Cardiology");
        var start = new DateTime(2026, 5, 4, 8, 0, 0);
        var shiftId = database.InsertShift(connection, staffId, "Ward A", start, start.AddHours(8));
        try
        {
            Assert.Equal(8.0, new SalaryRepository(database.ConnectionString).GetShiftHoursFromDb(shiftId), precision: 2);
        }
        finally
        {
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void GetShiftHoursFromDb_ReturnsZero_WhenShiftDoesNotExist()
        => Assert.Equal(0.0, new SalaryRepository(database.ConnectionString).GetShiftHoursFromDb(int.MaxValue), precision: 2);

    [Fact]
    public void DidStaffParticipateInHangout_ReturnsTrue_WhenParticipated()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Alice", "HangoutParticipant", "Cardiology");
        var hangoutId = database.InsertHangout(connection, "Test Hang", new DateTime(2026, 5, 1, 17, 0, 0));
        database.InsertHangoutParticipant(connection, hangoutId, staffId);
        try
        {
            Assert.True(new SalaryRepository(database.ConnectionString).DidStaffParticipateInHangout(staffId, 5, 2026));
        }
        finally
        {
            database.DeleteHangoutParticipants(connection, hangoutId);
            database.DeleteHangout(connection, hangoutId);
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void DidStaffParticipateInHangout_ReturnsFalse_WhenNotParticipated()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Bob", "NoHangout", "Neurology");
        try
        {
            Assert.False(new SalaryRepository(database.ConnectionString).DidStaffParticipateInHangout(staffId, 5, 2026));
        }
        finally
        {
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void DidStaffParticipateInHangout_ReturnsFalse_WhenHangoutIsInDifferentMonth()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Carol", "WrongMonth", "Oncology");
        var hangoutId = database.InsertHangout(connection, "Mar Hang", new DateTime(2026, 3, 15, 17, 0, 0));
        database.InsertHangoutParticipant(connection, hangoutId, staffId);
        try
        {
            Assert.False(new SalaryRepository(database.ConnectionString).DidStaffParticipateInHangout(staffId, 5, 2026));
        }
        finally
        {
            database.DeleteHangoutParticipants(connection, hangoutId);
            database.DeleteHangout(connection, hangoutId);
            database.DeleteStaff(connection, staffId);
        }
    }
}
