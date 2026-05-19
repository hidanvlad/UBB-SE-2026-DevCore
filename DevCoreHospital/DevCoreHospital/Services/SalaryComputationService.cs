using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class SalaryComputationService : ISalaryComputationService
    {
        private const double DoctorBaseHourlyRate = 85.0;
        private const double PharmacistBaseHourlyRate = 45.0;

        private const double SaturdayOvertimeMultiplier = 1.15;
        private const double SundayOvertimeMultiplier = 1.25;

        private const int NightShiftStartHour = 20;
        private const int NightShiftEndHour = 6;
        private const double NightShiftOvertimeMultiplier = 1.20;

        private const double SurgeonSpecializationBonusPercentage = 0.20;
        private const double CardiologistSpecializationBonusPercentage = 0.15;
        private const double EmergencySpecializationBonusPercentage = 0.10;

        private const double YearsOfExperienceBonusPercentagePerYear = 0.02;
        private const double HangoutParticipationBonusMultiplier = 1.05;

        private const int MedicinesSoldBonusInterval = 10;
        private const double MedicinesSoldBonusPerInterval = 0.01;
        private const double MaxMedicineSalesBonusPercentage = 0.30;

        private readonly IPharmacyHandoverRepository pharmacyHandoverRepository;
        private readonly IHangoutRepository hangoutRepository;
        private readonly IHangoutParticipantRepository hangoutParticipantRepository;
        private readonly IStaffRepository? staffRepository;
        private readonly IShiftManagementShiftRepository? shiftRepository;

        public SalaryComputationService(
            IPharmacyHandoverRepository pharmacyHandoverRepository,
            IHangoutRepository hangoutRepository,
            IHangoutParticipantRepository hangoutParticipantRepository,
            IStaffRepository? staffRepository = null,
            IShiftManagementShiftRepository? shiftRepository = null)
        {
            this.pharmacyHandoverRepository = pharmacyHandoverRepository;
            this.hangoutRepository = hangoutRepository;
            this.hangoutParticipantRepository = hangoutParticipantRepository;
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public Task<double> ComputeSalaryDoctorAsync(Doctor doctor, List<Shift> monthlyShifts, int month, int year)
        {
            double baseSalaryFromShifts = ComputeBaseSalaryFromShifts(monthlyShifts, DoctorBaseHourlyRate);

            double specializationBonusPercentage = ResolveSpecializationBonusPercentage(doctor.Specialization);
            double finalSalary = baseSalaryFromShifts;
            finalSalary += baseSalaryFromShifts * specializationBonusPercentage;
            finalSalary += baseSalaryFromShifts * (doctor.YearsOfExperience * YearsOfExperienceBonusPercentagePerYear);

            if (DidStaffParticipateInHangoutForMonth(doctor.StaffID, month, year))
            {
                finalSalary *= HangoutParticipationBonusMultiplier;
            }

            return Task.FromResult(finalSalary);
        }

        public Task<double> ComputeSalaryPharmacistAsync(Pharmacyst pharmacist, List<Shift> monthlyShifts, int month, int year)
        {
            double baseSalaryFromShifts = ComputeBaseSalaryFromShifts(monthlyShifts, PharmacistBaseHourlyRate);

            int medicinesSold = CountMedicinesSoldForPharmacist(pharmacist.StaffID, month, year);
            double medicineSalesBonusPercentage = (medicinesSold / MedicinesSoldBonusInterval) * MedicinesSoldBonusPerInterval;
            if (medicineSalesBonusPercentage > MaxMedicineSalesBonusPercentage)
            {
                medicineSalesBonusPercentage = MaxMedicineSalesBonusPercentage;
            }

            double finalSalary = baseSalaryFromShifts;
            finalSalary += baseSalaryFromShifts * medicineSalesBonusPercentage;
            finalSalary += baseSalaryFromShifts * (pharmacist.YearsOfExperience * YearsOfExperienceBonusPercentagePerYear);

            if (DidStaffParticipateInHangoutForMonth(pharmacist.StaffID, month, year))
            {
                finalSalary *= HangoutParticipationBonusMultiplier;
            }

            return Task.FromResult(finalSalary);
        }

        public List<IStaff> GetAllStaff() =>
            staffRepository?.LoadAllStaff() ?? new List<IStaff>();

        public List<Shift> GetAllShifts() =>
            shiftRepository?.GetAllShifts().ToList() ?? new List<Shift>();

        private double ComputeBaseSalaryFromShifts(List<Shift> monthlyShifts, double baseHourlyRate)
        {
            double total = 0;
            foreach (var shift in monthlyShifts)
            {
                double shiftHours = (shift.EndTime - shift.StartTime).TotalHours;
                double shiftSalary = shiftHours * baseHourlyRate;

                if (shift.StartTime.DayOfWeek == DayOfWeek.Saturday)
                {
                    shiftSalary *= SaturdayOvertimeMultiplier;
                }
                else if (shift.StartTime.DayOfWeek == DayOfWeek.Sunday)
                {
                    shiftSalary *= SundayOvertimeMultiplier;
                }

                bool isNightShift = shift.StartTime.Hour >= NightShiftStartHour
                    || shift.StartTime.Hour <= NightShiftEndHour
                    || shift.EndTime.Hour <= NightShiftEndHour;
                if (isNightShift)
                {
                    shiftSalary *= NightShiftOvertimeMultiplier;
                }

                total += shiftSalary;
            }
            return total;
        }

        private static double ResolveSpecializationBonusPercentage(string? specialization)
        {
            string normalizedSpecialization = (specialization ?? string.Empty).ToLowerInvariant();
            if (normalizedSpecialization.Contains("surgeon") || normalizedSpecialization.Contains("surgery"))
            {
                return SurgeonSpecializationBonusPercentage;
            }
            if (normalizedSpecialization.Contains("cardiologist"))
            {
                return CardiologistSpecializationBonusPercentage;
            }
            if (normalizedSpecialization.Contains("er") || normalizedSpecialization.Contains("emergency"))
            {
                return EmergencySpecializationBonusPercentage;
            }
            return 0;
        }

        private int CountMedicinesSoldForPharmacist(int pharmacistStaffId, int month, int year)
        {
            var allHandovers = pharmacyHandoverRepository.GetAllPharmacyHandovers();
            bool MatchesPharmacistAndMonth(PharmacyHandover handover) =>
                handover.PharmacistId == pharmacistStaffId
                && handover.HandoverDate.Month == month
                && handover.HandoverDate.Year == year;
            return allHandovers.Count(MatchesPharmacistAndMonth);
        }

        private bool DidStaffParticipateInHangoutForMonth(int staffId, int month, int year)
        {
            bool IsForStaff((int HangoutId, int StaffId) participant) => participant.StaffId == staffId;
            int ToHangoutId((int HangoutId, int StaffId) participant) => participant.HangoutId;

            var allParticipants = hangoutParticipantRepository.GetAllParticipants();
            var hangoutIdsForStaff = allParticipants
                .Where(IsForStaff)
                .Select(ToHangoutId)
                .ToHashSet();
            if (hangoutIdsForStaff.Count == 0)
            {
                return false;
            }

            bool IsHangoutInTargetMonth(Hangout hangout) =>
                hangoutIdsForStaff.Contains(hangout.HangoutID)
                && hangout.Date.Month == month
                && hangout.Date.Year == year;

            return hangoutRepository.GetAllHangouts().Any(IsHangoutInTargetMonth);
        }
    }
}
