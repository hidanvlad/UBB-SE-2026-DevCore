using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public class SalaryComputationViewModel : ObservableObject
    {
        private readonly ISalaryComputationService salaryService;

        public ObservableCollection<IStaff> StaffList { get; } = new ObservableCollection<IStaff>();
        public ObservableCollection<Shift> ShiftList { get; } = new ObservableCollection<Shift>();

        private IStaff selectedStaff = default!;
        public IStaff SelectedStaff
        {
            get => selectedStaff;
            set
            {
                SetProperty(ref selectedStaff, value);
                ComputeSalaryCommand.RaiseCanExecuteChanged();
            }
        }

        private int selectedMonth = DateTime.Now.Month;
        public int SelectedMonth { get => selectedMonth; set => SetProperty(ref selectedMonth, value); }

        private int selectedYear = DateTime.Now.Year;
        public int SelectedYear { get => selectedYear; set => SetProperty(ref selectedYear, value); }

        private bool isLoading;
        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }

        private string errorMessage = string.Empty;
        public string ErrorMessage { get => errorMessage; set => SetProperty(ref errorMessage, value); }

        private string salaryResult = string.Empty;
        public string SalaryResult { get => salaryResult; set => SetProperty(ref salaryResult, value); }

        public AsyncRelayCommand ComputeSalaryCommand { get; }

        public SalaryComputationViewModel(ISalaryComputationService salaryService)
        {
            this.salaryService = salaryService;

            ComputeSalaryCommand = new AsyncRelayCommand(ComputeSalaryAsync, CanComputeSalary);

            LoadStaffList();
            LoadShiftList();
        }

        public SalaryComputationViewModel(ISalaryComputationService salaryService, IEnumerable<IStaff> staffList, IEnumerable<Shift> shiftList)
        {
            this.salaryService = salaryService;

            ComputeSalaryCommand = new AsyncRelayCommand(ComputeSalaryAsync, CanComputeSalary);

            StaffList.ReplaceWith(staffList);
            ShiftList.ReplaceWith(shiftList);
        }

        private void LoadStaffList() => StaffList.ReplaceWith(salaryService.GetAllStaff());

        private void LoadShiftList() => ShiftList.ReplaceWith(salaryService.GetAllShifts());

        private bool CanComputeSalary()
        {
            return SelectedStaff != null && SelectedStaff.StaffID > 0;
        }

        private async Task ComputeSalaryAsync()
        {
            ErrorMessage = string.Empty;
            SalaryResult = string.Empty;
            IsLoading = true;

            try
            {
                bool IsStaffShiftForPeriod(Shift shift) =>
                    shift.AppointedStaff?.StaffID == SelectedStaff.StaffID
                    && shift.StartTime.Month == SelectedMonth
                    && shift.StartTime.Year == SelectedYear;

                var staffShiftsForPeriod = ShiftList
                    .Where(IsStaffShiftForPeriod)
                    .ToList();

                double computedSalary = 0;

                if (SelectedStaff is Models.Doctor doctor)
                {
                    computedSalary = await salaryService.ComputeSalaryDoctorAsync(doctor, staffShiftsForPeriod, SelectedMonth, SelectedYear);
                }
                else if (SelectedStaff is Models.Pharmacyst pharmacist)
                {
                    computedSalary = await salaryService.ComputeSalaryPharmacistAsync(pharmacist, staffShiftsForPeriod, SelectedMonth, SelectedYear);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported staff type for salary computation.");
                }

                SalaryResult = $"Computed Salary: ${computedSalary:F2}";
            }
            catch (Exception exception)
            {
                ErrorMessage = $"Computation failed: {exception.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
