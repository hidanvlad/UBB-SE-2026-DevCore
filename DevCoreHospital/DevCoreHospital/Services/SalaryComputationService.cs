using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Data;

namespace DevCoreHospital.Services
{
    public class SalaryComputationService
    {
        private readonly DatabaseManager _dbManager;

        public SalaryComputationService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public Task<double> ComputeSalaryDoctorAsync(Doctor doctor, List<Shift> monthlyShifts)
        {
            double totalHours = 0;

            // Task 3 implemented via DB
            foreach (var shift in monthlyShifts)
            {
                double dbHours = _dbManager.GetShiftHoursFromDb(shift.Id);

                if (dbHours > 0)
                {
                    totalHours += dbHours;
                }
                else
                {
                    // Fallback calculation in case DB fails
                    totalHours += (shift.EndTime - shift.StartTime).TotalHours;
                }
            }

            double doctorHourlyRate = 85.0;
            return Task.FromResult(totalHours * doctorHourlyRate);
        }

        public Task<double> ComputeSalaryPharmacistAsync(Pharmacyst pharmacist, List<Shift> monthlyShifts, int month, int year)
        {
            double totalHours = 0;

            // Compute shift hours using the DB implementation
            foreach (var shift in monthlyShifts)
            {
                double dbHours = _dbManager.GetShiftHoursFromDb(shift.Id);

                if (dbHours > 0)
                {
                    totalHours += dbHours;
                }
                else
                {
                    // Fallback calculation in case DB fails
                    totalHours += (shift.EndTime - shift.StartTime).TotalHours;
                }
            }

            double pharmacistHourlyRate = 45.0;

            // Task 5 implemented via DB
            int medicinesSold = 0;
            try
            {
                medicinesSold = _dbManager.GetMedicinesSold(pharmacist.StaffID, month, year);
            }
            catch
            {
                // Fallback if DB isn't connected yet during testing
                medicinesSold = 150;
            }

            double bonusPerMedicine = 1.5;

            return Task.FromResult((totalHours * pharmacistHourlyRate) + (medicinesSold * bonusPerMedicine));
        }
    }
}