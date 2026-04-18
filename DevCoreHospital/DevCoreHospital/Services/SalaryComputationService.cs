using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Data;

namespace DevCoreHospital.Services
{
    public class SalaryComputationService : ISalaryComputationService
    {
        private readonly DatabaseManager _dbManager;

        public SalaryComputationService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public Task<double> ComputeSalaryDoctorAsync(Doctor doctor, List<Shift> monthlyShifts, int month, int year)
        {
            double initialSalary = 0;
            double doctorHourlyRate = 85.0;

            foreach (var shift in monthlyShifts)
            {
                double dbHours = _dbManager.GetShiftHoursFromDb(shift.Id);
                double shiftHours = dbHours > 0 ? dbHours : (shift.EndTime - shift.StartTime).TotalHours;

                double shiftSalary = shiftHours * doctorHourlyRate;

                // Weekend logic
                if (shift.StartTime.DayOfWeek == System.DayOfWeek.Saturday)
                    shiftSalary *= 1.15;
                else if (shift.StartTime.DayOfWeek == System.DayOfWeek.Sunday)
                    shiftSalary *= 1.25;

                // Night shift logic (+20%)
                bool isNightShift = shift.StartTime.Hour >= 20 || shift.StartTime.Hour <= 6 || shift.EndTime.Hour <= 6;
                if (isNightShift)
                    shiftSalary *= 1.20;

                initialSalary += shiftSalary;
            }

            double finalSalary = initialSalary;

            // Specialization bonus
            double specBonusPercentage = 0;
            string spec = doctor.Specialization?.ToLower() ?? "";

            if (spec.Contains("surgeon") || spec.Contains("surgery")) specBonusPercentage = 0.20;
            else if (spec.Contains("cardiologist")) specBonusPercentage = 0.15;
            else if (spec.Contains("er") || spec.Contains("emergency")) specBonusPercentage = 0.10;

            finalSalary += (initialSalary * specBonusPercentage);

            // Years of experience bonus (+2% per year)
            finalSalary += (initialSalary * (doctor.YearsOfExperience * 0.02));

            // Hangout bonus (+5% if they attended at least one hangout this month)
            // Note: Make sure DidStaffParticipateInHangout is implemented in DatabaseManager
            try
            {
                if (_dbManager.DidStaffParticipateInHangout(doctor.StaffID, month, year))
                {
                    finalSalary *= 1.05;
                }
            }
            catch { /* Ignore if not yet implemented */ }

            return Task.FromResult(finalSalary);
        }

        public Task<double> ComputeSalaryPharmacistAsync(Pharmacyst pharmacist, List<Shift> monthlyShifts, int month, int year)
        {
            double initialSalary = 0;
            double pharmacistHourlyRate = 45.0;

            foreach (var shift in monthlyShifts)
            {
                double dbHours = _dbManager.GetShiftHoursFromDb(shift.Id);
                double shiftHours = dbHours > 0 ? dbHours : (shift.EndTime - shift.StartTime).TotalHours;

                double shiftSalary = shiftHours * pharmacistHourlyRate;

                // Weekend logic (+15% Sat, +25% Sun)
                if (shift.StartTime.DayOfWeek == System.DayOfWeek.Saturday)
                    shiftSalary *= 1.15;
                else if (shift.StartTime.DayOfWeek == System.DayOfWeek.Sunday)
                    shiftSalary *= 1.25;

                // Night shift logic (+20%)
                bool isNightShift = shift.StartTime.Hour >= 20 || shift.StartTime.Hour <= 6 || shift.EndTime.Hour <= 6;
                if (isNightShift)
                    shiftSalary *= 1.20;

                initialSalary += shiftSalary;
            }

            double finalSalary = initialSalary;

            // Medicines sold logic (Increased by M%)
            int medicinesSold = 0;
            try
            {
                medicinesSold = _dbManager.GetMedicinesSold(pharmacist.StaffID, month, year);
            }
            catch
            {
                medicinesSold = 0;
            }

            // Example business rule: +1% bonus for every 10 medicines sold, capped at 30%
            double medicineBonusPercent = (medicinesSold / 10) * 0.01;
            if (medicineBonusPercent > 0.30) medicineBonusPercent = 0.30;

            finalSalary += (initialSalary * medicineBonusPercent);

            // Years of experience bonus (+2% per year)
            finalSalary += (initialSalary * (pharmacist.YearsOfExperience * 0.02));

            // Hangout bonus (+5% if they attended at least one hangout this month)
            try
            {
                if (_dbManager.DidStaffParticipateInHangout(pharmacist.StaffID, month, year))
                {
                    finalSalary *= 1.05;
                }
            }
            catch { /* Ignore if not yet implemented */ }

            return Task.FromResult(finalSalary);
        }
    }
}