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
    private readonly SqlTestFixture database;

    public ShiftSwapFlowIntegrationTests(SqlTestFixture database) => this.database = database;

    [Fact]
    public void IncomingRequests_WhenNoSwapRequestsExistForColleague_InboxIsEmpty()
    {
        using var connection = database.OpenConnection();
        var colleagueId = database.InsertStaff(connection, "Doctor", "InbEmpty", "Colleague", "Cardiology");
        try
        {
            var staffRepo = new StaffRepository(database.ConnectionString);
            var shiftRepo = new ShiftRepository(database.ConnectionString);
            var swapRepo = new ShiftSwapRepository(database.ConnectionString);
            var service = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);

            var incoming = new IncomingSwapRequestsViewModel(
                service,
                new[] { new DoctorOptionViewModel { StaffId = colleagueId, DisplayName = "InbEmpty Colleague" } });

            Assert.Empty(incoming.Requests);
        }
        finally
        {
            database.DeleteStaff(connection, colleagueId);
        }
    }

    [Fact]
    public void IncomingRequests_WhenOnePendingRequestExists_InboxHasSingleItem()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "InbOne", "Requester", "Cardiology");
        var colleagueId = database.InsertStaff(connection, "Doctor", "InbOne", "Colleague", "Cardiology");
        var start = DateTime.Today.AddDays(30).AddHours(9);
        var shiftId = database.InsertShift(connection, requesterId, "ER", start, start.AddHours(8));
        var swapRequestId = 0;
        try
        {
            swapRequestId = InsertSwapRequest(connection, shiftId, requesterId, colleagueId);

            var staffRepo = new StaffRepository(database.ConnectionString);
            var shiftRepo = new ShiftRepository(database.ConnectionString);
            var swapRepo = new ShiftSwapRepository(database.ConnectionString);
            var service = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);

            var incoming = new IncomingSwapRequestsViewModel(
                service,
                new[] { new DoctorOptionViewModel { StaffId = colleagueId, DisplayName = "InbOne Colleague" } });

            Assert.Single(incoming.Requests);
        }
        finally
        {
            database.DeleteSwapRequest(connection, swapRequestId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }

    [Fact]
    public void RequestSwapCommand_WhenAllConditionsMet_CreatesSwapRequestInDatabase()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "SwapReq", "Requester", "SwapTestSpec");
        var colleagueId = database.InsertStaff(connection, "Doctor", "SwapReq", "Colleague", "SwapTestSpec");
        var start = DateTime.Today.AddDays(35).AddHours(9);
        var shiftId = database.InsertShift(connection, requesterId, "ER", start, start.AddHours(8));
        try
        {
            var staffRepo = new StaffRepository(database.ConnectionString);
            var shiftRepo = new ShiftRepository(database.ConnectionString);
            var swapRepo = new ShiftSwapRepository(database.ConnectionString);
            var service = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);
            var viewModel = new MyScheduleViewModel(service);

            bool IsRequester(DoctorOptionViewModel doctor) => doctor.StaffId == requesterId;
            bool IsTargetShift(DoctorShiftItemViewModel shiftItem) => shiftItem.Id == shiftId;
            bool IsColleague(StaffOptionViewModel colleague) => colleague.StaffId == colleagueId;
            viewModel.SelectedDoctor = viewModel.Doctors.First(IsRequester);
            viewModel.SelectedShift = viewModel.FutureShifts.First(IsTargetShift);
            viewModel.SelectedColleague = viewModel.EligibleColleagues.First(IsColleague);

            ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

            var pending = swapRepo.GetSwapRequestsForColleague(colleagueId);
            bool IsMatchingRequest(ShiftSwapRequest swapRequest) => swapRequest.ShiftId == shiftId && swapRequest.RequesterId == requesterId;
            Assert.Contains(pending, IsMatchingRequest);
        }
        finally
        {
            database.DeleteSwapRequestsByShift(connection, shiftId);
            database.DeleteNotificationsByStaff(connection, colleagueId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }


    [Fact]
    public void RequestSwapCommand_WhenAllConditionsMet_SetsSuccessStatusMessage()
    {
        using var connection = database.OpenConnection();
        var requesterId = database.InsertStaff(connection, "Doctor", "SwapMsg", "Requester", "SwapMsgSpec");
        var colleagueId = database.InsertStaff(connection, "Doctor", "SwapMsg", "Colleague", "SwapMsgSpec");
        var start = DateTime.Today.AddDays(36).AddHours(9);
        var shiftId = database.InsertShift(connection, requesterId, "ER", start, start.AddHours(8));
        try
        {
            var staffRepo = new StaffRepository(database.ConnectionString);
            var shiftRepo = new ShiftRepository(database.ConnectionString);
            var swapRepo = new ShiftSwapRepository(database.ConnectionString);
            var service = new ShiftSwapService(staffRepo, shiftRepo, swapRepo);
            var viewModel = new MyScheduleViewModel(service);

            bool IsRequester(DoctorOptionViewModel doctor) => doctor.StaffId == requesterId;
            bool IsTargetShift(DoctorShiftItemViewModel shiftItem) => shiftItem.Id == shiftId;
            bool IsColleague(StaffOptionViewModel colleague) => colleague.StaffId == colleagueId;
            viewModel.SelectedDoctor = viewModel.Doctors.First(IsRequester);
            viewModel.SelectedShift = viewModel.FutureShifts.First(IsTargetShift);
            viewModel.SelectedColleague = viewModel.EligibleColleagues.First(IsColleague);

            ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

            Assert.Contains("successfully", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            database.DeleteSwapRequestsByShift(connection, shiftId);
            database.DeleteNotificationsByStaff(connection, colleagueId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, requesterId);
            database.DeleteStaff(connection, colleagueId);
        }
    }

    private static int InsertSwapRequest(SqlConnection connection, int shiftId, int requesterId, int colleagueId)
    {
        using var command = new SqlCommand(@"
            INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
            VALUES (@ShiftId, @RequesterId, @ColleagueId, @RequestedAt, 'PENDING');
            SELECT CAST(SCOPE_IDENTITY() AS INT);", connection);
        command.Parameters.AddWithValue("@ShiftId", shiftId);
        command.Parameters.AddWithValue("@RequesterId", requesterId);
        command.Parameters.AddWithValue("@ColleagueId", colleagueId);
        command.Parameters.AddWithValue("@RequestedAt", DateTime.UtcNow);
        return (int)command.ExecuteScalar()!;
    }
}
