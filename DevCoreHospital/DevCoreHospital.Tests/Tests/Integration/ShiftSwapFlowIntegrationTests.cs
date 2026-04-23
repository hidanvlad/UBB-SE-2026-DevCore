using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Doctor;
using Moq;
using Xunit;
using MDoctor = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.Tests.Integration;

public class ShiftSwapFlowIntegrationTests
{
    [Fact]
    public void When_the_swap_repository_has_nothing_for_that_colleague_incoming_inbox_stays_empty()
    {
        var shiftSwap = new Mock<IShiftSwapRepository>();
        shiftSwap
            .Setup(shiftSwapRepository => shiftSwapRepository.GetPendingSwapRequestsForColleague(3))
            .Returns(new List<ShiftSwapRequest>());
        var staff = new Mock<IStaffRepository>();
        var shifts = new Mock<IShiftRepository>();
        var throughService = new ShiftSwapService(staff.Object, shifts.Object, shiftSwap.Object);
        var incoming = new IncomingSwapRequestsViewModel(
            throughService,
            new[] { new DoctorOptionViewModel { StaffId = 3, DisplayName = "On-call" } });

        Assert.Equal(0, incoming.Requests.Count);
    }

    [Fact]
    public void When_the_swap_repository_returns_a_pending_row_incoming_binds_a_single_list_item()
    {
        var pending = new ShiftSwapRequest
        {
            SwapId = 1,
            ShiftId = 2,
            ColleagueId = 1,
            RequesterId = 3,
            RequestedAt = DateTime.UtcNow,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var shiftSwap = new Mock<IShiftSwapRepository>();
        shiftSwap
            .Setup(shiftSwapRepository => shiftSwapRepository.GetPendingSwapRequestsForColleague(1))
            .Returns(new List<ShiftSwapRequest> { pending });
        var staff = new Mock<IStaffRepository>();
        var shifts = new Mock<IShiftRepository>();
        var throughService = new ShiftSwapService(staff.Object, shifts.Object, shiftSwap.Object);
        var incoming = new IncomingSwapRequestsViewModel(
            throughService,
            new[] { new DoctorOptionViewModel { StaffId = 1, DisplayName = "A" } });

        Assert.Equal(1, incoming.Requests.Count);
    }

    [Fact]
    public void Submitting_a_request_swap_uses_create_on_the_configured_shift_swap_repository()
    {
        var requester = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "Sp", "L", DoctorStatus.AVAILABLE, 1);
        var peer = new MDoctor(2, "B", "B", string.Empty, string.Empty, true, "Sp", "L", DoctorStatus.AVAILABLE, 1);
        var windowStart = DateTime.UtcNow.AddDays(5);
        var future = new Shift(50, requester, "ER", windowStart, windowStart.AddHours(4), ShiftStatus.SCHEDULED);
        var createCalls = 0;
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { requester, peer });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(requester);
        var shiftRepository = new Mock<IShiftRepository>();
        shiftRepository.Setup(mockedShifts => mockedShifts.GetShiftsByStaffID(1)).Returns(new List<Shift> { future });
        shiftRepository.Setup(mockedShifts => mockedShifts.GetShiftById(50)).Returns(future);
        shiftRepository.Setup(mockedShifts => mockedShifts.IsStaffWorkingDuring(2, windowStart, windowStart.AddHours(4))).Returns(false);
        var shiftSwap = new Mock<IShiftSwapRepository>();
        shiftSwap
            .Setup(shiftSwapRepository => shiftSwapRepository.CreateShiftSwapRequest(It.IsAny<ShiftSwapRequest>()))
            .Callback(() => createCalls++)
            .Returns(1);
        var throughService = new ShiftSwapService(staff.Object, shiftRepository.Object, shiftSwap.Object);
        var mySchedule = new MyScheduleViewModel(throughService, shiftRepository.Object, staff.Object);
        mySchedule.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "B" };
        mySchedule.SelectedShift = mySchedule.FutureShifts[0];

        ((RelayCommand)mySchedule.RequestSwapCommand).Execute(null!);

        Assert.Equal(1, createCalls);
    }

    [Fact]
    public void A_successful_submitted_request_swap_drops_the_user_on_the_familiar_confirmation_in_status()
    {
        var requester = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "Sp", "L", DoctorStatus.AVAILABLE, 1);
        var peer = new MDoctor(2, "B", "B", string.Empty, string.Empty, true, "Sp", "L", DoctorStatus.AVAILABLE, 1);
        var windowStart = DateTime.UtcNow.AddDays(5);
        var future = new Shift(50, requester, "ER", windowStart, windowStart.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { requester, peer });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(requester);
        var shiftRepository = new Mock<IShiftRepository>();
        shiftRepository.Setup(mockedShifts => mockedShifts.GetShiftsByStaffID(1)).Returns(new List<Shift> { future });
        shiftRepository.Setup(mockedShifts => mockedShifts.GetShiftById(50)).Returns(future);
        shiftRepository.Setup(mockedShifts => mockedShifts.IsStaffWorkingDuring(2, windowStart, windowStart.AddHours(4))).Returns(false);
        var shiftSwap = new Mock<IShiftSwapRepository>();
        shiftSwap
            .Setup(shiftSwapRepository => shiftSwapRepository.CreateShiftSwapRequest(It.IsAny<ShiftSwapRequest>()))
            .Returns(1);
        var throughService = new ShiftSwapService(staff.Object, shiftRepository.Object, shiftSwap.Object);
        var mySchedule = new MyScheduleViewModel(throughService, shiftRepository.Object, staff.Object);
        mySchedule.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "B" };
        mySchedule.SelectedShift = mySchedule.FutureShifts[0];

        ((RelayCommand)mySchedule.RequestSwapCommand).Execute(null!);

        Assert.Contains("successfully", mySchedule.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
