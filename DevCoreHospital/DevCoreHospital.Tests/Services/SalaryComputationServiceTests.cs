using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services;

public class SalaryComputationServiceTests
{
    private const double DoctorBaseHourlyRate = 85.0;
    private const double PharmacistBaseHourlyRate = 45.0;
    private const double ShiftDurationHours = 8.0;
    private const double HangoutBonusMultiplier = 1.05;
    private const double MedicineBonusRatePerGroup = 0.01;
    private const int MedicineBonusGroupSize = 10;
    private const double MaxMedicineBonusRate = 0.30;
    private const int HandoverCountForBonusTest = 50;
    private const int HandoverCountForCapTest = 1000;
    private const double SurgeonSaturdayNightExpectedSalary = 844.56;

    private readonly Mock<IPharmacyHandoverRepository> handoverRepository = new();
    private readonly Mock<IHangoutRepository> hangoutRepository = new();
    private readonly Mock<IHangoutParticipantRepository> hangoutParticipantRepository = new();

    public SalaryComputationServiceTests()
    {
        handoverRepository.Setup(repository => repository.GetAllPharmacyHandovers())
            .Returns(new List<PharmacyHandover>());
        hangoutRepository.Setup(repository => repository.GetAllHangouts())
            .Returns(new List<Hangout>());
        hangoutParticipantRepository.Setup(repository => repository.GetAllParticipants())
            .Returns(new List<(int HangoutId, int StaffId)>());
    }

    private SalaryComputationService CreateService() =>
        new SalaryComputationService(
            handoverRepository.Object,
            hangoutRepository.Object,
            hangoutParticipantRepository.Object);

    private static Shift CreateShift(int shiftId, IStaff staff, DateTime start, DateTime end) =>
        new Shift(shiftId, staff, "Ward A", start, end, ShiftStatus.SCHEDULED);

    [Fact]
    public async Task ComputeSalaryDoctorAsync_WeekdayDayShift_ReturnsBaseRateTimesHours()
    {
        var doctor = new Doctor { StaffID = 1, Specialization = "Pediatrics", YearsOfExperience = 0 };
        var shift = CreateShift(100, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));

        var salary = await CreateService().ComputeSalaryDoctorAsync(doctor, new List<Shift> { shift }, 5, 2026);

        Assert.Equal(ShiftDurationHours * DoctorBaseHourlyRate, salary, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_SurgeonOnSaturdayNight_AppliesAllMultipliers()
    {
        var doctor = new Doctor { StaffID = 2, Specialization = "Surgery", YearsOfExperience = 0 };
        var shift = CreateShift(101, doctor, new DateTime(2026, 5, 2, 21, 0, 0), new DateTime(2026, 5, 3, 3, 0, 0));

        var salary = await CreateService().ComputeSalaryDoctorAsync(doctor, new List<Shift> { shift }, 5, 2026);

        Assert.Equal(SurgeonSaturdayNightExpectedSalary, salary, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_HangoutParticipationInMonth_AppliesFivePercentBonus()
    {
        var doctor = new Doctor { StaffID = 3, Specialization = "Pediatrics", YearsOfExperience = 0 };
        var shift = CreateShift(102, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        hangoutRepository.Setup(repository => repository.GetAllHangouts())
            .Returns(new List<Hangout> { new Hangout(7, "Lunch", "d", new DateTime(2026, 5, 10), 5) });
        hangoutParticipantRepository.Setup(repository => repository.GetAllParticipants())
            .Returns(new List<(int HangoutId, int StaffId)> { (7, 3) });

        var salary = await CreateService().ComputeSalaryDoctorAsync(doctor, new List<Shift> { shift }, 5, 2026);

        Assert.Equal(ShiftDurationHours * DoctorBaseHourlyRate * HangoutBonusMultiplier, salary, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_HangoutInDifferentMonth_DoesNotApplyBonus()
    {
        var doctor = new Doctor { StaffID = 4, Specialization = "Pediatrics", YearsOfExperience = 0 };
        var shift = CreateShift(103, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        hangoutRepository.Setup(repository => repository.GetAllHangouts())
            .Returns(new List<Hangout> { new Hangout(8, "Lunch", "d", new DateTime(2026, 4, 10), 5) });
        hangoutParticipantRepository.Setup(repository => repository.GetAllParticipants())
            .Returns(new List<(int HangoutId, int StaffId)> { (8, 4) });

        var salary = await CreateService().ComputeSalaryDoctorAsync(doctor, new List<Shift> { shift }, 5, 2026);

        Assert.Equal(ShiftDurationHours * DoctorBaseHourlyRate, salary, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_CountsHandoversInTargetMonth()
    {
        var pharmacist = new Pharmacyst { StaffID = 5, YearsOfExperience = 0 };
        var shift = CreateShift(104, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var handovers = new List<PharmacyHandover>();
        for (int handoverIndex = 0; handoverIndex < HandoverCountForBonusTest; handoverIndex++)
        {
            handovers.Add(new PharmacyHandover { PharmacistId = 5, HandoverDate = new DateTime(2026, 5, 1) });
        }
        handovers.Add(new PharmacyHandover { PharmacistId = 5, HandoverDate = new DateTime(2026, 4, 1) });
        handovers.Add(new PharmacyHandover { PharmacistId = 99, HandoverDate = new DateTime(2026, 5, 1) });
        handoverRepository.Setup(repository => repository.GetAllPharmacyHandovers()).Returns(handovers);

        var salary = await CreateService().ComputeSalaryPharmacistAsync(pharmacist, new List<Shift> { shift }, 5, 2026);

        double baseSalary = ShiftDurationHours * PharmacistBaseHourlyRate;
        double medicineBonusRate = (HandoverCountForBonusTest / MedicineBonusGroupSize) * MedicineBonusRatePerGroup;
        Assert.Equal(baseSalary + baseSalary * medicineBonusRate, salary, 2);
    }

    [Fact]
    public async Task ComputeSalaryPharmacistAsync_CapsMedicineBonusAtThirtyPercent()
    {
        var pharmacist = new Pharmacyst { StaffID = 6, YearsOfExperience = 0 };
        var shift = CreateShift(105, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
        var handovers = new List<PharmacyHandover>();
        for (int handoverIndex = 0; handoverIndex < HandoverCountForCapTest; handoverIndex++)
        {
            handovers.Add(new PharmacyHandover { PharmacistId = 6, HandoverDate = new DateTime(2026, 5, 1) });
        }
        handoverRepository.Setup(repository => repository.GetAllPharmacyHandovers()).Returns(handovers);

        var salary = await CreateService().ComputeSalaryPharmacistAsync(pharmacist, new List<Shift> { shift }, 5, 2026);

        double baseSalary = ShiftDurationHours * PharmacistBaseHourlyRate;
        Assert.Equal(baseSalary + baseSalary * MaxMedicineBonusRate, salary, 2);
    }

    [Fact]
    public async Task ComputeSalaryDoctorAsync_NoShifts_ReturnsZero()
    {
        var doctor = new Doctor { StaffID = 7, Specialization = "Pediatrics", YearsOfExperience = 0 };

        var salary = await CreateService().ComputeSalaryDoctorAsync(doctor, new List<Shift>(), 5, 2026);

        Assert.Equal(0.0, salary, 2);
    }
}