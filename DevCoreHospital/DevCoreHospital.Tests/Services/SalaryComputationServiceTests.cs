using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services;

public class SalaryComputationServiceTests
{
    [Fact]
    public async Task ComputeSalaryDoctorAsync_ComputesBaseExperienceAndHangoutBonus()
    {
        var doctor = new Doctor { StaffID = 10, Specialization = "Pediatrics", YearsOfExperience = 5 };
        var shift = CreateShift(1, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(1)).Returns(10);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(10, 5, 2026)).Returns(true);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryDoctorAsync(doctor, [shift], 5, 2026);

        Assert.Equal(981.75, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_AppliesSurgeonSpecializationWithSaturdayNightOvertime()
    {
        var doctor = new Doctor { StaffID = 11, Specialization = "Surgery", YearsOfExperience = 0 };
        var shift = CreateShift(2, doctor, new DateTime(2026, 5, 2, 21, 0, 0), new DateTime(2026, 5, 3, 3, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(2)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(11, 5, 2026)).Returns(false);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryDoctorAsync(doctor, [shift], 5, 2026);

        Assert.Equal(844.56, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_AppliesCardiologistSpecialization_WhenHangoutLookupThrows()
    {
        var doctor = new Doctor { StaffID = 12, Specialization = "Cardiologist", YearsOfExperience = 0 };
        var shift = CreateShift(3, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(3)).Returns(8);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(12, 5, 2026)).Throws(new InvalidOperationException("db"));

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryDoctorAsync(doctor, [shift], 5, 2026);

        Assert.Equal(782.00, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_AppliesEmergencySpecializationAndSundayOvertime()
    {
        var doctor = new Doctor { StaffID = 13, Specialization = "ER", YearsOfExperience = 1 };
        var shift = CreateShift(4, doctor, new DateTime(2026, 5, 3, 10, 0, 0), new DateTime(2026, 5, 3, 18, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(4)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(13, 5, 2026)).Returns(false);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryDoctorAsync(doctor, [shift], 5, 2026);

        Assert.Equal(952.00, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_ComputesBaseMedicineSalesExperienceAndHangoutBonus()
    {
        var pharmacist = new Pharmacyst { StaffID = 20, YearsOfExperience = 4 };
        var shift = CreateShift(5, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(5)).Returns(12);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetMedicinesSold(20, 5, 2026)).Returns(95);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(20, 5, 2026)).Returns(true);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryPharmacistAsync(pharmacist, [shift], 5, 2026);

        Assert.Equal(663.39, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_CapsMedicineSalesBonus()
    {
        var pharmacist = new Pharmacyst { StaffID = 21, YearsOfExperience = 0 };
        var shift = CreateShift(6, pharmacist, new DateTime(2026, 5, 3, 22, 0, 0), new DateTime(2026, 5, 4, 2, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(6)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetMedicinesSold(21, 5, 2026)).Returns(1000);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(21, 5, 2026)).Returns(false);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryPharmacistAsync(pharmacist, [shift], 5, 2026);

        Assert.Equal(351.00, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_UsesFallbacks_WhenRepositoryThrows()
    {
        var pharmacist = new Pharmacyst { StaffID = 22, YearsOfExperience = 2 };
        var shift = CreateShift(7, pharmacist, new DateTime(2026, 5, 4, 9, 0, 0), new DateTime(2026, 5, 4, 17, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(7)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetMedicinesSold(22, 5, 2026)).Throws(new InvalidOperationException("db"));
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(22, 5, 2026)).Throws(new InvalidOperationException("db"));

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryPharmacistAsync(pharmacist, [shift], 5, 2026);

        Assert.Equal(374.40, result, 2);
    }

    [Fact]
    public async Task SalaryComputation_EndToEnd_WithRealisticRepositoryData_ReturnsExpectedValues()
    {
        var doctor = new Doctor { StaffID = 30, Specialization = "Emergency medicine", YearsOfExperience = 6 };
        var doctorShifts = new List<Shift>
        {
            CreateShift(100, doctor, new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 16, 0, 0)),
            CreateShift(101, doctor, new DateTime(2026, 5, 2, 20, 0, 0), new DateTime(2026, 5, 3, 2, 0, 0))
        };

        var pharmacist = new Pharmacyst { StaffID = 31, YearsOfExperience = 5 };
        var pharmacistShifts = new List<Shift>
        {
            CreateShift(200, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0)),
            CreateShift(201, pharmacist, new DateTime(2026, 5, 3, 21, 0, 0), new DateTime(2026, 5, 4, 1, 0, 0))
        };

        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(100)).Returns(8);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(101)).Returns(6);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(200)).Returns(8);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(201)).Returns(4);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetMedicinesSold(31, 5, 2026)).Returns(120);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(30, 5, 2026)).Returns(true);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(31, 5, 2026)).Returns(true);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);

        var doctorSalary = await service.ComputeSalaryDoctorAsync(doctor, doctorShifts, 5, 2026);
        var pharmacistSalary = await service.ComputeSalaryPharmacistAsync(pharmacist, pharmacistShifts, 5, 2026);

        Assert.Equal(1772.65, doctorSalary, 2);
        Assert.Equal(807.03, pharmacistSalary, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_ReturnsZero_WhenNoShifts()
    {
        var doctor = new Doctor { StaffID = 50, Specialization = "Pediatrics", YearsOfExperience = 0 };
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(50, 5, 2026)).Returns(false);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryDoctorAsync(doctor, new List<Shift>(), 5, 2026);

        Assert.Equal(0.0, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_ReturnsZero_WhenNoShifts()
    {
        var pharmacist = new Pharmacyst { StaffID = 51, YearsOfExperience = 0 };
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetMedicinesSold(51, 5, 2026)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(51, 5, 2026)).Returns(false);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryPharmacistAsync(pharmacist, new List<Shift>(), 5, 2026);

        Assert.Equal(0.0, result, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_AppliesNoMedicineBonus_WhenZeroMedicinesSold()
    {
        var pharmacist = new Pharmacyst { StaffID = 52, YearsOfExperience = 0 };
        var shift = CreateShift(8, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var salaryRepositoryMock = new Mock<SalaryRepository>("fake");

        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetShiftHoursFromDb(8)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.GetMedicinesSold(52, 5, 2026)).Returns(0);
        salaryRepositoryMock.Setup(salaryRepository => salaryRepository.DidStaffParticipateInHangout(52, 5, 2026)).Returns(false);

        var service = new SalaryComputationService(salaryRepositoryMock.Object);
        var result = await service.ComputeSalaryPharmacistAsync(pharmacist, [shift], 5, 2026);

        Assert.Equal(360.0, result, 2);
    }

    [Fact]
    public void GetAllStaff_ReturnsStaffFromRepository()
    {
        var doctor = new Doctor { StaffID = 1, FirstName = "A", LastName = "B" };
        var pharmacist = new Pharmacyst { StaffID = 2, FirstName = "C", LastName = "D" };
        var salaryRepository = new Mock<SalaryRepository>("fake");
        var staffRepository = new Mock<IStaffRepository>();
        staffRepository.Setup(staffRepository => staffRepository.LoadAllStaff()).Returns(new List<IStaff> { doctor, pharmacist });

        var service = new SalaryComputationService(salaryRepository.Object, staffRepository.Object, null);

        var result = service.GetAllStaff();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetAllStaff_ReturnsEmptyList_WhenRepositoryIsNull()
    {
        var salaryRepository = new Mock<SalaryRepository>("fake");
        var service = new SalaryComputationService(salaryRepository.Object);

        var result = service.GetAllStaff();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllShifts_ReturnsShiftsFromRepository()
    {
        var staff = new Doctor { StaffID = 1, FirstName = "A", LastName = "B" };
        var shift = CreateShift(10, staff, new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 16, 0, 0));
        var salaryRepository = new Mock<SalaryRepository>("fake");
        var shiftRepository = new Mock<IShiftManagementShiftRepository>();
        shiftRepository.Setup(shiftRepository => shiftRepository.GetShifts()).Returns(new List<Shift> { shift });

        var service = new SalaryComputationService(salaryRepository.Object, null, shiftRepository.Object);

        var result = service.GetAllShifts();

        Assert.Single(result);
        Assert.Equal(10, result[0].Id);
    }

    [Fact]
    public void GetAllShifts_ReturnsEmptyList_WhenRepositoryIsNull()
    {
        var salaryRepository = new Mock<SalaryRepository>("fake");
        var service = new SalaryComputationService(salaryRepository.Object);

        var result = service.GetAllShifts();

        Assert.Empty(result);
    }

    private static Shift CreateShift(int id, IStaff staff, DateTime start, DateTime end)
    {
        return new Shift(id, staff, "Ward A", start, end, ShiftStatus.SCHEDULED);
    }
}
