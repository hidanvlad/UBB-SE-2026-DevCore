using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public class SalaryComputationViewModel : ObservableObject
    {
        private readonly ISalaryComputationService salaryService;
        private readonly StaffRepository? staffRepository;
        private readonly ShiftRepository? shiftRepository;

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

        public SalaryComputationViewModel()
        {
            staffRepository = new StaffRepository(AppSettings.ConnectionString);
            shiftRepository = new ShiftRepository(AppSettings.ConnectionString, staffRepository);
            var salaryRepository = new SalaryRepository(AppSettings.ConnectionString);
            salaryService = new SalaryComputationService(salaryRepository);

            ComputeSalaryCommand = new AsyncRelayCommand(ComputeSalaryAsync, CanComputeSalary);

            LoadStaffList();
            LoadShiftList();
        }

        public SalaryComputationViewModel(ISalaryComputationService salaryService, IEnumerable<IStaff> staffList, IEnumerable<Shift> shiftList)
        {
            this.salaryService = salaryService;
            staffRepository = null;
            shiftRepository = null;

            ComputeSalaryCommand = new AsyncRelayCommand(ComputeSalaryAsync, CanComputeSalary);

            foreach (var staff in staffList)
            {
                StaffList.Add(staff);
            }

            foreach (var shift in shiftList)
            {
                ShiftList.Add(shift);
            }
        }

        private void LoadStaffList()
        {
            if (staffRepository == null)
            {
                return;
            }

            StaffList.Clear();
            foreach (var staff in staffRepository.LoadAllStaff())
            {
                StaffList.Add(staff);
            }
        }

        private void LoadShiftList()
        {
            if (shiftRepository == null)
            {
                return;
            }

            ShiftList.Clear();
            foreach (var shift in shiftRepository.GetShifts())
            {
                ShiftList.Add(shift);
            }
        }

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
                var staffShiftsForPeriod = ShiftList.Where(shift => shift.AppointedStaff?.StaffID == SelectedStaff.StaffID
                                                    && shift.StartTime.Month == SelectedMonth
                                                    && shift.StartTime.Year == SelectedYear).ToList();

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
            catch (Exception ex)
            {
                ErrorMessage = $"Computation failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
