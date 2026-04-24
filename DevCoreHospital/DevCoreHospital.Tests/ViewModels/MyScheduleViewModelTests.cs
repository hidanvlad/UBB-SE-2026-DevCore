using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Tests.Fakes;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Base;
using DevCoreHospital.ViewModels.Doctor;
using Xunit;
using MDoctor = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.Tests.ViewModels;

public class MyScheduleViewModelTests
{
    [Fact]
    public void SelectedDoctor_WhenSwitchedToSecondDoctor_ReloadsFutureShiftsForThatDoctor()
    {
        var doctorA = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "Card", "L", DoctorStatus.AVAILABLE, 1);
        var doctorB = new MDoctor(2, "B", "B", string.Empty, string.Empty, true, "Card", "L", DoctorStatus.AVAILABLE, 1);
        var futureTime = DateTime.UtcNow.AddDays(3);
        var shiftForDoctorA = new Shift(10, doctorA, "ER", futureTime, futureTime.AddHours(4), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService();
        service.AllDoctors.Add(doctorA);
        service.AllDoctors.Add(doctorB);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftForDoctorA };
        service.FutureShiftsByStaffId[2] = new List<Shift>
        {
            shiftForDoctorA,
            new Shift(11, doctorB, "ER", futureTime.AddDays(1), futureTime.AddDays(1).AddHours(4), ShiftStatus.SCHEDULED)
        };
        var viewModel = new MyScheduleViewModel(service);

        viewModel.SelectedDoctor = viewModel.Doctors[1];

        Assert.Equal(2, viewModel.FutureShifts.Count);
    }

    [Fact]
    public void SelectedShift_WhenSet_LoadsColleagueCountFromService()
    {
        var doctorA = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "Neuro", "L", DoctorStatus.AVAILABLE, 1);
        var doctorC = new MDoctor(2, "C", "C", string.Empty, string.Empty, true, "Neuro", "L", DoctorStatus.AVAILABLE, 1);
        var futureTime = DateTime.UtcNow.AddDays(4);
        var shiftOne = new Shift(10, doctorA, "W", futureTime, futureTime.AddHours(5), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService
        {
            EligibleError = string.Empty
        };
        service.AllDoctors.Add(doctorA);
        service.AllDoctors.Add(doctorC);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        service.EligibleColleagues.Add(doctorC);
        var viewModel = new MyScheduleViewModel(service);

        viewModel.SelectedShift = viewModel.FutureShifts.FirstOrDefault();

        Assert.Single(viewModel.EligibleColleagues);
    }

    [Fact]
    public void RequestSwapCommand_CanExecuteIsFalseWhenColleagueMissing()
    {
        var doctorA = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var futureTime = DateTime.UtcNow.AddDays(5);
        var shiftOne = new Shift(10, doctorA, "W", futureTime, futureTime.AddHours(3), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService { EligibleError = string.Empty };
        service.AllDoctors.Add(doctorA);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);
        viewModel.SelectedColleague = null;
        viewModel.SelectedShift = viewModel.FutureShifts[0];

        var result = ((RelayCommand)viewModel.RequestSwapCommand).CanExecute(null!);

        Assert.False(result);
    }

    [Fact]
    public void RequestSwap_PropagatesServiceMessage()
    {
        var doctorA = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var futureTime = DateTime.UtcNow.AddDays(6);
        var shiftOne = new Shift(10, doctorA, "W", futureTime, futureTime.AddHours(2), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService
        {
            RequestResult = true,
            RequestMessage = "Shift swap request sent successfully."
        };
        service.AllDoctors.Add(doctorA);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);
        viewModel.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "Col" };
        viewModel.SelectedShift = viewModel.FutureShifts[0];

        ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

        Assert.Equal("Shift swap request sent successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadDoctors_WhenStaffHasNoDoctors_SetsNoDoctorsMessage()
    {
        var service = new FakeShiftSwapService();
        var viewModel = new MyScheduleViewModel(service);

        Assert.Equal("No doctors found in database.", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadFutureShifts_WhenDoctorHasNoFutureSlots_SetsNoFutureShiftMessage()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "S", "L", DoctorStatus.AVAILABLE, 1);
        var past = new Shift(1, doc, "W", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2).AddHours(1), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService();
        service.AllDoctors.Add(doc);
        service.FutureShiftsByStaffId[1] = new List<Shift> { past };
        var viewModel = new MyScheduleViewModel(service);

        Assert.Equal("Selected doctor has no future shifts available for swap requests.", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadEligibleColleagues_WhenServiceReturnsError_ShowsServiceError()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "S", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(2);
        var shiftOne = new Shift(8, doc, "W", when, when.AddHours(1), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService { EligibleError = "Shift not found." };
        service.AllDoctors.Add(doc);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);

        viewModel.SelectedShift = viewModel.FutureShifts[0];

        Assert.Equal("Shift not found.", viewModel.StatusMessage);
    }

    [Fact]
    public void LoadEligibleColleagues_WhenNoPeersInProfile_ExplainsNoColleagues()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "S", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(2);
        var shiftOne = new Shift(8, doc, "W", when, when.AddHours(1), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService { EligibleError = string.Empty };
        service.AllDoctors.Add(doc);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);

        viewModel.SelectedShift = viewModel.FutureShifts[0];

        Assert.Equal("No colleagues available in the same role/department profile.", viewModel.StatusMessage);
    }

    [Fact]
    public void RequestSwapCommand_CanExecuteIsFalseWhenShiftMissing()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(3);
        var shiftOne = new Shift(10, doc, "W", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService { EligibleError = string.Empty };
        service.AllDoctors.Add(doc);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);
        viewModel.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "B" };
        viewModel.SelectedShift = null;

        var result = ((RelayCommand)viewModel.RequestSwapCommand).CanExecute(null!);

        Assert.False(result);
    }

    [Fact]
    public void RequestSwap_ClearsSelectedColleague_WhenServiceSucceeds()
    {
        var doctorA = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var futureTime = DateTime.UtcNow.AddDays(6);
        var shiftOne = new Shift(10, doctorA, "W", futureTime, futureTime.AddHours(2), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService
        {
            RequestResult = true,
            RequestMessage = "Shift swap request sent successfully."
        };
        service.AllDoctors.Add(doctorA);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);
        viewModel.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "Col" };
        viewModel.SelectedShift = viewModel.FutureShifts[0];

        ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

        Assert.Null(viewModel.SelectedColleague);
    }

    [Fact]
    public void RequestSwap_WhenRequiredSelectionMissing_ExplainsAllThree()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(3);
        var shiftOne = new Shift(10, doc, "W", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var service = new FakeShiftSwapService();
        service.AllDoctors.Add(doc);
        service.FutureShiftsByStaffId[1] = new List<Shift> { shiftOne };
        var viewModel = new MyScheduleViewModel(service);
        viewModel.SelectedColleague = new StaffOptionViewModel { StaffId = 1, DisplayName = "X" };
        viewModel.SelectedShift = null;

        ((RelayCommand)viewModel.RequestSwapCommand).Execute(null!);

        Assert.Equal("Please select doctor, shift and colleague.", viewModel.StatusMessage);
    }
}
