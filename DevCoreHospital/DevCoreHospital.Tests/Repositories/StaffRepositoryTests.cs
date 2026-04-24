using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class StaffRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public StaffRepositoryTests(SqlTestFixture database) => this.database = database;

    [Fact]
    public void LoadAllStaff_WhenConnectionFails_ReturnsEmptyList()
        => Assert.Empty(new StaffRepository(InvalidConnectionString).LoadAllStaff());

    [Fact]
    public void GetStaffById_WhenConnectionFails_ReturnsNull()
        => Assert.Null(new StaffRepository(InvalidConnectionString).GetStaffById(1));

    [Fact]
    public void GetPharmacists_WhenConnectionFails_ReturnsEmptyList()
        => Assert.Empty(new StaffRepository(InvalidConnectionString).GetPharmacists());

    [Fact]
    public void GetAvailableDoctors_WhenConnectionFails_ReturnsEmptyList()
        => Assert.Empty(new StaffRepository(InvalidConnectionString).GetAvailableDoctors());

    [Fact]
    public void GetDoctorsBySpecialization_WhenConnectionFails_ReturnsEmptyList()
        => Assert.Empty(new StaffRepository(InvalidConnectionString).GetDoctorsBySpecialization("Cardiology"));

    [Fact]
    public void GetPharmacystsByCertification_WhenConnectionFails_ReturnsEmptyList()
        => Assert.Empty(new StaffRepository(InvalidConnectionString).GetPharmacystsByCertification("Sterile Compounding"));

    [Fact]
    public void UpdateStaffAvailability_WhenConnectionFails_DoesNotThrow()
    {
        var exception = Record.Exception(() => new StaffRepository(InvalidConnectionString).UpdateStaffAvailability(999, true, DoctorStatus.AVAILABLE));

        Assert.Null(exception);
    }

    [Fact]
    public void LoadAllStaff_ReturnsDoctorInsertedInDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Alice", "LoadAll", "Cardiology");
        try
        {
            Assert.Contains(new StaffRepository(database.ConnectionString).LoadAllStaff(), staff => staff.StaffID == staffId);
        }
        finally
        {
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void GetStaffById_ReturnsCorrectStaff()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Bob", "GetById", "Neurology");
        try
        {
            var result = new StaffRepository(database.ConnectionString).GetStaffById(staffId);

            Assert.NotNull(result);
            Assert.Equal(staffId, result!.StaffID);
            Assert.Equal("Bob", result.FirstName);
        }
        finally
        {
            database.DeleteStaff(connection, staffId);
        }
    }

    [Fact]
    public void GetAvailableDoctors_ReturnsOnlyAvailableDoctors()
    {
        using var connection = database.OpenConnection();
        var availableId = database.InsertStaff(connection, "Doctor", "Carol", "AvailDoc", "Oncology", status: "Available", isAvailable: true);
        var unavailableId = database.InsertStaff(connection, "Doctor", "Dave", "AvailDoc", "Oncology", status: "Off_Duty", isAvailable: false);
        try
        {
            var result = new StaffRepository(database.ConnectionString).GetAvailableDoctors();

            Assert.Contains(result, doctor => doctor.StaffID == availableId);
            Assert.DoesNotContain(result, doctor => doctor.StaffID == unavailableId);
        }
        finally
        {
            database.DeleteStaff(connection, availableId);
            database.DeleteStaff(connection, unavailableId);
        }
    }

    [Fact]
    public void GetPharmacists_ReturnsPharmacistInsertedInDatabase()
    {
        using var connection = database.OpenConnection();
        var pharmacistId = database.InsertStaff(connection, "Pharmacist", "Eve", "GetPharm", certification: "PharmD");
        try
        {
            Assert.Contains(new StaffRepository(database.ConnectionString).GetPharmacists(), pharmacist => pharmacist.StaffID == pharmacistId);
        }
        finally
        {
            database.DeleteStaff(connection, pharmacistId);
        }
    }

    [Fact]
    public void GetDoctorsBySpecialization_ReturnsOnlyMatchingSpecialization()
    {
        using var connection = database.OpenConnection();
        var cardiologistId = database.InsertStaff(connection, "Doctor", "Frank", "BySpec", "Cardiology");
        var neurologistId = database.InsertStaff(connection, "Doctor", "Grace", "BySpec", "Neurology");
        try
        {
            var result = new StaffRepository(database.ConnectionString).GetDoctorsBySpecialization("Cardiology");

            Assert.Contains(result, doctor => doctor.StaffID == cardiologistId);
            Assert.DoesNotContain(result, doctor => doctor.StaffID == neurologistId);
        }
        finally
        {
            database.DeleteStaff(connection, cardiologistId);
            database.DeleteStaff(connection, neurologistId);
        }
    }

    [Fact]
    public void GetPharmacystsByCertification_ReturnsOnlyMatchingCertification()
    {
        using var connection = database.OpenConnection();
        var bcpsId = database.InsertStaff(connection, "Pharmacist", "Henry", "ByCert", certification: "BCPS");
        var pharmDId = database.InsertStaff(connection, "Pharmacist", "Iris", "ByCert", certification: "PharmD");
        try
        {
            var result = new StaffRepository(database.ConnectionString).GetPharmacystsByCertification("BCPS");

            Assert.Contains(result, pharmacist => pharmacist.StaffID == bcpsId);
            Assert.DoesNotContain(result, pharmacist => pharmacist.StaffID == pharmDId);
        }
        finally
        {
            database.DeleteStaff(connection, bcpsId);
            database.DeleteStaff(connection, pharmDId);
        }
    }

    [Fact]
    public void UpdateStaffAvailability_UpdatesAvailabilityAndStatusInDatabase()
    {
        using var connection = database.OpenConnection();
        var staffId = database.InsertStaff(connection, "Doctor", "Mia", "UpdateAvail", "Cardiology", status: "Available", isAvailable: true);
        try
        {
            var repository = new StaffRepository(database.ConnectionString);

            repository.UpdateStaffAvailability(staffId, false, DoctorStatus.OFF_DUTY);

            var updated = repository.GetStaffById(staffId) as Doctor;
            Assert.NotNull(updated);
            Assert.False(updated!.Available);
            Assert.Equal(DoctorStatus.OFF_DUTY, updated.DoctorStatus);
        }
        finally
        {
            database.DeleteStaff(connection, staffId);
        }
    }

}
