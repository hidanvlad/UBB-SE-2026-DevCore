using System;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Repositories;
using DevCoreHospital.ViewModels;
using Xunit;

namespace DevCoreHospital.Tests.Integration;

public class SalaryComputationIntegrationTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;

    public SalaryComputationIntegrationTests(SqlTestFixture database) => this.database = database;

    [Fact]
    public async Task RepoServiceViewModel_Integration_ComputesSalaryThroughAllLayers()
    {
        using var connection = database.OpenConnection();

        var doctorId = database.InsertStaff(connection, "Doctor", "SalaryDoc", "Integration",
            specialization: "Emergency medicine", yearsExp: 6);

        var shiftStart = new DateTime(2026, 5, 1, 8, 0, 0);
        var shiftId    = database.InsertShift(connection, doctorId, "Ward A", shiftStart, shiftStart.AddHours(8));

        var hangoutId = database.InsertHangout(connection, "May Integration Hangout", new DateTime(2026, 5, 10));
        database.InsertHangoutParticipant(connection, hangoutId, doctorId);

        try
        {
            var doctor = new Doctor { StaffID = doctorId, Specialization = "Emergency medicine", YearsOfExperience = 6 };
            var shift  = new Shift(shiftId, doctor, "Ward A", shiftStart, shiftStart.AddHours(8), ShiftStatus.SCHEDULED);

            var repository = new SalaryRepository(database.ConnectionString);
            var service    = new SalaryComputationService(repository);
            var viewModel  = new SalaryComputationViewModel(service, new IStaff[] { doctor }, new[] { shift })
            {
                SelectedStaff = doctor,
                SelectedMonth = 5,
                SelectedYear  = 2026,
            };

            await viewModel.ComputeSalaryCommand.ExecuteAsync();

            Assert.Equal($"Computed Salary: $871{GetSeparator()}08", viewModel.SalaryResult);
            Assert.Equal(string.Empty, viewModel.ErrorMessage);
        }
        finally
        {
            database.DeleteHangoutParticipants(connection, hangoutId);
            database.DeleteHangout(connection, hangoutId);
            database.DeleteShift(connection, shiftId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    private static string GetSeparator()
        => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
}
