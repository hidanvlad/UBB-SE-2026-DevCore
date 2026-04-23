using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.Services;

public class ShiftSwapServiceTests
{
    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenShiftMissing_ReturnsShiftNotFoundError()
    {
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(1)).Returns((Shift?)null);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.GetEligibleSwapColleaguesForShift(1, 1, out var error);

        Assert.Equal("Shift not found.", error);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenRequesterIsNotAppointed_LimitsToOwnShiftMessage()
    {
        var requesterDoctor = BuildDoctor(1, "Cardio");
        var appointedDoctor = BuildDoctor(2, "Cardio");
        var targetShift = new Shift(10, appointedDoctor, "ER", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(8), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(10)).Returns(targetShift);

        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);
        _ = service.GetEligibleSwapColleaguesForShift(1, 10, out var error);

        Assert.Equal("You can only request swap for your own shift.", error);
    }

    [Fact]
    public void GetIncomingSwapRequests_UsesRepositoryList()
    {
        var list = new List<ShiftSwapRequest> { new() { SwapId = 5, ColleagueId = 9, ShiftId = 1, RequesterId = 1, RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING } };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetPendingSwapRequestsForColleague(9)).Returns(list);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var result = service.GetIncomingSwapRequests(9);

        Assert.Same(list, result);
    }

    [Fact]
    public void AcceptSwapRequest_WhenSwapIdUnknown_ReturnsNotFoundMessage()
    {
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns((ShiftSwapRequest?)null);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 1, out var message);

        Assert.Equal("Swap request not found.", message);
    }

    [Fact]
    public void RejectSwapRequest_WhenValidPending_UpdatesStatusInRepository()
    {
        var pendingSwapRequest = new ShiftSwapRequest { SwapId = 1, ColleagueId = 2, RequesterId = 3, ShiftId = 4, Status = ShiftSwapRequestStatus.PENDING };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(pendingSwapRequest);
        string? updatedStatus = null;
        swap.Setup(shiftSwapRepository => shiftSwapRepository.UpdateShiftSwapRequestStatus(It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, string>((_, st) => updatedStatus = st)
            .Returns(true);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RejectSwapRequest(1, 2, out _);

        Assert.Equal("REJECTED", updatedStatus);
    }

    [Fact]
    public void RequestShiftSwap_WhenIneligibleColleague_ReturnsSelectionMessage()
    {
        var requesterDoctor = BuildDoctor(1, "Oncology");
        var futureShift = new Shift(100, requesterDoctor, "Ward", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { requesterDoctor });
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(100)).Returns(futureShift);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(false);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 100, 9, out var message);

        Assert.Equal("Selected colleague is not eligible (must be same profile and free in interval).", message);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenStartTimeInPast_DoesNotAllowFutureSwapMessage()
    {
        var doctor = BuildDoctor(1, "Derm");
        var past = new Shift(1, doctor, "ER", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1).AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(1)).Returns(past);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.GetEligibleSwapColleaguesForShift(1, 1, out var error);

        Assert.Equal("You can only request swap for a future shift.", error);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenRequesterMissingFromRoster_ExplainsRequesterNotFound()
    {
        var requester = BuildDoctor(1, "Ortho");
        var when = DateTime.UtcNow.AddDays(4);
        var st = new Shift(9, requester, "W1", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff>());
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(9)).Returns(st);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.GetEligibleSwapColleaguesForShift(1, 9, out var error);

        Assert.Equal("Requester not found.", error);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenPharmacistPeersShareCertification_ListsFreePeer()
    {
        var a = new Pharmacyst(1, "P1", "A", "e", true, "Sterile", 2);
        var b = new Pharmacyst(2, "P2", "B", "e", true, "Sterile", 2);
        var when = DateTime.UtcNow.AddDays(2);
        var s = new Shift(20, a, "Rx", when, when.AddHours(6), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, b });
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(20)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var colleagues = service.GetEligibleSwapColleaguesForShift(1, 20, out var err);

        Assert.Equal(2, colleagues.Single().StaffID);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenDoctorPeersShareSpecAndFree_ReturnsOneCandidate()
    {
        var a = BuildDoctor(1, "Gastro");
        var b = BuildDoctor(2, "Gastro");
        var when = DateTime.UtcNow.AddDays(3);
        var s = new Shift(30, a, "C", when, when.AddHours(3), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, b });
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(30)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var list = service.GetEligibleSwapColleaguesForShift(1, 30, out _);

        Assert.Equal(b.StaffID, list[0].StaffID);
    }

    [Fact]
    public void RequestShiftSwap_WhenCreateFails_ReturnsFailedToCreate()
    {
        var a = BuildDoctor(1, "Hema");
        var b = BuildDoctor(2, "Hema");
        var when = DateTime.UtcNow.AddDays(1);
        var s = new Shift(40, a, "C", when, when.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, b });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(a);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(40)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        swap.Setup(shiftSwapRepository => shiftSwapRepository.CreateShiftSwapRequest(It.IsAny<ShiftSwapRequest>())).Returns(0);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 40, 2, out var message);

        Assert.Equal("Failed to create shift swap request.", message);
    }

    [Fact]
    public void RequestShiftSwap_WhenRequesterResolvesToNull_ExplainsRequesterNotFound()
    {
        var a = BuildDoctor(1, "Hema");
        var b = BuildDoctor(2, "Hema");
        var when = DateTime.UtcNow.AddDays(1);
        var s = new Shift(41, a, "C", when, when.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, b });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns((IStaff?)null);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(41)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 41, 2, out var message);

        Assert.Equal("Requester not found.", message);
    }

    [Fact]
    public void RequestShiftSwap_WhenEligible_CreatesRequestAndSucceeds()
    {
        var a = BuildDoctor(1, "Hema");
        var b = BuildDoctor(2, "Hema");
        var when = DateTime.UtcNow.AddDays(1);
        var s = new Shift(42, a, "C", when, when.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, b });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(a);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(42)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        swap.Setup(shiftSwapRepository => shiftSwapRepository.CreateShiftSwapRequest(It.IsAny<ShiftSwapRequest>())).Returns(99);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 42, 2, out var message);

        Assert.Equal("Shift swap request sent successfully.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenColleagueIdMismatch_ReturnsYouCannotAccept()
    {
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 3,
            ShiftId = 4,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 9, out var message);

        Assert.Equal("You cannot accept this request.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenNotPending_ExplainsNoLongerPending()
    {
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 3,
            ShiftId = 4,
            Status = ShiftSwapRequestStatus.ACCEPTED
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("This request is no longer pending.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenShiftGone_ExplainsShiftNotFound()
    {
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 1,
            ShiftId = 55,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns((Shift?)null);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("Shift not found.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenColleagueAlreadyScheduled_ExplainsConflict()
    {
        var a = BuildDoctor(1, "X");
        var when = DateTime.UtcNow.AddDays(1);
        var s = new Shift(55, a, "C", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 1,
            ShiftId = 55,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(true);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("You are already scheduled to work in that interval.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenReassignReturnsFalse_ExplainsFailedReassign()
    {
        var a = BuildDoctor(1, "X");
        var when = DateTime.UtcNow.AddDays(1);
        var s = new Shift(55, a, "C", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 1,
            ShiftId = 55,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        swap.Setup(shiftSwapRepository => shiftSwapRepository.ReassignShiftToStaff(55, 2)).Returns(false);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("Failed to reassign shift.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenValid_UpdatesToAccepted()
    {
        var a = BuildDoctor(1, "X");
        var when = DateTime.UtcNow.AddDays(1);
        var s = new Shift(55, a, "C", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 1,
            ShiftId = 55,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns(s);
        shift.Setup(shiftRepository => shiftRepository.IsStaffWorkingDuring(2, s.StartTime, s.EndTime)).Returns(false);
        swap.Setup(shiftSwapRepository => shiftSwapRepository.ReassignShiftToStaff(55, 2)).Returns(true);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("Swap accepted.", message);
    }

    [Fact]
    public void RejectSwapRequest_WhenWrongColleague_ExplainsCannotReject()
    {
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 3,
            ShiftId = 4,
            Status = ShiftSwapRequestStatus.PENDING
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RejectSwapRequest(1, 3, out var message);

        Assert.Equal("You cannot reject this request.", message);
    }

    [Fact]
    public void RejectSwapRequest_WhenNotPending_ExplainsState()
    {
        var request = new ShiftSwapRequest
        {
            SwapId = 1,
            ColleagueId = 2,
            RequesterId = 3,
            ShiftId = 4,
            Status = ShiftSwapRequestStatus.REJECTED
        };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(request);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RejectSwapRequest(1, 2, out var message);

        Assert.Equal("This request is no longer pending.", message);
    }

    private static Doctor BuildDoctor(int id, string spec)
        => new(id, "A", "B", string.Empty, string.Empty, true, spec, "L-1", DoctorStatus.AVAILABLE, 1);
}
