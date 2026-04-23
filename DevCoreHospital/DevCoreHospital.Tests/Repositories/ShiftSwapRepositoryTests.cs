using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class ShiftSwapRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public ShiftSwapRepositoryTests(SqlTestFixture db) => this.db = db;

    [Fact]
    public void GetPendingSwapRequestsForColleague_WhenConnectionFails_ReturnsEmptyList()
    {
        var repo = new ShiftSwapRepository(InvalidConnectionString);

        Assert.Empty(repo.GetPendingSwapRequestsForColleague(1));
    }

    [Fact]
    public void GetShiftSwapRequestById_WhenConnectionFails_ReturnsNull()
    {
        var repo = new ShiftSwapRepository(InvalidConnectionString);

        Assert.Null(repo.GetShiftSwapRequestById(1));
    }

    [Fact]
    public void CreateShiftSwapRequest_WhenConnectionFails_ReturnsZero()
    {
        var repo = new ShiftSwapRepository(InvalidConnectionString);
        var request = new ShiftSwapRequest
        {
            ShiftId = 1,
            RequesterId = 2,
            ColleagueId = 3,
            RequestedAt = DateTime.UtcNow,
            Status = ShiftSwapRequestStatus.PENDING,
        };

        Assert.Equal(0, repo.CreateShiftSwapRequest(request));
    }

    [Fact]
    public void UpdateShiftSwapRequestStatus_WhenConnectionFails_ReturnsFalse()
    {
        var repo = new ShiftSwapRepository(InvalidConnectionString);

        Assert.False(repo.UpdateShiftSwapRequestStatus(1, "ACCEPTED"));
    }

    [Fact]
    public void ReassignShiftToStaff_WhenConnectionFails_ReturnsFalse()
    {
        var repo = new ShiftSwapRepository(InvalidConnectionString);

        Assert.False(repo.ReassignShiftToStaff(1, 5));
    }

    [Fact]
    public void AddNotification_WhenConnectionFails_DoesNotThrow()
    {
        var repo = new ShiftSwapRepository(InvalidConnectionString);

        var ex = Record.Exception(() => repo.AddNotification(1, "Test Title", "Test message"));

        Assert.Null(ex);
    }

    [Fact]
    public void CreateShiftSwapRequest_ReturnsPositiveId()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "Alice", "CreateSwap", "Cardiology");
        var colleagueId = db.InsertStaff(conn, "Doctor", "Bob", "CreateSwap", "Cardiology");
        var shiftId = db.InsertShift(conn, requesterId, "Ward A", DateTime.Now.AddHours(1), DateTime.Now.AddHours(9));
        int swapId = 0;
        try
        {
            var repo = new ShiftSwapRepository(db.ConnectionString);
            swapId = repo.CreateShiftSwapRequest(new ShiftSwapRequest
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
            if (swapId > 0) db.DeleteSwapRequest(conn, swapId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    [Fact]
    public void GetShiftSwapRequestById_ReturnsCorrectRequest()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "Alice", "GetById", "Cardiology");
        var colleagueId = db.InsertStaff(conn, "Doctor", "Bob", "GetById", "Cardiology");
        var shiftId = db.InsertShift(conn, requesterId, "Ward B", DateTime.Now.AddHours(2), DateTime.Now.AddHours(10));
        int swapId = 0;
        try
        {
            var repo = new ShiftSwapRepository(db.ConnectionString);
            swapId = repo.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId, RequesterId = requesterId, ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING,
            });

            var result = repo.GetShiftSwapRequestById(swapId);

            Assert.NotNull(result);
            Assert.Equal(swapId, result!.SwapId);
            Assert.Equal(shiftId, result.ShiftId);
            Assert.Equal(requesterId, result.RequesterId);
            Assert.Equal(colleagueId, result.ColleagueId);
            Assert.Equal(ShiftSwapRequestStatus.PENDING, result.Status);
        }
        finally
        {
            if (swapId > 0) db.DeleteSwapRequest(conn, swapId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    [Fact]
    public void GetPendingSwapRequestsForColleague_ReturnsPendingRequests()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "Alice", "GetPending", "Neurology");
        var colleagueId = db.InsertStaff(conn, "Doctor", "Bob", "GetPending", "Neurology");
        var shiftId = db.InsertShift(conn, requesterId, "Ward C", DateTime.Now.AddHours(3), DateTime.Now.AddHours(11));
        int swapId = 0;
        try
        {
            var repo = new ShiftSwapRepository(db.ConnectionString);
            swapId = repo.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId, RequesterId = requesterId, ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING,
            });

            var results = repo.GetPendingSwapRequestsForColleague(colleagueId);

            Assert.Contains(results, r => r.SwapId == swapId);
            Assert.All(results, r => Assert.Equal(ShiftSwapRequestStatus.PENDING, r.Status));
        }
        finally
        {
            if (swapId > 0) db.DeleteSwapRequest(conn, swapId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    [Fact]
    public void UpdateShiftSwapRequestStatus_ReturnsTrueAndUpdatesStatus()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "Alice", "UpdateStatus", "Oncology");
        var colleagueId = db.InsertStaff(conn, "Doctor", "Bob", "UpdateStatus", "Oncology");
        var shiftId = db.InsertShift(conn, requesterId, "Ward D", DateTime.Now.AddHours(4), DateTime.Now.AddHours(12));
        int swapId = 0;
        try
        {
            var repo = new ShiftSwapRepository(db.ConnectionString);
            swapId = repo.CreateShiftSwapRequest(new ShiftSwapRequest
            {
                ShiftId = shiftId, RequesterId = requesterId, ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING,
            });

            var result = repo.UpdateShiftSwapRequestStatus(swapId, "ACCEPTED");

            Assert.True(result);
            Assert.Equal(ShiftSwapRequestStatus.ACCEPTED, repo.GetShiftSwapRequestById(swapId)!.Status);
        }
        finally
        {
            if (swapId > 0) db.DeleteSwapRequest(conn, swapId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    [Fact]
    public void ReassignShiftToStaff_ReturnsTrueAndUpdatesStaffOnShift()
    {
        using var conn = db.OpenConnection();
        var originalStaffId = db.InsertStaff(conn, "Doctor", "Alice", "Reassign", "Cardiology");
        var newStaffId = db.InsertStaff(conn, "Doctor", "Bob", "Reassign", "Cardiology");
        var shiftId = db.InsertShift(conn, originalStaffId, "Ward E", DateTime.Now.AddHours(5), DateTime.Now.AddHours(13));
        try
        {
            var repo = new ShiftSwapRepository(db.ConnectionString);

            var result = repo.ReassignShiftToStaff(shiftId, newStaffId);

            Assert.True(result);
            Assert.Equal(newStaffId, db.GetShiftStaffId(conn, shiftId));
        }
        finally
        {
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, originalStaffId);
            db.DeleteStaff(conn, newStaffId);
        }
    }

    [Fact]
    public void AddNotification_InsertsRecordInDatabase()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Alice", "Notification", "Cardiology");
        try
        {
            var repo = new ShiftSwapRepository(db.ConnectionString);

            repo.AddNotification(staffId, "Test Alert", "This is a test notification.");

            Assert.Equal(1, db.CountNotificationsForStaff(conn, staffId, "Test Alert"));
        }
        finally
        {
            db.DeleteNotificationsByStaff(conn, staffId);
            db.DeleteStaff(conn, staffId);
        }
    }
}
