using System.Collections.Generic;
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

        private readonly SalaryRepository salaryRepository;

        public SalaryComputationService(SalaryRepository salaryRepository)
        {
            this.salaryRepository = salaryRepository;
        }

        public Task<double> ComputeSalaryDoctorAsync(Doctor doctor, List<Shift> monthlyShifts, int month, int year)
        {
            double baseSalaryFromShifts = 0;

            foreach (var shift in monthlyShifts)
            {
                double hoursFromDatabase = salaryRepository.GetShiftHoursFromDb(shift.Id);
                double shiftHours = hoursFromDatabase > 0 ? hoursFromDatabase : (shift.EndTime - shift.StartTime).TotalHours;

                double shiftSalary = shiftHours * DoctorBaseHourlyRate;

                if (shift.StartTime.DayOfWeek == System.DayOfWeek.Saturday)
                {
                    shiftSalary *= SaturdayOvertimeMultiplier;
                }
                else if (shift.StartTime.DayOfWeek == System.DayOfWeek.Sunday)
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

                baseSalaryFromShifts += shiftSalary;
            }

            double finalSalary = baseSalaryFromShifts;

            double specializationBonusPercentage = 0;
            string normalizedSpecialization = doctor.Specialization?.ToLower() ?? string.Empty;

            if (normalizedSpecialization.Contains("surgeon") || normalizedSpecialization.Contains("surgery"))
            {
                specializationBonusPercentage = SurgeonSpecializationBonusPercentage;
            }
            else if (normalizedSpecialization.Contains("cardiologist"))
            {
                specializationBonusPercentage = CardiologistSpecializationBonusPercentage;
            }
            else if (normalizedSpecialization.Contains("er") || normalizedSpecialization.Contains("emergency"))
            {
                specializationBonusPercentage = EmergencySpecializationBonusPercentage;
            }

            finalSalary += baseSalaryFromShifts * specializationBonusPercentage;
            finalSalary += baseSalaryFromShifts * (doctor.YearsOfExperience * YearsOfExperienceBonusPercentagePerYear);

            try
            {
                if (salaryRepository.DidStaffParticipateInHangout(doctor.StaffID, month, year))
                {
                    finalSalary *= HangoutParticipationBonusMultiplier;
                }
            }
            catch
            {
            }

            return Task.FromResult(finalSalary);
        }

        public Task<double> ComputeSalaryPharmacistAsync(Pharmacyst pharmacist, List<Shift> monthlyShifts, int month, int year)
        {
            double baseSalaryFromShifts = 0;

            foreach (var shift in monthlyShifts)
            {
                double hoursFromDatabase = salaryRepository.GetShiftHoursFromDb(shift.Id);
                double shiftHours = hoursFromDatabase > 0 ? hoursFromDatabase : (shift.EndTime - shift.StartTime).TotalHours;

                double shiftSalary = shiftHours * PharmacistBaseHourlyRate;

                if (shift.StartTime.DayOfWeek == System.DayOfWeek.Saturday)
                {
                    shiftSalary *= SaturdayOvertimeMultiplier;
                }
                else if (shift.StartTime.DayOfWeek == System.DayOfWeek.Sunday)
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

                baseSalaryFromShifts += shiftSalary;
            }

            double finalSalary = baseSalaryFromShifts;

            int medicinesSold = 0;
            try
            {
                medicinesSold = salaryRepository.GetMedicinesSold(pharmacist.StaffID, month, year);
            }
            catch
            {
                medicinesSold = 0;
            }

            double medicineSalesBonusPercentage = (medicinesSold / MedicinesSoldBonusInterval) * MedicinesSoldBonusPerInterval;
            if (medicineSalesBonusPercentage > MaxMedicineSalesBonusPercentage)
            {
                medicineSalesBonusPercentage = MaxMedicineSalesBonusPercentage;
            }

            finalSalary += baseSalaryFromShifts * medicineSalesBonusPercentage;
            finalSalary += baseSalaryFromShifts * (pharmacist.YearsOfExperience * YearsOfExperienceBonusPercentagePerYear);

            try
            {
                if (salaryRepository.DidStaffParticipateInHangout(pharmacist.StaffID, month, year))
                {
                    finalSalary *= HangoutParticipationBonusMultiplier;
                }
            }
            catch
            {
            }

            return Task.FromResult(finalSalary);
        }
    }
}
