using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class ShiftRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public ShiftRepositoryTests(SqlTestFixture db) => this.db = db;

    [Fact]
    public void AddShift_WhenShiftIsProvided_AddsShiftToCachedList()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var initialCount = shiftRepo.GetShifts().Count;
        var shift = BuildShift(101, BuildDoctor(1, "Cardiology"), "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED);

        shiftRepo.AddShift(shift);

        Assert.Equal(initialCount + 1, shiftRepo.GetShifts().Count);
        Assert.Equal(shift.Id, shiftRepo.GetShifts().Last().Id);
    }

    [Fact]
    public void UpdateShiftStatus_WhenShiftExists_ChangesShiftStatusInCache()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var shift = BuildShift(102, BuildDoctor(2, "Neurology"), "ER", DateTime.Today.AddHours(9), DateTime.Today.AddHours(13), ShiftStatus.SCHEDULED);
        shiftRepo.AddShift(shift);

        shiftRepo.UpdateShiftStatus(102, ShiftStatus.ACTIVE);

        var updated = Assert.Single(shiftRepo.GetShifts().Where(s => s.Id == 102));
        Assert.Equal(ShiftStatus.ACTIVE, updated.Status);
    }

    [Fact]
    public void CancelShift_WhenShiftExists_RemovesShiftFromCachedList()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var shift = BuildShift(103, BuildDoctor(3, "Oncology"), "ER", DateTime.Today.AddHours(10), DateTime.Today.AddHours(14), ShiftStatus.SCHEDULED);
        shiftRepo.AddShift(shift);

        shiftRepo.CancelShift(103);

        Assert.Equal(0, shiftRepo.GetShifts().Count(s => s.Id == 103));
    }

    [Fact]
    public void GetShiftsByStaffID_WhenShiftsHaveMixedStaff_ReturnsOnlyMatchingStaffShifts()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var doctorOne = BuildDoctor(4, "Cardiology");
        var doctorTwo = BuildDoctor(5, "Neurology");
        shiftRepo.AddShift(BuildShift(104, doctorOne, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(10), ShiftStatus.SCHEDULED));
        shiftRepo.AddShift(BuildShift(105, doctorTwo, "ER", DateTime.Today.AddHours(10), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED));
        shiftRepo.AddShift(BuildShift(106, doctorOne, "ER", DateTime.Today.AddHours(12), DateTime.Today.AddHours(14), ShiftStatus.SCHEDULED));

        var result = shiftRepo.GetShiftsByStaffID(doctorOne.StaffID);

        Assert.Equal(new[] { 104, 106 }, result.Select(s => s.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void GetActiveShifts_WhenStatusesDiffer_ReturnsOnlyActiveShifts()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var doctor = BuildDoctor(6, "Emergency Medicine");
        shiftRepo.AddShift(BuildShift(107, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.ACTIVE));
        shiftRepo.AddShift(BuildShift(108, doctor, "ER", DateTime.Today.AddHours(13), DateTime.Today.AddHours(17), ShiftStatus.SCHEDULED));

        var result = shiftRepo.GetActiveShifts();

        Assert.Equal(new[] { 107 }, result.Select(s => s.Id).ToArray());
    }

    [Fact]
    public void GetShiftById_WhenShiftExists_ReturnsCorrectShift()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        shiftRepo.AddShift(BuildShift(200, BuildDoctor(10, "Cardiology"), "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED));

        var result = shiftRepo.GetShiftById(200);

        Assert.NotNull(result);
        Assert.Equal(200, result!.Id);
    }

    [Fact]
    public void GetShiftById_WhenShiftDoesNotExist_ReturnsNull()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);

        Assert.Null(shiftRepo.GetShiftById(9999));
    }

    [Fact]
    public void GetWeeklyHours_WhenShiftsAreInCurrentWeek_ReturnsTotalHours()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var doctor = BuildDoctor(11, "Neurology");
        var now = DateTime.Now;
        var weekMonday = now.Date.AddDays(-((7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7));
        shiftRepo.AddShift(BuildShift(201, doctor, "ER", weekMonday.AddHours(8), weekMonday.AddHours(16), ShiftStatus.SCHEDULED));
        shiftRepo.AddShift(BuildShift(202, doctor, "ER", weekMonday.AddDays(1).AddHours(8), weekMonday.AddDays(1).AddHours(12), ShiftStatus.SCHEDULED));

        Assert.Equal(12f, shiftRepo.GetWeeklyHours(doctor.StaffID));
    }

    [Fact]
    public void GetWeeklyHours_WhenShiftsAreOutsideCurrentWeek_ReturnsZero()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        var doctor = BuildDoctor(12, "Oncology");
        shiftRepo.AddShift(BuildShift(203, doctor, "ER", DateTime.Today.AddDays(-8).AddHours(8), DateTime.Today.AddDays(-8).AddHours(16), ShiftStatus.SCHEDULED));

        Assert.Equal(0f, shiftRepo.GetWeeklyHours(doctor.StaffID));
    }

    [Fact]
    public void IsStaffWorkingDuring_WhenConnectionFails_ReturnsFalse()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);

        Assert.False(shiftRepo.IsStaffWorkingDuring(1, DateTime.Today, DateTime.Today.AddHours(8)));
    }

    [Fact]
    public void Refresh_WhenConnectionFails_ClearsCache()
    {
        var staffRepo = new StaffRepository(InvalidConnectionString);
        var shiftRepo = new ShiftRepository(InvalidConnectionString, staffRepo);
        shiftRepo.AddShift(BuildShift(204, BuildDoctor(13, "Cardiology"), "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED));

        shiftRepo.Refresh();

        Assert.Empty(shiftRepo.GetShifts());
    }

    [Fact]
    public void Constructor_LoadsShiftsFromDatabase()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Alice", "ShiftLoad", "Cardiology");
        var start = DateTime.Today.AddDays(1).AddHours(8);
        var shiftId = db.InsertShift(conn, staffId, "Ward A", start, start.AddHours(8));
        try
        {
            var staffRepo = new StaffRepository(db.ConnectionString);
            var shiftRepo = new ShiftRepository(db.ConnectionString, staffRepo);

            Assert.Contains(shiftRepo.GetShifts(), s => s.Id == shiftId);
        }
        finally
        {
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void IsStaffWorkingDuring_ReturnsTrueWhenShiftOverlaps()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Bob", "IsWorking", "Neurology");
        var shiftStart = DateTime.Today.AddDays(2).AddHours(8);
        var shiftId = db.InsertShift(conn, staffId, "Ward B", shiftStart, shiftStart.AddHours(8), "SCHEDULED");
        try
        {
            var shiftRepo = new ShiftRepository(db.ConnectionString, new StaffRepository(db.ConnectionString));

            Assert.True(shiftRepo.IsStaffWorkingDuring(staffId, shiftStart.AddHours(2), shiftStart.AddHours(4)));
        }
        finally
        {
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void IsStaffWorkingDuring_ReturnsFalseWhenShiftDoesNotOverlap()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Carol", "NotWorking", "Oncology");
        var shiftStart = DateTime.Today.AddDays(3).AddHours(8);
        var shiftEnd = shiftStart.AddHours(8);
        var shiftId = db.InsertShift(conn, staffId, "Ward C", shiftStart, shiftEnd, "SCHEDULED");
        try
        {
            var shiftRepo = new ShiftRepository(db.ConnectionString, new StaffRepository(db.ConnectionString));

            Assert.False(shiftRepo.IsStaffWorkingDuring(staffId, shiftEnd.AddHours(2), shiftEnd.AddHours(4)));
        }
        finally
        {
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void GetShiftsForStaffInRange_ReturnsOnlyShiftsWithinRange()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Dave", "ShiftRange", "Cardiology");
        var baseDate = DateTime.Today.AddDays(5);
        var inRange = db.InsertShift(conn, staffId, "Ward D", baseDate.AddHours(8), baseDate.AddHours(16));
        var outRange = db.InsertShift(conn, staffId, "Ward D", baseDate.AddDays(10).AddHours(8), baseDate.AddDays(10).AddHours(16));
        try
        {
            var shiftRepo = new ShiftRepository(db.ConnectionString, new StaffRepository(db.ConnectionString));

            var result = shiftRepo.GetShiftsForStaffInRange(staffId, baseDate, baseDate.AddDays(2));

            Assert.Contains(result, s => s.Id == inRange);
            Assert.DoesNotContain(result, s => s.Id == outRange);
        }
        finally
        {
            db.DeleteShift(conn, inRange);
            db.DeleteShift(conn, outRange);
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void Refresh_ReloadsShiftsFromDatabase()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Eve", "ShiftRefresh", "Neurology");
        var start = DateTime.Today.AddDays(6).AddHours(8);
        var staffRepo = new StaffRepository(db.ConnectionString);
        var shiftRepo = new ShiftRepository(db.ConnectionString, staffRepo);
        var shiftId = db.InsertShift(conn, staffId, "Ward E", start, start.AddHours(8));
        try
        {
            shiftRepo.Refresh();

            Assert.Contains(shiftRepo.GetShifts(), s => s.Id == shiftId);
        }
        finally
        {
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, staffId);
        }
    }

    private static Doctor BuildDoctor(int staffId, string specialization)
        => new Doctor(staffId, "John", "Doe", "john.doe@example.com", string.Empty, false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

    private static Shift BuildShift(int id, IStaff staff, string location, DateTime start, DateTime end, ShiftStatus status)
        => new Shift(id, staff, location, start, end, status);
}
