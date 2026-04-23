using System;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class SalaryRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;
    private const string InvalidConnectionString = "InvalidConnectionString";
    private const int FallbackMedicinesSoldCount = 150;

    public SalaryRepositoryTests(SqlTestFixture db) => this.db = db;

    [Fact]
    public void GetShiftHoursFromDb_WhenConnectionFails_ReturnsZero()
        => Assert.Equal(0.0, new SalaryRepository(InvalidConnectionString).GetShiftHoursFromDb(1));

    [Fact]
    public void GetMedicinesSold_WhenConnectionFails_ReturnsFallbackCount()
        => Assert.Equal(FallbackMedicinesSoldCount, new SalaryRepository(InvalidConnectionString).GetMedicinesSold(1, 5, 2026));

    [Fact]
    public void GetMedicinesSold_WhenConnectionFails_ReturnsFallbackCount_ForAnyStaff()
    {
        var repo = new SalaryRepository(InvalidConnectionString);

        Assert.Equal(FallbackMedicinesSoldCount, repo.GetMedicinesSold(10, 1, 2025));
        Assert.Equal(FallbackMedicinesSoldCount, repo.GetMedicinesSold(99, 12, 2024));
    }

    [Fact]
    public void DidStaffParticipateInHangout_WhenConnectionFails_ReturnsFalse()
        => Assert.False(new SalaryRepository(InvalidConnectionString).DidStaffParticipateInHangout(1, 5, 2026));

    [Fact]
    public void DidStaffParticipateInHangout_WhenConnectionFails_ReturnsFalse_ForAnyStaff()
    {
        var repo = new SalaryRepository(InvalidConnectionString);

        Assert.False(repo.DidStaffParticipateInHangout(10, 1, 2025));
        Assert.False(repo.DidStaffParticipateInHangout(99, 12, 2024));
    }

    [Fact]
    public void GetShiftHoursFromDb_ReturnsCorrectHours()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Alice", "SalaryHours", "Cardiology");
        var start = new DateTime(2026, 5, 4, 8, 0, 0);
        var shiftId = db.InsertShift(conn, staffId, "Ward A", start, start.AddHours(8));
        try
        {
            Assert.Equal(8.0, new SalaryRepository(db.ConnectionString).GetShiftHoursFromDb(shiftId), precision: 2);
        }
        finally
        {
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void GetShiftHoursFromDb_ReturnsZero_WhenShiftDoesNotExist()
        => Assert.Equal(0.0, new SalaryRepository(db.ConnectionString).GetShiftHoursFromDb(int.MaxValue), precision: 2);

    [Fact]
    public void DidStaffParticipateInHangout_ReturnsTrue_WhenParticipated()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Alice", "HangoutParticipant", "Cardiology");
        var hangoutId = db.InsertHangout(conn, "Test Hang", new DateTime(2026, 5, 1, 17, 0, 0));
        db.InsertHangoutParticipant(conn, hangoutId, staffId);
        try
        {
            Assert.True(new SalaryRepository(db.ConnectionString).DidStaffParticipateInHangout(staffId, 5, 2026));
        }
        finally
        {
            db.DeleteHangoutParticipants(conn, hangoutId);
            db.DeleteHangout(conn, hangoutId);
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void DidStaffParticipateInHangout_ReturnsFalse_WhenNotParticipated()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Bob", "NoHangout", "Neurology");
        try
        {
            Assert.False(new SalaryRepository(db.ConnectionString).DidStaffParticipateInHangout(staffId, 5, 2026));
        }
        finally
        {
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void DidStaffParticipateInHangout_ReturnsFalse_WhenHangoutIsInDifferentMonth()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Carol", "WrongMonth", "Oncology");
        var hangoutId = db.InsertHangout(conn, "Mar Hang", new DateTime(2026, 3, 15, 17, 0, 0));
        db.InsertHangoutParticipant(conn, hangoutId, staffId);
        try
        {
            Assert.False(new SalaryRepository(db.ConnectionString).DidStaffParticipateInHangout(staffId, 5, 2026));
        }
        finally
        {
            db.DeleteHangoutParticipants(conn, hangoutId);
            db.DeleteHangout(conn, hangoutId);
            db.DeleteStaff(conn, staffId);
        }
    }
}
