using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Tests.Repositories;

public class ShiftRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public ShiftRepositoryTests(SqlTestFixture database) => this.database = database;


    [Fact]
    public void GetShifts_WhenConnectionFails_ReturnsEmptyList()
    {
        var shiftRepository = new ShiftRepository(InvalidConnectionString, new StaffRepository(InvalidConnectionString));

        Assert.Empty(shiftRepository.GetShifts());
    }

    [Fact]
    public void GetShiftById_WhenConnectionFails_ReturnsNull()
    {
        var shiftRepository = new ShiftRepository(InvalidConnectionString, new StaffRepository(InvalidConnectionString));

        Assert.Null(shiftRepository.GetShiftById(1));
    }

    [Fact]
    public void GetShiftsByStaffID_WhenConnectionFails_ReturnsEmptyList()
    {
        var shiftRepository = new ShiftRepository(InvalidConnectionString, new StaffRepository(InvalidConnectionString));

        Assert.Empty(shiftRepository.GetShiftsByStaffID(1));
    }

    [Fact]
    public void AddShift_WhenConnectionFails_DoesNotThrow()
    {
        var shiftRepository = new ShiftRepository(InvalidConnectionString, new StaffRepository(InvalidConnectionString));
        var doctor = new Doctor(1, "John", "Doe", string.Empty, string.Empty, true, "Cardiology", "L-1", DoctorStatus.AVAILABLE, 1);
        var shift = new Shift(0, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED);

        var exception = Record.Exception(() => shiftRepository.AddShift(shift));

        Assert.Null(exception);
    }

    [Fact]
    public void UpdateShiftStatus_WhenConnectionFails_DoesNotThrow()
    {
        var shiftRepository = new ShiftRepository(InvalidConnectionString, new StaffRepository(InvalidConnectionString));

        var exception = Record.Exception(() => shiftRepository.UpdateShiftStatus(1, ShiftStatus.ACTIVE));

        Assert.Null(exception);
    }

    [Fact]
    public void CancelShift_WhenConnectionFails_DoesNotThrow()
    {
        var shiftRepository = new ShiftRepository(InvalidConnectionString, new StaffRepository(InvalidConnectionString));

        var exception = Record.Exception(() => shiftRepository.CancelShift(1));

        Assert.Null(exception);
    }


    [Fact]
    public void GetShifts_ReturnsShiftFromDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Alice", "ShiftLoad", "Cardiology");
        var start = DateTime.Today.AddDays(1).AddHours(8);
        var shiftId = database.InsertShift(connection, staffId, "Ward A", start, start.AddHours(8));
        try
        {
            var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

            Assert.Contains(shiftRepository.GetShifts(), shift => shift.Id == shiftId);
        }
        finally
        {
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void AddShift_PersistsShiftToDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "AddShift", "DbTest", "Neurology");
        try
        {
            var staffRepository = new StaffRepository(database.ConnectionString);
            var shiftRepository = new ShiftRepository(database.ConnectionString, staffRepository);
            var staff = staffRepository.GetStaffById(staffId)!;
            var start = DateTime.Today.AddDays(40).AddHours(8);
            var shift = new Shift(0, staff, "Ward B", start, start.AddHours(8), ShiftStatus.SCHEDULED);
            var countBefore = shiftRepository.GetShiftsByStaffID(staffId).Count;

            shiftRepository.AddShift(shift);

            Assert.Equal(countBefore + 1, shiftRepository.GetShiftsByStaffID(staffId).Count);
        }
        finally
        {
            DeleteShiftsByStaff(connection, staffId);
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void UpdateShiftStatus_ChangesStatusInDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "UpdateStatus", "DbTest", "Oncology");
        var start = DateTime.Today.AddDays(41).AddHours(9);
        var shiftId = database.InsertShift(connection, staffId, "Ward C", start, start.AddHours(8), "SCHEDULED");
        try
        {
            var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

            shiftRepository.UpdateShiftStatus(shiftId, ShiftStatus.ACTIVE);

            Assert.Equal("ACTIVE", database.GetShiftStatus(connection, shiftId));
        }
        finally
        {
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void CancelShift_RemovesShiftFromDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "CancelShift", "DbTest", "Cardiology");
        var start = DateTime.Today.AddDays(42).AddHours(10);
        var shiftId = database.InsertShift(connection, staffId, "Ward D", start, start.AddHours(8));
        try
        {
            var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

            shiftRepository.CancelShift(shiftId);

            Assert.Null(database.GetShiftStatus(connection, shiftId));
        }
        finally
        {
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void GetShiftById_ReturnsCorrectShift()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "GetById", "DbTest", "Neurology");
        var start = DateTime.Today.AddDays(43).AddHours(8);
        var shiftId = database.InsertShift(connection, staffId, "Ward E", start, start.AddHours(8));
        try
        {
            var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

            var result = shiftRepository.GetShiftById(shiftId);

            Assert.NotNull(result);
            Assert.Equal(shiftId, result!.Id);
        }
        finally
        {
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void GetShiftById_WhenShiftDoesNotExist_ReturnsNull()
    {
        var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

        Assert.Null(shiftRepository.GetShiftById(int.MaxValue));
    }

    [Fact]
    public void GetShiftsByStaffID_ReturnsOnlyMatchingStaffShifts()
    {
        using var connection = database.OpenConnection();
        var doctorOneId = database.InsertStaff(connection, "Doctor", "StaffA", "ShiftFilter", "Cardiology");
        var doctorTwoId = database.InsertStaff(connection, "Doctor", "StaffB", "ShiftFilter", "Neurology");
        var start = DateTime.Today.AddDays(44).AddHours(8);
        var shiftAId = database.InsertShift(connection, doctorOneId, "Ward F", start, start.AddHours(4));
        var shiftBId = database.InsertShift(connection, doctorTwoId, "Ward F", start.AddHours(4), start.AddHours(8));
        try
        {
            var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

            var result = shiftRepository.GetShiftsByStaffID(doctorOneId);

            Assert.Contains(result, shift => shift.Id == shiftAId);
            Assert.DoesNotContain(result, shift => shift.Id == shiftBId);
        }
        finally
        {
            database.DeleteShift(connection, shiftAId);
            database.DeleteShift(connection, shiftBId);
            database.DeleteStaff(connection, doctorOneId);
            database.DeleteStaff(connection, doctorTwoId);
        }
    }

    [Fact]
    public void GetShiftsForStaffInRange_ReturnsOnlyShiftsWithinRange()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Dave", "ShiftRange", "Cardiology");
        var baseDate = DateTime.Today.AddDays(50);
        var inRange = database.InsertShift(connection, staffId, "Ward D", baseDate.AddHours(8), baseDate.AddHours(16));
        var outRange = database.InsertShift(connection, staffId, "Ward D", baseDate.AddDays(10).AddHours(8), baseDate.AddDays(10).AddHours(16));
        try
        {
            var shiftRepository = new ShiftRepository(database.ConnectionString, new StaffRepository(database.ConnectionString));

            var result = shiftRepository.GetShiftsForStaffInRange(staffId, baseDate, baseDate.AddDays(2));

            Assert.Contains(result, shift => shift.Id == inRange);
            Assert.DoesNotContain(result, shift => shift.Id == outRange);
        }
        finally
        {
            database.DeleteShift(connection, inRange);
            database.DeleteShift(connection, outRange);
            database.DeleteStaff(connection, staffId);
        }
    }

    private static void DeleteShiftsByStaff(SqlConnection connection, int staffId)
    {
        using var command = new SqlCommand("DELETE FROM Shifts WHERE staff_id = @Id", connection);
        command.Parameters.AddWithValue("@Id", staffId);
        command.ExecuteNonQuery();
    }
}
