using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.ViewModels;

public class SalaryComputationViewModelTests
{
    private static SalaryComputationViewModel CreateViewModelWithTwoStaffAndTwoShifts(out Doctor doctor, out Pharmacyst pharmacist)
    {
        doctor = new Doctor { StaffID = 10, FirstName = "D", LastName = "One" };
        pharmacist = new Pharmacyst { StaffID = 11, FirstName = "P", LastName = "Two" };
        var shifts = new[]
        {
            CreateShift(1, doctor, new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 16, 0, 0)),
            CreateShift(2, pharmacist, new DateTime(2026, 5, 2, 8, 0, 0), new DateTime(2026, 5, 2, 16, 0, 0)),
        };
        return new SalaryComputationViewModel(CreateStrictSalaryService().Object, new IStaff[] { doctor, pharmacist }, shifts);
    }

    [Fact]
    public void Constructor_WhenInjectedWithStaff_PopulatesStaffListWithBothMembers()
    {
        var viewModel = CreateViewModelWithTwoStaffAndTwoShifts(out var doctor, out var pharmacist);
        Assert.Equal(new IStaff[] { doctor, pharmacist }, viewModel.StaffList);
    }

    [Fact]
    public void Constructor_WhenInjectedWithShifts_PopulatesShiftListWithBothShifts()
    {
        var viewModel = CreateViewModelWithTwoStaffAndTwoShifts(out _, out _);
        Assert.Equal(2, viewModel.ShiftList.Count);
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
    public void CanComputeSalary_ReturnsFalse_WhenStaffIdIsNegative()
    {
        var viewModel = CreateViewModelWithStubService();
        viewModel.SelectedStaff = new TestStaff { StaffID = -1 };

        Assert.False(viewModel.ComputeSalaryCommand.CanExecute(null));
    }

    [Fact]
    public void CanComputeSalary_ReturnsTrue_WhenStaffIsValid()
    {
        var viewModel = CreateViewModelWithStubService();
        viewModel.SelectedStaff = new TestStaff { StaffID = 101 };

        Assert.True(viewModel.ComputeSalaryCommand.CanExecute(null));
    }

    private static async Task<SalaryComputationViewModel> ExecuteDoctorComputationReturning(double computedSalary)
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(service => service.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 5, 2026))
            .ReturnsAsync(computedSalary);

        var doctor = new Doctor { StaffID = 1, FirstName = "A", LastName = "B", YearsOfExperience = 3 };
        var shift = CreateShift(11, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { doctor }, [shift])
        {
            SelectedStaff = doctor,
            SelectedMonth = 5,
            SelectedYear = 2026,
        };
        await viewModel.ComputeSalaryCommand.ExecuteAsync();
        return viewModel;
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenDoctorComputationSucceeds_FormatsSalaryResult()
    {
        var viewModel = await ExecuteDoctorComputationReturning(1234.5);
        Assert.Equal($"Computed Salary: $1234{GetSeparator()}50", viewModel.SalaryResult);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenDoctorComputationSucceeds_ClearsErrorMessage()
    {
        var viewModel = await ExecuteDoctorComputationReturning(1234.5);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenDoctorComputationSucceeds_ClearsLoadingFlag()
    {
        var viewModel = await ExecuteDoctorComputationReturning(1234.5);
        Assert.False(viewModel.IsLoading);
    }

    private static async Task<List<Shift>?> CaptureShiftsPassedToDoctorComputation()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        List<Shift>? capturedShifts = null;

        void CaptureShifts(Doctor _, List<Shift> shifts, int month, int year) { capturedShifts = shifts; }
        salaryService.Setup(service => service.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 5, 2026))
            .Callback<Doctor, List<Shift>, int, int>(CaptureShifts)
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
            SelectedYear = 2026,
        };
        await viewModel.ComputeSalaryCommand.ExecuteAsync();
        return capturedShifts;
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenDoctorPathInvoked_PassesSingleShiftMatchingStaffMonthAndYear()
    {
        var capturedShifts = await CaptureShiftsPassedToDoctorComputation();
        Assert.Single(capturedShifts!);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenDoctorPathInvoked_PassesShiftWithMatchingId()
    {
        var capturedShifts = await CaptureShiftsPassedToDoctorComputation();
        Assert.Equal(100, capturedShifts![0].Id);
    }

    private static async Task<SalaryComputationViewModel> ExecutePharmacistComputationReturning(double computedSalary)
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(service => service.ComputeSalaryPharmacistAsync(It.IsAny<Pharmacyst>(), It.IsAny<List<Shift>>(), 6, 2026))
            .ReturnsAsync(computedSalary);

        var pharmacist = new Pharmacyst { StaffID = 2, FirstName = "P", LastName = "H", YearsOfExperience = 4 };
        var shift = CreateShift(12, pharmacist, new DateTime(2026, 6, 1, 8, 0, 0), new DateTime(2026, 6, 1, 16, 0, 0));
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { pharmacist }, [shift])
        {
            SelectedStaff = pharmacist,
            SelectedMonth = 6,
            SelectedYear = 2026,
        };
        await viewModel.ComputeSalaryCommand.ExecuteAsync();
        return viewModel;
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenPharmacistComputationSucceeds_FormatsSalaryResult()
    {
        var viewModel = await ExecutePharmacistComputationReturning(987.65);
        Assert.Equal($"Computed Salary: $987{GetSeparator()}65", viewModel.SalaryResult);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenPharmacistComputationSucceeds_ClearsLoadingFlag()
    {
        var viewModel = await ExecutePharmacistComputationReturning(987.65);
        Assert.False(viewModel.IsLoading);
    }

    private static async Task<SalaryComputationViewModel> ExecuteWithUnsupportedStaffType()
    {
        var salaryService = new Mock<ISalaryComputationService>(MockBehavior.Strict);
        var viewModel = new SalaryComputationViewModel(salaryService.Object, [], [])
        {
            SelectedStaff = new TestStaff { StaffID = 3 },
            SelectedMonth = 5,
            SelectedYear = 2026,
        };
        await viewModel.ComputeSalaryCommand.ExecuteAsync();
        return viewModel;
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenStaffTypeUnsupported_SetsUnsupportedTypeErrorMessage()
    {
        var viewModel = await ExecuteWithUnsupportedStaffType();
        Assert.Contains("Unsupported staff type", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenStaffTypeUnsupported_ClearsSalaryResult()
    {
        var viewModel = await ExecuteWithUnsupportedStaffType();
        Assert.Equal(string.Empty, viewModel.SalaryResult);
    }

    private static async Task<SalaryComputationViewModel> ExecuteDoctorComputationThatThrows()
    {
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(service => service.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 8, 2026))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var doctor = new Doctor { StaffID = 40, FirstName = "Err", LastName = "Doc" };
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { doctor }, [])
        {
            SelectedStaff = doctor,
            SelectedMonth = 8,
            SelectedYear = 2026,
        };
        await viewModel.ComputeSalaryCommand.ExecuteAsync();
        return viewModel;
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenServiceThrows_ErrorMessageContainsExceptionText()
    {
        var viewModel = await ExecuteDoctorComputationThatThrows();
        Assert.Contains("boom", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ComputeSalaryAsync_WhenServiceThrows_ClearsSalaryResult()
    {
        var viewModel = await ExecuteDoctorComputationThatThrows();
        Assert.Equal(string.Empty, viewModel.SalaryResult);
    }

    [Fact]
    public async Task ComputeSalaryAsync_TransitionsIsLoadingAndFormatsSalaryResult()
    {
        var taskCompletionSource = new TaskCompletionSource<double>();
        var salaryService = new Mock<ISalaryComputationService>();
        salaryService.Setup(service => service.ComputeSalaryDoctorAsync(It.IsAny<Doctor>(), It.IsAny<List<Shift>>(), 7, 2026))
            .Returns(taskCompletionSource.Task);

        var doctor = new Doctor { StaffID = 4, YearsOfExperience = 2 };
        var viewModel = new SalaryComputationViewModel(salaryService.Object, new IStaff[] { doctor }, [])
        {
            SelectedStaff = doctor,
            SelectedMonth = 7,
            SelectedYear = 2026
        };

        var execution = viewModel.ComputeSalaryCommand.ExecuteAsync();
        Assert.True(viewModel.IsLoading);

        taskCompletionSource.SetResult(4567.8);
        await execution;

        Assert.Equal($"Computed Salary: $4567{GetSeparator()}80", viewModel.SalaryResult);
        Assert.False(viewModel.IsLoading);
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
