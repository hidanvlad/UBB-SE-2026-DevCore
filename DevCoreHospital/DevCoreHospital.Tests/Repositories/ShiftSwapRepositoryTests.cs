using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class ShiftSwapRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public ShiftSwapRepositoryTests(SqlTestFixture database) => this.database = database;

    [Fact]
    public void GetSwapRequestsForColleague_WhenConnectionFails_ReturnsEmptyList()
    {
        var repository = new ShiftSwapRepository(InvalidConnectionString);

        Assert.Empty(repository.GetSwapRequestsForColleague(1));
    }

    [Fact]
    public void GetShiftSwapRequestById_WhenConnectionFails_ReturnsNull()
    {
        var repository = new ShiftSwapRepository(InvalidConnectionString);

        Assert.Null(repository.GetShiftSwapRequestById(1));
    }

    [Fact]
    public void CreateShiftSwapRequest_WhenConnectionFails_ReturnsZero()
    {
        var repository = new ShiftSwapRepository(InvalidConnectionString);
        var request = new ShiftSwapRequest
        {
            ShiftId = 1,
            RequesterId = 2,
            ColleagueId = 3,
            RequestedAt = DateTime.UtcNow,
            Status = ShiftSwapRequestStatus.PENDING,
        };

        Assert.Equal(0, repository.CreateShiftSwapRequest(request));
    }

    [Fact]
    public void UpdateShiftSwapRequestStatus_WhenConnectionFails_ReturnsFalse()
    {
        var repository = new ShiftSwapRepository(InvalidConnectionString);

        Assert.False(repository.UpdateShiftSwapRequestStatus(1, "ACCEPTED"));
    }

    [Fact]
    public void ReassignShiftToStaff_WhenConnectionFails_ReturnsFalse()
    {
        var repository = new ShiftSwapRepository(InvalidConnectionString);

        Assert.False(repository.ReassignShiftToStaff(1, 5));
    }

    [Fact]
    public void AddNotification_WhenConnectionFails_DoesNotThrow()
    {
        var repository = new ShiftSwapRepository(InvalidConnectionString);

        var exception = Record.Exception(() => repository.AddNotification(1, "Test Title", "Test message"));

        Assert.Null(exception);
    }

    [Fact]
    public void CreateShiftSwapRequest_ReturnsPositiveId()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "Alice", "CreateSwap", "Cardiology");
        var colleagueId = database.InsertStaff(connection, "Doctor", "Bob", "CreateSwap", "Cardiology");
        var shiftId = database.InsertShift(connection, requesterId, "Ward A", DateTime.Now.AddHours(1), DateTime.Now.AddHours(9));
        int swapId = 0;
        try
        {
            var repository = new ShiftSwapRepository(database.ConnectionString);
            swapId = repository.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId,
                RequesterId = requesterId,
                ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING,
            });

            Assert.True(swapId > 0);
        }
        finally
        {
            database.DeleteSwapRequest(connection, swapId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }

    [Fact]
    public void GetShiftSwapRequestById_ReturnsCorrectRequest()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "Alice", "GetById", "Cardiology");
        var colleagueId = database.InsertStaff(connection, "Doctor", "Bob", "GetById", "Cardiology");
        var shiftId = database.InsertShift(connection, requesterId, "Ward B", DateTime.Now.AddHours(2), DateTime.Now.AddHours(10));
        int swapId = 0;
        try
        {
            var repository = new ShiftSwapRepository(database.ConnectionString);
            swapId = repository.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId, RequesterId = requesterId, ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING,
            });

            var result = repository.GetShiftSwapRequestById(swapId);

            Assert.NotNull(result);
            Assert.Equal(swapId, result!.SwapId);
            Assert.Equal(shiftId, result.ShiftId);
            Assert.Equal(requesterId, result.RequesterId);
            Assert.Equal(colleagueId, result.ColleagueId);
            Assert.Equal(ShiftSwapRequestStatus.PENDING, result.Status);
        }
        finally
        {
            database.DeleteSwapRequest(connection, swapId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }

    [Fact]
    public void GetSwapRequestsForColleague_ReturnsRequestsForColleague()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "Alice", "GetPending", "Neurology");
        var colleagueId = database.InsertStaff(connection, "Doctor", "Bob", "GetPending", "Neurology");
        var shiftId = database.InsertShift(connection, requesterId, "Ward C", DateTime.Now.AddHours(3), DateTime.Now.AddHours(11));
        int swapId = 0;
        try
        {
            var repository = new ShiftSwapRepository(database.ConnectionString);
            swapId = repository.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId, RequesterId = requesterId, ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING,
            });

            var results = repository.GetSwapRequestsForColleague(colleagueId);

            Assert.Contains(results, swapRequest => swapRequest.SwapId == swapId);
        }
        finally
        {
            database.DeleteSwapRequest(connection, swapId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }

    [Fact]
    public void UpdateShiftSwapRequestStatus_ReturnsTrueAndUpdatesStatus()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "Alice", "UpdateStatus", "Oncology");
        var colleagueId = database.InsertStaff(connection, "Doctor", "Bob", "UpdateStatus", "Oncology");
        var shiftId = database.InsertShift(connection, requesterId, "Ward D", DateTime.Now.AddHours(4), DateTime.Now.AddHours(12));
        int swapId = 0;
        try
        {
            var repository = new ShiftSwapRepository(database.ConnectionString);
            swapId = repository.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId, RequesterId = requesterId, ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING,
            });

            var result = repository.UpdateShiftSwapRequestStatus(swapId, "ACCEPTED");

            Assert.True(result);
            Assert.Equal(ShiftSwapRequestStatus.ACCEPTED, repository.GetShiftSwapRequestById(swapId)!.Status);
        }
        finally
        {
            database.DeleteSwapRequest(connection, swapId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }

    [Fact]
    public void ReassignShiftToStaff_ReturnsTrueAndUpdatesStaffOnShift()
    {
        using var connection = database.OpenConnection();
        var originalStaffId = database.InsertStaff(connection, "Doctor", "Alice", "Reassign", "Cardiology");
        var newStaffId = database.InsertStaff(connection, "Doctor", "Bob", "Reassign", "Cardiology");
        var shiftId = database.InsertShift(connection, originalStaffId, "Ward E", DateTime.Now.AddHours(5), DateTime.Now.AddHours(13));
        try
        {
            var repository = new ShiftSwapRepository(database.ConnectionString);

            var result = repository.ReassignShiftToStaff(shiftId, newStaffId);

            Assert.True(result);
            Assert.Equal(newStaffId, database.GetShiftStaffId(connection, shiftId));
        }
        finally
        {
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, originalStaffId);
            database.DeleteStaff(connection, newStaffId);
        }
    }

    [Fact]
    public void AddNotification_InsertsRecordInDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Alice", "Notification", "Cardiology");
        try
        {
            var repository = new ShiftSwapRepository(database.ConnectionString);

            repository.AddNotification(staffId, "Test Alert", "This is a test notification.");

            Assert.Equal(1, database.CountNotificationsForStaff(connection, staffId, "Test Alert"));
        }
        finally
        {
            database.DeleteNotificationsByStaff(connection, staffId);
            database.DeleteStaff(connection, staffId);
        }
    }
}
