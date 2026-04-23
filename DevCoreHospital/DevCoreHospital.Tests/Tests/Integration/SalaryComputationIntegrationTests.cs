using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.Tests.Integration;

public class SalaryComputationIntegrationTests
{
    [Fact]
    public async Task RepoServiceViewModel_Integration_ComputesSalaryThroughAllLayers()
    {
        var doctor = new Doctor { StaffID = 50, Specialization = "Emergency medicine", YearsOfExperience = 6 };
        var shift = CreateShift(500, doctor, new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 16, 0, 0));
        var repoMock = new Mock<SalaryRepository>("fake");

        repoMock.Setup(r => r.GetShiftHoursFromDb(500)).Returns(8);
        repoMock.Setup(r => r.DidStaffParticipateInHangout(50, 5, 2026)).Returns(true);

        var service = new SalaryComputationService(repoMock.Object);
        var viewModel = new SalaryComputationViewModel(service, new IStaff[] { doctor }, [shift])
        {
            SelectedStaff = doctor,
            SelectedMonth = 5,
            SelectedYear = 2026
        };

        await viewModel.ComputeSalaryCommand.ExecuteAsync();

        Assert.Equal($"Computed Salary: $871{GetSeparator()}08", viewModel.SalaryResult);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    private static Shift CreateShift(int id, IStaff staff, DateTime start, DateTime end)
    {
        return new Shift(id, staff, "Ward A", start, end, ShiftStatus.SCHEDULED);
    }

    private static string GetSeparator() => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
}
