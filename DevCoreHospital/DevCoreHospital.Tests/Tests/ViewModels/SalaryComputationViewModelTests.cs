using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;
using System.Reflection;

namespace DevCoreHospital.Tests.Tests.ViewModels;

public class SalaryComputationViewModelTests
{
    [Fact]
    public void Constructor_WithInjectedCollections_PopulatesStaffAndShiftLists()
    {
        var doctor = new Doctor { StaffID = 10, FirstName = "D", LastName = "One" };
        var pharmacist = new Pharmacyst { StaffID = 11, FirstName = "P", LastName = "Two" };
        var shifts = new[]
        {
            CreateShift(1, doctor, new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 16, 0, 0)),
            CreateShift(2, pharmacist, new DateTime(2026, 5, 2, 8, 0, 0), new DateTime(2026, 5, 2, 16, 0, 0))
        };

        var viewModel = new SalaryComputationViewModel(CreateStrictSalaryService().Object, new IStaff[] { doctor, pharmacist }, shifts);

        Assert.Equal(2, viewModel.StaffList.Count);
        Assert.Equal(2, viewModel.ShiftList.Count);
        Assert.Contains(doctor, viewModel.StaffList);
        Assert.Contains(pharmacist, viewModel.StaffList);
    }

    [Fact]
    public void CanComputeSalary_ReturnsFalse_WhenStaffIsNullOrIdInvalid()
    {
        var viewModel = CreateViewModelWithStubService();

        Assert.False(viewModel.ComputeSalaryCommand.CanExecute(null));

        viewModel.SelectedStaff = new TestStaff { StaffID = 0 };
        Assert.False(viewModel.ComputeSalaryCommand.CanExecute(null));
    }

    [Fact]
    public void CanComputeSalary_ReturnsTrue_WhenStaffIsValid()
    {
        var viewModel = CreateViewModelWithStubService();
        viewModel.SelectedStaff = new TestStaff { StaffID = 101 };

        Assert.True(viewModel.ComputeSalaryCommand.CanExecute(null));
    }

    [Fact]
    public async Task ComputeSalaryAsync_DoctorPath_SetsSalaryResultAndClearsLoading()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(s => s.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 5, 2026))
            .ReturnsAsync(1234.5);

        var doctor = new Doctor { StaffID = 1, FirstName = "A", LastName = "B", YearsOfExperience = 3 };
        var shift = CreateShift(11, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { doctor }, [shift])
        {
            SelectedStaff = doctor,
            SelectedMonth = 5,
            SelectedYear = 2026
        };

        await viewModel.ComputeSalaryCommand.ExecuteAsync();

        Assert.Equal($"Computed Salary: $1234{GetSeparator()}50", viewModel.SalaryResult);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task ComputeSalaryAsync_DoctorPath_FiltersShiftsByStaffMonthAndYear()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        List<Shift>? capturedShifts = null;

        salaryService.Setup(s => s.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 5, 2026))
            .Callback<Doctor, List<Shift>, int, int>((_, shifts, _, _) => capturedShifts = shifts)
            .ReturnsAsync(1000);

        var selectedDoctor = new Doctor { StaffID = 21, FirstName = "Sel", LastName = "Doc" };
        var otherDoctor = new Doctor { StaffID = 22, FirstName = "Other", LastName = "Doc" };

        var matchingShift = CreateShift(100, selectedDoctor, new DateTime(2026, 5, 3, 8, 0, 0), new DateTime(2026, 5, 3, 16, 0, 0));
        var wrongMonthShift = CreateShift(101, selectedDoctor, new DateTime(2026, 6, 3, 8, 0, 0), new DateTime(2026, 6, 3, 16, 0, 0));
        var wrongYearShift = CreateShift(102, selectedDoctor, new DateTime(2025, 5, 3, 8, 0, 0), new DateTime(2025, 5, 3, 16, 0, 0));
        var wrongStaffShift = CreateShift(103, otherDoctor, new DateTime(2026, 5, 3, 8, 0, 0), new DateTime(2026, 5, 3, 16, 0, 0));

        var viewModel = new SalaryComputationViewModel(
            salaryService.Object,
            new IStaff[] { selectedDoctor, otherDoctor },
            [matchingShift, wrongMonthShift, wrongYearShift, wrongStaffShift])
        {
            SelectedStaff = selectedDoctor,
            SelectedMonth = 5,
            SelectedYear = 2026
        };

        await viewModel.ComputeSalaryCommand.ExecuteAsync();

        Assert.NotNull(capturedShifts);
        Assert.Single(capturedShifts!);
        Assert.Equal(100, capturedShifts![0].Id);
    }

    [Fact]
    public async Task ComputeSalaryAsync_PharmacistPath_SetsSalaryResult()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(s => s.ComputeSalaryPharmacistAsync(It.IsAny<Pharmacyst>(), It.IsAny<List<Shift>>(), 6, 2026))
            .ReturnsAsync(987.65);

        var pharmacist = new Pharmacyst { StaffID = 2, FirstName = "P", LastName = "H", YearsOfExperience = 4 };
        var shift = CreateShift(12, pharmacist, new DateTime(2026, 6, 1, 8, 0, 0), new DateTime(2026, 6, 1, 16, 0, 0));
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { pharmacist }, [shift])
        {
            SelectedStaff = pharmacist,
            SelectedMonth = 6,
            SelectedYear = 2026
        };

        await viewModel.ComputeSalaryCommand.ExecuteAsync();

        Assert.Equal($"Computed Salary: $987{GetSeparator()}65", viewModel.SalaryResult);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task ComputeSalaryAsync_UnsupportedStaffType_SetsErrorMessage()
    {
        var salaryService = new Mock<ISalaryComputationService>(MockBehavior.Strict);
        var viewModel = new SalaryComputationViewModel(salaryService.Object, [], [])
        {
            SelectedStaff = new TestStaff { StaffID = 3 },
            SelectedMonth = 5,
            SelectedYear = 2026
        };

        await viewModel.ComputeSalaryCommand.ExecuteAsync();

        Assert.Contains("Unsupported staff type", viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.SalaryResult);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenServiceThrows_SetsComputationFailedError()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(s => s.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 8, 2026))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var doctor = new Doctor { StaffID = 40, FirstName = "Err", LastName = "Doc" };
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { doctor }, [])
        {
            SelectedStaff = doctor,
            SelectedMonth = 8,
            SelectedYear = 2026
        };

        await viewModel.ComputeSalaryCommand.ExecuteAsync();

        Assert.Contains("Computation failed", viewModel.ErrorMessage);
        Assert.Contains("boom", viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.SalaryResult);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task ComputeSalaryAsync_TransitionsIsLoadingAndFormatsSalaryResult()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(s => s.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 7, 2026))
            .Returns(async () =>
            {
                await Task.Delay(10);
                return 4567.8;
            });

        var doctor = new Doctor { StaffID = 4, YearsOfExperience = 2 };
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { doctor }, [])
        {
            SelectedStaff = doctor,
            SelectedMonth = 7,
            SelectedYear = 2026
        };

        var execution = viewModel.ComputeSalaryCommand.ExecuteAsync();
        Assert.True(viewModel.IsLoading);
        await execution;

        Assert.Equal($"Computed Salary: $4567{GetSeparator()}80", viewModel.SalaryResult);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void LoadStaffList_WhenRepositoryIsNull_ReturnsWithoutThrowing()
    {
        var viewModel = CreateViewModelWithStubService();

        var method = typeof(SalaryComputationViewModel).GetMethod("LoadStaffList", BindingFlags.Instance | BindingFlags.NonPublic);
        var exception = Record.Exception(() => method!.Invoke(viewModel, null));

        Assert.Null(exception);
    }

    [Fact]
    public void LoadShiftList_WhenRepositoryIsNull_ReturnsWithoutThrowing()
    {
        var viewModel = CreateViewModelWithStubService();

        var method = typeof(SalaryComputationViewModel).GetMethod("LoadShiftList", BindingFlags.Instance | BindingFlags.NonPublic);
        var exception = Record.Exception(() => method!.Invoke(viewModel, null));

        Assert.Null(exception);
    }

    [Fact]
    public void TestStaff_Properties_CanBeReadAndWritten()
    {
        var staff = new TestStaff
        {
            StaffID = 55,
            FirstName = "Test",
            LastName = "Staff",
            ContactInfo = "test@hospital.local",
            Available = true
        };

        Assert.Equal(55, staff.StaffID);
        Assert.Equal("Test", staff.FirstName);
        Assert.Equal("Staff", staff.LastName);
        Assert.Equal("test@hospital.local", staff.ContactInfo);
        Assert.True(staff.Available);

        staff.StaffID = 56;
        staff.FirstName = "Updated";
        staff.LastName = "Person";
        staff.ContactInfo = "updated@hospital.local";
        staff.Available = false;

        Assert.Equal(56, staff.StaffID);
        Assert.Equal("Updated", staff.FirstName);
        Assert.Equal("Person", staff.LastName);
        Assert.Equal("updated@hospital.local", staff.ContactInfo);
        Assert.False(staff.Available);
    }

    private static SalaryComputationViewModel CreateViewModelWithStubService()
    {
        var salaryService = CreateStrictSalaryService();
        return new SalaryComputationViewModel(salaryService.Object, [], []);
    }

    private static Mock<ISalaryComputationService> CreateStrictSalaryService()
    {
        return new Mock<ISalaryComputationService>(MockBehavior.Strict);
    }

    private static Shift CreateShift(int id, IStaff staff, DateTime start, DateTime end)
    {
        return new Shift(id, staff, "Ward A", start, end, ShiftStatus.SCHEDULED);
    }

    private static string GetSeparator() => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    private sealed class TestStaff : IStaff
    {
        public int StaffID { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public bool Available { get; set; }
    }
}
