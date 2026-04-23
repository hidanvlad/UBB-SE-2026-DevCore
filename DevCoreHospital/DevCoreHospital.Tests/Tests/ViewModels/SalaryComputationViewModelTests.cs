using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.Tests.ViewModels;

public class SalaryComputationViewModelTests
{
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

    private static SalaryComputationViewModel CreateViewModelWithStubService()
    {
        var salaryService = new Mock<ISalaryComputationService>(MockBehavior.Strict);
        return new SalaryComputationViewModel(salaryService.Object, [], []);
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
