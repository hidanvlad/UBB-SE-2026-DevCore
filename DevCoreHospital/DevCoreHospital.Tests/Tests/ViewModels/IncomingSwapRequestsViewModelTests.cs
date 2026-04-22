using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Fakes;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Doctor;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.ViewModels;

public class IncomingSwapRequestsViewModelTests
{
    [Fact]
    public void LoadRequests_WhenDoctorCleared_ExplainsSelectDoctor()
    {
        var service = new Mock<IShiftSwapService>();
        service.Setup(shiftSwapService => shiftSwapService.GetIncomingSwapRequests(1)).Returns(new List<ShiftSwapRequest>());
        var vm = new IncomingSwapRequestsViewModel(service.Object, new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });

        vm.SelectedDoctor = null;

        Assert.Equal("Select doctor first.", vm.StatusMessage);
    }

    [Fact]
    public void Constructor_WhenNoRequests_LoadStatusShowsNoPending()
    {
        var service = new Mock<IShiftSwapService>();
        service.Setup(shiftSwapService => shiftSwapService.GetIncomingSwapRequests(1)).Returns(new List<ShiftSwapRequest>());
        var vm = new IncomingSwapRequestsViewModel(service.Object, new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });

        Assert.Equal("No pending requests.", vm.StatusMessage);
    }

    [Fact]
    public void Constructor_WhenListPopulated_ShowsRequestCountInStatus()
    {
        var service = new Mock<IShiftSwapService>();
        var list = new List<ShiftSwapRequest>
        {
            new() { SwapId = 1, ShiftId = 2, RequesterId = 3, ColleagueId = 1, RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING }
        };
        service.Setup(shiftSwapService => shiftSwapService.GetIncomingSwapRequests(1)).Returns(list);
        var vm = new IncomingSwapRequestsViewModel(service.Object, new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });

        Assert.Equal("1 pending request(s).", vm.StatusMessage);
    }

    [Fact]
    public void AcceptCommand_WhenSelectedRequestNull_DoesNotInvokeServiceUnderStrictMock()
    {
        var service = new Mock<IShiftSwapService>(MockBehavior.Strict);
        service.Setup(shiftSwapService => shiftSwapService.GetIncomingSwapRequests(1)).Returns(new List<ShiftSwapRequest>());
        var vm = new IncomingSwapRequestsViewModel(service.Object, new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });
        vm.Requests.Add(new IncomingSwapRequestItemViewModel { SwapId = 9, ShiftId = 1, RequesterId = 1 });
        vm.SelectedRequest = null;

        var ex = Record.Exception(() => ((RelayCommand)vm.AcceptCommand).Execute(null!));

        Assert.Null(ex);
    }

    [Fact]
    public void RejectCommand_WhenSelectedRequestNull_DoesNotInvokeServiceUnderStrictMock()
    {
        var service = new Mock<IShiftSwapService>(MockBehavior.Strict);
        service.Setup(shiftSwapService => shiftSwapService.GetIncomingSwapRequests(1)).Returns(new List<ShiftSwapRequest>());
        var viewModel = new IncomingSwapRequestsViewModel(service.Object, new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });
        viewModel.Requests.Add(new IncomingSwapRequestItemViewModel { SwapId = 9, ShiftId = 1, RequesterId = 1 });
        viewModel.SelectedRequest = null;

        var ex = Record.Exception(() => ((RelayCommand)viewModel.RejectCommand).Execute(null!));

        Assert.Null(ex);
    }

    [Fact]
    public void AcceptCommand_WhenServiceSucceeds_ReloadsWithEmptyInboxMessage()
    {
        var service = new FakeShiftSwapService();
        service.PendingInbox.Add(
            new ShiftSwapRequest
            {
                SwapId = 1,
                ShiftId = 1,
                RequesterId = 1,
                ColleagueId = 1,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING
            });
        service.AcceptResult = true;
        service.AcceptMessage = "Swap accepted.";
        service.ReturningEmptyInboxOnSecondGetIncoming = true;
        var viewModel = new IncomingSwapRequestsViewModel(
            service,
            new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });
        viewModel.SelectedRequest = viewModel.Requests[0];

        ((RelayCommand)viewModel.AcceptCommand).Execute(null!);

        Assert.Equal("No pending requests.", viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptCommand_WhenServiceReturnsFalse_KeepsRejectionTextOnStatusLine()
    {
        var service = new FakeShiftSwapService();
        service.PendingInbox.Add(
            new ShiftSwapRequest
            {
                SwapId = 1,
                ShiftId = 1,
                RequesterId = 1,
                ColleagueId = 1,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING
            });
        service.AcceptResult = false;
        service.AcceptMessage = "Swap request not found.";
        var viewModel = new IncomingSwapRequestsViewModel(
            service,
            new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });
        viewModel.SelectedRequest = viewModel.Requests[0];

        ((RelayCommand)viewModel.AcceptCommand).Execute(null!);

        Assert.Equal("Swap request not found.", viewModel.StatusMessage);
    }

    [Fact]
    public void RejectCommand_WhenServiceSucceeds_ReloadsWithEmptyInboxMessage()
    {
        var service = new FakeShiftSwapService();
        service.PendingInbox.Add(
            new ShiftSwapRequest
            {
                SwapId = 1,
                ShiftId = 1,
                RequesterId = 1,
                ColleagueId = 1,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING
            });
        service.RejectResult = true;
        service.RejectMessage = "Swap rejected.";
        service.ReturningEmptyInboxOnSecondGetIncoming = true;
        var viewModel = new IncomingSwapRequestsViewModel(
            service,
            new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });
        viewModel.SelectedRequest = viewModel.Requests[0];

        ((RelayCommand)viewModel.RejectCommand).Execute(null!);

        Assert.Equal("No pending requests.", viewModel.StatusMessage);
    }

    [Fact]
    public void RejectCommand_WhenServiceReturnsFalse_KeepsRejectionTextOnStatusLine()
    {
        var service = new FakeShiftSwapService();
        service.PendingInbox.Add(
            new ShiftSwapRequest
            {
                SwapId = 1,
                ShiftId = 1,
                RequesterId = 1,
                ColleagueId = 1,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING
            });
        service.RejectResult = false;
        service.RejectMessage = "This request is no longer pending.";
        var viewModel = new IncomingSwapRequestsViewModel(
            service,
            new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });
        viewModel.SelectedRequest = viewModel.Requests[0];

        ((RelayCommand)viewModel.RejectCommand).Execute(null!);

        Assert.Equal("This request is no longer pending.", viewModel.StatusMessage);
    }

    [Fact]
    public void Constructor_WhenTwoPendingRequests_UsesPluralCountInStatus()
    {
        var service = new Mock<IShiftSwapService>();
        var list = new List<ShiftSwapRequest>
        {
            new() { SwapId = 1, ShiftId = 1, RequesterId = 1, ColleagueId = 1, RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING },
            new() { SwapId = 2, ShiftId = 1, RequesterId = 1, ColleagueId = 1, RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING }
        };
        service.Setup(shiftSwapService => shiftSwapService.GetIncomingSwapRequests(1)).Returns(list);
        var viewModel = new IncomingSwapRequestsViewModel(service.Object, new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "D" } });

        Assert.Equal("2 pending request(s).", viewModel.StatusMessage);
    }
}
