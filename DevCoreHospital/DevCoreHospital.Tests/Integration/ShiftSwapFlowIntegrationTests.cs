using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Repositories;
using DevCoreHospital.ViewModels.Base;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DevCoreHospital.Tests.Integration;

public class ShiftSwapFlowIntegrationTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;

    public ShiftSwapFlowIntegrationTests(SqlTestFixture db) => this.db = db;

    // -----------------------------------------------------------------------
    // Test 1: no pending requests → inbox is empty
    // -----------------------------------------------------------------------
    [Fact]
    public void IncomingRequests_WhenNoSwapRequestsExistForColleague_InboxIsEmpty()
    {
        using var conn = db.OpenConnection();
        var colleagueId = db.InsertStaff(conn, "Doctor", "InbEmpty", "Colleague", "Cardiology");
        try
        {
            var staffRepo = new StaffRepository(db.ConnectionString);
            var shiftRepo = new ShiftRepository(db.ConnectionString, staffRepo);
            var swapRepo  = new ShiftSwapRepository(db.ConnectionString);
            var service   = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);

            // Use the IEnumerable overload so we control exactly which doctor is selected,
            // without relying on alphabetical position across the whole DB.
            var incoming = new IncomingSwapRequestsViewModel(
                service,
                new[] { new DoctorOptionViewModel { StaffId = colleagueId, DisplayName = "InbEmpty Colleague" } });

            Assert.Empty(incoming.Requests);
        }
        finally
        {
            db.DeleteStaff(conn, colleagueId);
        }
    }

    // -----------------------------------------------------------------------
    // Test 2: one real pending row in the DB → inbox shows exactly one item
    // -----------------------------------------------------------------------
    [Fact]
    public void IncomingRequests_WhenOnePendingRequestExists_InboxHasSingleItem()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "InbOne", "Requester", "Cardiology");
        var colleagueId = db.InsertStaff(conn, "Doctor", "InbOne", "Colleague", "Cardiology");
        var start   = DateTime.Today.AddDays(30).AddHours(9);
        var shiftId = db.InsertShift(conn, requesterId, "ER", start, start.AddHours(8));
        var swapId  = 0;
        try
        {
            swapId = InsertSwapRequest(conn, shiftId, requesterId, colleagueId);

            var staffRepo = new StaffRepository(db.ConnectionString);
            var shiftRepo = new ShiftRepository(db.ConnectionString, staffRepo);
            var swapRepo  = new ShiftSwapRepository(db.ConnectionString);
            var service   = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);

            var incoming = new IncomingSwapRequestsViewModel(
                service,
                new[] { new DoctorOptionViewModel { StaffId = colleagueId, DisplayName = "InbOne Colleague" } });

            Assert.Single(incoming.Requests);
        }
        finally
        {
            db.DeleteSwapRequest(conn, swapId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    // -----------------------------------------------------------------------
    // Test 3: executing RequestSwapCommand writes a real row to ShiftSwapRequests
    // -----------------------------------------------------------------------
    [Fact]
    public void RequestSwapCommand_WhenAllConditionsMet_CreatesSwapRequestInDatabase()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "SwapReq",  "Requester", "SwapTestSpec");
        var colleagueId = db.InsertStaff(conn, "Doctor", "SwapReq",  "Colleague", "SwapTestSpec");
        var start   = DateTime.Today.AddDays(35).AddHours(9);
        var shiftId = db.InsertShift(conn, requesterId, "ER", start, start.AddHours(8));
        try
        {
            var staffRepo = new StaffRepository(db.ConnectionString);
            var shiftRepo = new ShiftRepository(db.ConnectionString, staffRepo);
            var swapRepo  = new ShiftSwapRepository(db.ConnectionString);
            var service   = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);
            var viewModel = new MyScheduleViewModel(service);

            viewModel.SelectedDoctor   = viewModel.Doctors.First(d => d.StaffId == requesterId);
            viewModel.SelectedShift    = viewModel.FutureShifts.First(s => s.Id == shiftId);
            viewModel.SelectedColleague = viewModel.EligibleColleagues.First(c => c.StaffId == colleagueId);

            ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

            var pending = swapRepo.GetSwapRequestsForColleague(colleagueId);
            Assert.Contains(pending, r => r.ShiftId == shiftId && r.RequesterId == requesterId);
        }
        finally
        {
            db.DeleteSwapRequestsByShift(conn, shiftId);
            db.DeleteNotificationsByStaff(conn, colleagueId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    // -----------------------------------------------------------------------
    // Test 4: after a successful submit the status message contains "successfully"
    // -----------------------------------------------------------------------
    [Fact]
    public void RequestSwapCommand_WhenAllConditionsMet_SetsSuccessStatusMessage()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "SwapMsg",  "Requester", "SwapMsgSpec");
        var colleagueId = db.InsertStaff(conn, "Doctor", "SwapMsg",  "Colleague", "SwapMsgSpec");
        var start   = DateTime.Today.AddDays(36).AddHours(9);
        var shiftId = db.InsertShift(conn, requesterId, "ER", start, start.AddHours(8));
        try
        {
            var staffRepo = new StaffRepository(db.ConnectionString);
            var shiftRepo = new ShiftRepository(db.ConnectionString, staffRepo);
            var swapRepo  = new ShiftSwapRepository(db.ConnectionString);
            var service   = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);
            var viewModel = new MyScheduleViewModel(service);

            viewModel.SelectedDoctor    = viewModel.Doctors.First(d => d.StaffId == requesterId);
            viewModel.SelectedShift     = viewModel.FutureShifts.First(s => s.Id == shiftId);
            viewModel.SelectedColleague = viewModel.EligibleColleagues.First(c => c.StaffId == colleagueId);

            ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

            Assert.Contains("successfully", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            db.DeleteSwapRequestsByShift(conn, shiftId);
            db.DeleteNotificationsByStaff(conn, colleagueId);
            db.DeleteShift(conn, shiftId);
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, colleagueId);
        }
    }

    // -----------------------------------------------------------------------
    // Helper: insert a PENDING swap request directly (no service layer)
    // -----------------------------------------------------------------------
    private static int InsertSwapRequest(SqlConnection conn, int shiftId, int requesterId, int colleagueId)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
            VALUES (@ShiftId, @RequesterId, @ColleagueId, @RequestedAt, 'PENDING');
            SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
        cmd.Parameters.AddWithValue("@ShiftId",     shiftId);
        cmd.Parameters.AddWithValue("@RequesterId", requesterId);
        cmd.Parameters.AddWithValue("@ColleagueId", colleagueId);
        cmd.Parameters.AddWithValue("@RequestedAt", DateTime.UtcNow);
        return (int)cmd.ExecuteScalar()!;
    }
}
