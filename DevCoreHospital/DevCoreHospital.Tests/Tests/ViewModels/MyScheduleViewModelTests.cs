using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Tests.Fakes;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Doctor;
using Moq;
using Xunit;
using MDoctor = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.Tests.ViewModels;

public class MyScheduleViewModelTests
{
    [Fact]
    public void SelectedDoctor_WhenSwitchedToSecondDoctor_ReloadsFutureShiftsForThatDoctor()
    {
        var a = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "Card", "L", DoctorStatus.AVAILABLE, 1);
        var b = new MDoctor(2, "B", "B", string.Empty, string.Empty, true, "Card", "L", DoctorStatus.AVAILABLE, 1);
        var t = DateTime.UtcNow.AddDays(3);
        var s1a = new Shift(10, a, "ER", t, t.AddHours(4), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, b });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { s1a });
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(2)).Returns(new List<Shift>
        {
            s1a,
            new Shift(11, b, "ER", t.AddDays(1), t.AddDays(1).AddHours(4), ShiftStatus.SCHEDULED)
        });
        var service = new FakeShiftSwapService();
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);

        vm.SelectedDoctor = vm.Doctors[1];

        Assert.Equal(2, vm.FutureShifts.Count);
    }

    [Fact]
    public void SelectedShift_WhenSet_LoadsColleagueCountFromService()
    {
        var a = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "Neuro", "L", DoctorStatus.AVAILABLE, 1);
        var c = new MDoctor(2, "C", "C", string.Empty, string.Empty, true, "Neuro", "L", DoctorStatus.AVAILABLE, 1);
        var t = DateTime.UtcNow.AddDays(4);
        var shift1 = new Shift(10, a, "W", t, t.AddHours(5), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a, c });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { shift1 });
        var service = new FakeShiftSwapService
        {
            EligibleError = string.Empty
        };
        service.EligibleColleagues.Add(c);
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);

        vm.SelectedShift = vm.FutureShifts.FirstOrDefault();

        Assert.Equal(1, vm.EligibleColleagues.Count);
    }

    [Fact]
    public void RequestSwapCommand_CanExecuteIsFalseWhenColleagueMissing()
    {
        var a = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var t = DateTime.UtcNow.AddDays(5);
        var sh1 = new Shift(10, a, "W", t, t.AddHours(3), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { sh1 });
        var service = new FakeShiftSwapService { EligibleError = string.Empty };
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);
        vm.SelectedColleague = null;
        vm.SelectedShift = vm.FutureShifts[0];

        var can = ((RelayCommand)vm.RequestSwapCommand).CanExecute(null!);

        Assert.False(can);
    }

    [Fact]
    public void RequestSwap_PropagatesServiceMessage()
    {
        var a = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var t = DateTime.UtcNow.AddDays(6);
        var sh1 = new Shift(10, a, "W", t, t.AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { a });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { sh1 });
        var service = new FakeShiftSwapService
        {
            RequestResult = true,
            RequestMessage = "Shift swap request sent successfully."
        };
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);
        vm.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "Col" };
        vm.SelectedShift = vm.FutureShifts[0];

        ((RelayCommand)vm.RequestSwapCommand).Execute(null!);

        Assert.Equal("Shift swap request sent successfully.", vm.StatusMessage);
    }

    [Fact]
    public void LoadDoctors_WhenStaffHasNoDoctors_SetsNoDoctorsMessage()
    {
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff>());
        var sh = new Mock<IShiftRepository>();
        var service = new FakeShiftSwapService();
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);

        Assert.Equal("No doctors found in database.", vm.StatusMessage);
    }

    [Fact]
    public void LoadFutureShifts_WhenDoctorHasNoFutureSlots_SetsNoFutureShiftMessage()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "S", "L", DoctorStatus.AVAILABLE, 1);
        var past = new Shift(1, doc, "W", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2).AddHours(1), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { doc });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { past });
        var service = new FakeShiftSwapService();
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);

        Assert.Equal("Selected doctor has no future shifts available for swap requests.", vm.StatusMessage);
    }

    [Fact]
    public void LoadEligibleColleagues_WhenServiceReturnsError_ShowsServiceError()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "S", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(2);
        var one = new Shift(8, doc, "W", when, when.AddHours(1), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { doc });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { one });
        var service = new FakeShiftSwapService { EligibleError = "Shift not found." };
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);

        vm.SelectedShift = vm.FutureShifts[0];

        Assert.Equal("Shift not found.", vm.StatusMessage);
    }

    [Fact]
    public void LoadEligibleColleagues_WhenNoPeersInProfile_ExplainsNoColleagues()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "S", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(2);
        var one = new Shift(8, doc, "W", when, when.AddHours(1), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { doc });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { one });
        var service = new FakeShiftSwapService { EligibleError = string.Empty };
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);

        vm.SelectedShift = vm.FutureShifts[0];

        Assert.Equal("No colleagues available in the same role/department profile.", vm.StatusMessage);
    }

    [Fact]
    public void RequestSwapCommand_CanExecuteIsFalseWhenShiftMissing()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(3);
        var sh1 = new Shift(10, doc, "W", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { doc });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { sh1 });
        var service = new FakeShiftSwapService { EligibleError = string.Empty };
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);
        vm.SelectedColleague = new StaffOptionViewModel { StaffId = 2, DisplayName = "B" };
        vm.SelectedShift = null;

        var can = ((RelayCommand)vm.RequestSwapCommand).CanExecute(null!);

        Assert.False(can);
    }

    [Fact]
    public void RequestSwap_WhenRequiredSelectionMissing_ExplainsAllThree()
    {
        var doc = new MDoctor(1, "A", "A", string.Empty, string.Empty, true, "E", "L", DoctorStatus.AVAILABLE, 1);
        var when = DateTime.UtcNow.AddDays(3);
        var sh1 = new Shift(10, doc, "W", when, when.AddHours(2), ShiftStatus.SCHEDULED);
        var staff = new Mock<IStaffRepository>();
        staff.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { doc });
        var sh = new Mock<IShiftRepository>();
        sh.Setup(shiftRepository => shiftRepository.GetShiftsByStaffID(1)).Returns(new List<Shift> { sh1 });
        var service = new FakeShiftSwapService();
        var vm = new MyScheduleViewModel(service, sh.Object, staff.Object);
        vm.SelectedColleague = new StaffOptionViewModel { StaffId = 1, DisplayName = "X" };
        vm.SelectedShift = null;

        ((RelayCommand)vm.RequestSwapCommand).Execute(null!);

        Assert.Equal("Please select doctor, shift and colleague.", vm.StatusMessage);
    }
}
