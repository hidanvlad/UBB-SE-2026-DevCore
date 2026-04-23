using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class StaffRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;
    private const string InvalidConnectionString = "InvalidConnectionString";

    public StaffRepositoryTests(SqlTestFixture db) => this.db = db;

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
    public void UpdateStaffAvailability_WhenStaffNotInCache_DoesNotThrow()
    {
        var ex = Record.Exception(() => new StaffRepository(InvalidConnectionString).UpdateStaffAvailability(999, true, DoctorStatus.AVAILABLE));

        Assert.Null(ex);
    }

    [Fact]
    public void GetPotentialSwapColleagues_WhenConnectionFails_ReturnsEmptyList()
    {
        var requester = new Doctor(1, "John", "Doe", string.Empty, string.Empty, true, "Cardiology", "L-1", DoctorStatus.AVAILABLE, 3);

        Assert.Empty(new StaffRepository(InvalidConnectionString).GetPotentialSwapColleagues(requester));
    }

    [Fact]
    public void GetAvailableStaff_WhenConnectionFails_ReturnsEmptyList()
        => Assert.Empty(new StaffRepository(InvalidConnectionString).GetAvailableStaff("Cardiology", "Sterile Compounding"));

    [Fact]
    public void LoadAllStaff_ReturnsDoctorInsertedInDatabase()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Alice", "LoadAll", "Cardiology");
        try
        {
            Assert.Contains(new StaffRepository(db.ConnectionString).LoadAllStaff(), s => s.StaffID == staffId);
        }
        finally
        {
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void GetStaffById_ReturnsCorrectStaff()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Bob", "GetById", "Neurology");
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetStaffById(staffId);

            Assert.NotNull(result);
            Assert.Equal(staffId, result!.StaffID);
            Assert.Equal("Bob", result.FirstName);
        }
        finally
        {
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void GetAvailableDoctors_ReturnsOnlyAvailableDoctors()
    {
        using var conn = db.OpenConnection();
        var availableId = db.InsertStaff(conn, "Doctor", "Carol", "AvailDoc", "Oncology", status: "Available", isAvailable: true);
        var unavailableId = db.InsertStaff(conn, "Doctor", "Dave", "AvailDoc", "Oncology", status: "Off_Duty", isAvailable: false);
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetAvailableDoctors();

            Assert.Contains(result, d => d.StaffID == availableId);
            Assert.DoesNotContain(result, d => d.StaffID == unavailableId);
        }
        finally
        {
            db.DeleteStaff(conn, availableId);
            db.DeleteStaff(conn, unavailableId);
        }
    }

    [Fact]
    public void GetPharmacists_ReturnsPharmacistInsertedInDatabase()
    {
        using var conn = db.OpenConnection();
        var pharmacistId = db.InsertStaff(conn, "Pharmacist", "Eve", "GetPharm", certification: "PharmD");
        try
        {
            Assert.Contains(new StaffRepository(db.ConnectionString).GetPharmacists(), p => p.StaffID == pharmacistId);
        }
        finally
        {
            db.DeleteStaff(conn, pharmacistId);
        }
    }

    [Fact]
    public void GetDoctorsBySpecialization_ReturnsOnlyMatchingSpecialization()
    {
        using var conn = db.OpenConnection();
        var cardiologistId = db.InsertStaff(conn, "Doctor", "Frank", "BySpec", "Cardiology");
        var neurologistId = db.InsertStaff(conn, "Doctor", "Grace", "BySpec", "Neurology");
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetDoctorsBySpecialization("Cardiology");

            Assert.Contains(result, d => d.StaffID == cardiologistId);
            Assert.DoesNotContain(result, d => d.StaffID == neurologistId);
        }
        finally
        {
            db.DeleteStaff(conn, cardiologistId);
            db.DeleteStaff(conn, neurologistId);
        }
    }

    [Fact]
    public void GetPharmacystsByCertification_ReturnsOnlyMatchingCertification()
    {
        using var conn = db.OpenConnection();
        var bcpsId = db.InsertStaff(conn, "Pharmacist", "Henry", "ByCert", certification: "BCPS");
        var pharmDId = db.InsertStaff(conn, "Pharmacist", "Iris", "ByCert", certification: "PharmD");
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetPharmacystsByCertification("BCPS");

            Assert.Contains(result, p => p.StaffID == bcpsId);
            Assert.DoesNotContain(result, p => p.StaffID == pharmDId);
        }
        finally
        {
            db.DeleteStaff(conn, bcpsId);
            db.DeleteStaff(conn, pharmDId);
        }
    }

    [Fact]
    public void GetPotentialSwapColleagues_ReturnsDoctorsWithSameSpecialization()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Doctor", "Jack", "SwapColleague", "Cardiology");
        var sameSpecId = db.InsertStaff(conn, "Doctor", "Karen", "SwapColleague", "Cardiology");
        var diffSpecId = db.InsertStaff(conn, "Doctor", "Leo", "SwapColleague", "Neurology");
        try
        {
            var requester = new Doctor(requesterId, "Jack", "SwapColleague", string.Empty, string.Empty, true, "Cardiology", "LIC-1", DoctorStatus.AVAILABLE, 1);

            var result = new StaffRepository(db.ConnectionString).GetPotentialSwapColleagues(requester);

            Assert.Contains(result, s => s.StaffID == sameSpecId);
            Assert.DoesNotContain(result, s => s.StaffID == diffSpecId);
            Assert.DoesNotContain(result, s => s.StaffID == requesterId);
        }
        finally
        {
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, sameSpecId);
            db.DeleteStaff(conn, diffSpecId);
        }
    }

    [Fact]
    public void UpdateStaffAvailability_UpdatesAvailabilityAndStatusInDatabase()
    {
        using var conn = db.OpenConnection();
        var staffId = db.InsertStaff(conn, "Doctor", "Mia", "UpdateAvail", "Cardiology", status: "Available", isAvailable: true);
        try
        {
            var repo = new StaffRepository(db.ConnectionString);

            repo.UpdateStaffAvailability(staffId, false, DoctorStatus.OFF_DUTY);

            var updated = repo.GetStaffById(staffId) as Doctor;
            Assert.NotNull(updated);
            Assert.False(updated!.Available);
            Assert.Equal(DoctorStatus.OFF_DUTY, updated.DoctorStatus);
        }
        finally
        {
            db.DeleteStaff(conn, staffId);
        }
    }

    [Fact]
    public void GetAvailableStaff_WithBothParams_ReturnsDoctorAndPharmacistMatchingFilters()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Nina", "AvailBoth", "Cardiology", isAvailable: true);
        var pharmacistId = db.InsertStaff(conn, "Pharmacist", "Owen", "AvailBoth", certification: "BCPS", isAvailable: true);
        var otherDoctorId = db.InsertStaff(conn, "Doctor", "Paul", "AvailBoth", "Neurology", isAvailable: true);
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetAvailableStaff("Cardiology", "BCPS");

            Assert.Contains(result, s => s.StaffID == doctorId);
            Assert.Contains(result, s => s.StaffID == pharmacistId);
            Assert.DoesNotContain(result, s => s.StaffID == otherDoctorId);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
            db.DeleteStaff(conn, pharmacistId);
            db.DeleteStaff(conn, otherDoctorId);
        }
    }

    [Fact]
    public void GetAvailableStaff_WithOnlyDoctorSpec_ReturnsOnlyMatchingDoctors()
    {
        using var conn = db.OpenConnection();
        var matchingDoctorId = db.InsertStaff(conn, "Doctor", "Quinn", "AvailDocOnly", "Oncology", isAvailable: true);
        var pharmacistId = db.InsertStaff(conn, "Pharmacist", "Ruth", "AvailDocOnly", certification: "PharmD", isAvailable: true);
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetAvailableStaff("Oncology", string.Empty);

            Assert.Contains(result, s => s.StaffID == matchingDoctorId);
            Assert.DoesNotContain(result, s => s.StaffID == pharmacistId);
        }
        finally
        {
            db.DeleteStaff(conn, matchingDoctorId);
            db.DeleteStaff(conn, pharmacistId);
        }
    }

    [Fact]
    public void GetAvailableStaff_WithOnlyPharmacistCert_ReturnsOnlyMatchingPharmacists()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Sam", "AvailPharmOnly", "Cardiology", isAvailable: true);
        var matchingPharmId = db.InsertStaff(conn, "Pharmacist", "Tina", "AvailPharmOnly", certification: "BCOP", isAvailable: true);
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetAvailableStaff(string.Empty, "BCOP");

            Assert.Contains(result, s => s.StaffID == matchingPharmId);
            Assert.DoesNotContain(result, s => s.StaffID == doctorId);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
            db.DeleteStaff(conn, matchingPharmId);
        }
    }

    [Fact]
    public void GetAvailableStaff_WithNoParams_ReturnsAllAvailableStaff()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Uma", "AvailNone", "Cardiology", isAvailable: true);
        var pharmacistId = db.InsertStaff(conn, "Pharmacist", "Victor", "AvailNone", certification: "PharmD", isAvailable: true);
        try
        {
            var result = new StaffRepository(db.ConnectionString).GetAvailableStaff(string.Empty, string.Empty);

            Assert.Contains(result, s => s.StaffID == doctorId);
            Assert.Contains(result, s => s.StaffID == pharmacistId);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
            db.DeleteStaff(conn, pharmacistId);
        }
    }

    [Fact]
    public void GetPotentialSwapColleagues_ReturnsPharmacistsWithSameCertification()
    {
        using var conn = db.OpenConnection();
        var requesterId = db.InsertStaff(conn, "Pharmacist", "Wendy", "PharmSwap", certification: "BCPS");
        var sameCertId = db.InsertStaff(conn, "Pharmacist", "Xavier", "PharmSwap", certification: "BCPS");
        var diffCertId = db.InsertStaff(conn, "Pharmacist", "Yara", "PharmSwap", certification: "PharmD");
        try
        {
            var requester = new Pharmacyst(requesterId, "Wendy", "PharmSwap", string.Empty, true, "BCPS", 1);

            var result = new StaffRepository(db.ConnectionString).GetPotentialSwapColleagues(requester);

            Assert.Contains(result, s => s.StaffID == sameCertId);
            Assert.DoesNotContain(result, s => s.StaffID == diffCertId);
            Assert.DoesNotContain(result, s => s.StaffID == requesterId);
        }
        finally
        {
            db.DeleteStaff(conn, requesterId);
            db.DeleteStaff(conn, sameCertId);
            db.DeleteStaff(conn, diffCertId);
        }
    }

    [Fact]
    public void GetPotentialSwapColleagues_ReturnsEmpty_WhenRequesterNotInDatabase()
    {
        // Arrange: use a staff ID that does not exist in the database
        var ghost = new Doctor(999999, "Ghost", "User", string.Empty, string.Empty, true, "Cardiology", "LIC-X", DoctorStatus.AVAILABLE, 1);

        var result = new StaffRepository(db.ConnectionString).GetPotentialSwapColleagues(ghost);

        Assert.Empty(result);
    }
}
