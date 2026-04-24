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
    public void GetIncomingSwapRequests_ReturnsPendingRequestsForColleague()
    {
        var pending = new ShiftSwapRequest { SwapId = 5, ColleagueId = 9, ShiftId = 1, RequesterId = 1, RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.PENDING };
        var accepted = new ShiftSwapRequest { SwapId = 6, ColleagueId = 9, ShiftId = 2, RequesterId = 1, RequestedAt = DateTime.UtcNow, Status = ShiftSwapRequestStatus.ACCEPTED };
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetSwapRequestsForColleague(9))
            .Returns(new List<ShiftSwapRequest> { pending, accepted });
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var result = service.GetIncomingSwapRequests(9);

        Assert.Single(result);
        Assert.Equal(5, result[0].SwapId);
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

        void CaptureStatus(int swapId, string status) { updatedStatus = status; }

        swap.Setup(shiftSwapRepository => shiftSwapRepository.UpdateShiftSwapRequestStatus(It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, string>(CaptureStatus)
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
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 100, 9, out var message);

        Assert.Equal("Selected colleague is not eligible (must be same profile and free in interval).", message);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenStartTimeInPast_DoesNotAllowFutureSwapMessage()
    {
        var doctor = BuildDoctor(1, "Derm");
        var pastShift = new Shift(1, doctor, "ER", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1).AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(1)).Returns(pastShift);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.GetEligibleSwapColleaguesForShift(1, 1, out var error);

        Assert.Equal("You can only request swap for a future shift.", error);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenRequesterMissingFromRoster_ExplainsRequesterNotFound()
    {
        var requester = BuildDoctor(1, "Ortho");
        var futureTime = DateTime.UtcNow.AddDays(4);
        var targetShift = new Shift(9, requester, "W1", futureTime, futureTime.AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff>());
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(9)).Returns(targetShift);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.GetEligibleSwapColleaguesForShift(1, 9, out var error);

        Assert.Equal("Requester not found.", error);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenPharmacistPeersShareCertification_ListsFreePeer()
    {
        var firstPharmacist = new Pharmacyst(1, "P1", "A", "e", true, "Sterile", 2);
        var secondPharmacist = new Pharmacyst(2, "P2", "B", "e", true, "Sterile", 2);
        var futureTime = DateTime.UtcNow.AddDays(2);
        var targetShift = new Shift(20, firstPharmacist, "Rx", futureTime, futureTime.AddHours(6), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { firstPharmacist, secondPharmacist });
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(20)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var colleagues = service.GetEligibleSwapColleaguesForShift(1, 20, out var eligibilityError);

        Assert.Equal(2, colleagues.Single().StaffID);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenDoctorPeersShareSpecAndFree_ReturnsOneCandidate()
    {
        var firstDoctor = BuildDoctor(1, "Gastro");
        var secondDoctor = BuildDoctor(2, "Gastro");
        var futureTime = DateTime.UtcNow.AddDays(3);
        var targetShift = new Shift(30, firstDoctor, "C", futureTime, futureTime.AddHours(3), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { firstDoctor, secondDoctor });
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(30)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var list = service.GetEligibleSwapColleaguesForShift(1, 30, out _);

        Assert.Equal(secondDoctor.StaffID, list[0].StaffID);
    }

    [Fact]
    public void RequestShiftSwap_WhenCreateFails_ReturnsFailedToCreate()
    {
        var firstDoctor = BuildDoctor(1, "Hema");
        var secondDoctor = BuildDoctor(2, "Hema");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(40, firstDoctor, "C", futureTime, futureTime.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { firstDoctor, secondDoctor });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(firstDoctor);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(40)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
        swap.Setup(shiftSwapRepository => shiftSwapRepository.CreateShiftSwapRequest(It.IsAny<ShiftSwapRequest>())).Returns(0);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 40, 2, out var message);

        Assert.Equal("Failed to create shift swap request.", message);
    }

    [Fact]
    public void RequestShiftSwap_WhenRequesterResolvesToNull_ExplainsRequesterNotFound()
    {
        var firstDoctor = BuildDoctor(1, "Hema");
        var secondDoctor = BuildDoctor(2, "Hema");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(41, firstDoctor, "C", futureTime, futureTime.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { firstDoctor, secondDoctor });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns((IStaff?)null);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(41)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 41, 2, out var message);

        Assert.Equal("Requester not found.", message);
    }

    [Fact]
    public void RequestShiftSwap_WhenEligible_CreatesRequestAndSucceeds()
    {
        var firstDoctor = BuildDoctor(1, "Hema");
        var secondDoctor = BuildDoctor(2, "Hema");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(42, firstDoctor, "C", futureTime, futureTime.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { firstDoctor, secondDoctor });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(firstDoctor);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(42)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
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
        var firstDoctor = BuildDoctor(1, "X");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(55, firstDoctor, "C", futureTime, futureTime.AddHours(2), ShiftStatus.SCHEDULED);
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
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift> { targetShift });
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("You are already scheduled to work in that interval.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenReassignReturnsFalse_ExplainsFailedReassign()
    {
        var firstDoctor = BuildDoctor(1, "X");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(55, firstDoctor, "C", futureTime, futureTime.AddHours(2), ShiftStatus.SCHEDULED);
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
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
        swap.Setup(shiftSwapRepository => shiftSwapRepository.ReassignShiftToStaff(55, 2)).Returns(false);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.AcceptSwapRequest(1, 2, out var message);

        Assert.Equal("Failed to reassign shift.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenValid_UpdatesToAccepted()
    {
        var firstDoctor = BuildDoctor(1, "X");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(55, firstDoctor, "C", futureTime, futureTime.AddHours(2), ShiftStatus.SCHEDULED);
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
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(55)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
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

    [Fact]
    public void RequestShiftSwap_WhenSuccessful_SendsNotificationToColleague()
    {
        var firstDoctor = BuildDoctor(1, "Hema");
        var secondDoctor = BuildDoctor(2, "Hema");
        var futureTime = DateTime.UtcNow.AddDays(1);
        var targetShift = new Shift(43, firstDoctor, "C", futureTime, futureTime.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { firstDoctor, secondDoctor });
        staff.Setup(staffRepository => staffRepository.GetStaffById(1)).Returns(firstDoctor);
        shift.Setup(shiftRepository => shiftRepository.GetShiftById(43)).Returns(targetShift);
        shift.Setup(shiftRepository => shiftRepository.GetShiftsForStaffInRange(2, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<Shift>());
        swap.Setup(shiftSwapRepository => shiftSwapRepository.CreateShiftSwapRequest(It.IsAny<ShiftSwapRequest>())).Returns(99);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RequestShiftSwap(1, 43, 2, out _);

        swap.Verify(shiftSwapRepository => shiftSwapRepository.AddNotification(
            2, "New Shift Swap Request", It.Is<string>(msg => msg.Contains("43"))), Times.Once);
    }

    [Fact]
    public void RejectSwapRequest_WhenValid_SendsNotificationToRequester()
    {
        var pendingSwapRequest = new ShiftSwapRequest
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
        swap.Setup(shiftSwapRepository => shiftSwapRepository.GetShiftSwapRequestById(1)).Returns(pendingSwapRequest);
        swap.Setup(shiftSwapRepository => shiftSwapRepository.UpdateShiftSwapRequestStatus(It.IsAny<int>(), It.IsAny<string>())).Returns(true);
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        _ = service.RejectSwapRequest(1, 2, out _);

        swap.Verify(shiftSwapRepository => shiftSwapRepository.AddNotification(
            3, "Shift Swap Rejected", It.Is<string>(msg => msg.Contains("1"))), Times.Once);
    }

    [Fact]
    public void GetFutureShiftsForStaff_ReturnsOnlyShiftsWithFutureStartTime()
    {
        var doctor = BuildDoctor(1, "Cardiology");
        var futureShift = new Shift(1, doctor, "ER", DateTime.Now.AddDays(2), DateTime.Now.AddDays(2).AddHours(8), ShiftStatus.SCHEDULED);
        var pastShift = new Shift(2, doctor, "ER", DateTime.Now.AddDays(-1), DateTime.Now.AddDays(-1).AddHours(8), ShiftStatus.COMPLETED);

        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        shift.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { futureShift, pastShift });
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var result = service.GetFutureShiftsForStaff(1);

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void GetFutureShiftsForStaff_ReturnsEmptyList_WhenNoFutureShiftsExist()
    {
        var doctor = BuildDoctor(2, "Neurology");
        var pastShift = new Shift(10, doctor, "Ward A", DateTime.Now.AddDays(-3), DateTime.Now.AddDays(-3).AddHours(8), ShiftStatus.COMPLETED);

        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        shift.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(2)).Returns(new List<Shift> { pastShift });
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var result = service.GetFutureShiftsForStaff(2);

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllDoctors_ReturnsOnlyDoctorsFromStaffRepository()
    {
        var doctor = BuildDoctor(1, "Cardiology");
        var pharmacist = new Pharmacyst(2, "P", "H", string.Empty, true, "General", 1);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff())
            .Returns(new List<IStaff> { doctor, pharmacist });
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var result = service.GetAllDoctors();

        Assert.Single(result);
        Assert.Equal(1, result[0].StaffID);
    }

    [Fact]
    public void GetAllDoctors_ReturnsEmptyList_WhenNoStaffAreDoctors()
    {
        var pharmacist = new Pharmacyst(1, "P", "H", string.Empty, true, "General", 1);
        var staff = new Mock<IStaffRepository>();
        var shift = new Mock<IShiftRepository>();
        var swap = new Mock<IShiftSwapRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff())
            .Returns(new List<IStaff> { pharmacist });
        var service = new ShiftSwapService(staff.Object, shift.Object, swap.Object);

        var result = service.GetAllDoctors();

        Assert.Empty(result);
    }

    private static Doctor BuildDoctor(int staffId, string specialization)
        => new(staffId, "A", "B", string.Empty, string.Empty, true, specialization, "L-1", DoctorStatus.AVAILABLE, 1);
}
