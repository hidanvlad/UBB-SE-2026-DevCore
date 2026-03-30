using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
 

namespace DevCoreHospital.ViewModels
{
    public class SalaryComputationViewModel : ObservableObject
    {
        private readonly SalaryComputationService _salaryService;
        private readonly DatabaseManager _dbManager;

        public ObservableCollection<IStaff> StaffList { get; } = new ObservableCollection<IStaff>();
        public ObservableCollection<Shift> ShiftList { get; } = new ObservableCollection<Shift>();

        private IStaff _selectedStaff;
        public IStaff SelectedStaff
        {
            get => _selectedStaff;
            set { SetProperty(ref _selectedStaff, value); ComputeSalaryCommand.RaiseCanExecuteChanged(); }
        }

        private int _selectedMonth = DateTime.Now.Month;
        public int SelectedMonth { get => _selectedMonth; set => SetProperty(ref _selectedMonth, value); }

        private int _selectedYear = DateTime.Now.Year;
        public int SelectedYear { get => _selectedYear; set => SetProperty(ref _selectedYear, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private string _errorMessage = string.Empty;
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        private string _salaryResult = string.Empty;
        public string SalaryResult { get => _salaryResult; set => SetProperty(ref _salaryResult, value); }

        public AsyncRelayCommand ComputeSalaryCommand { get; }

        public SalaryComputationViewModel()
        {
            _dbManager = new DatabaseManager(AppSettings.ConnectionString);
            _salaryService = new SalaryComputationService(_dbManager);

            ComputeSalaryCommand = new AsyncRelayCommand(ComputeSalaryAsync, CanComputeSalary);

            LoadStaffList();
            LoadShiftList();
        }

        private void LoadStaffList()
        {
            StaffList.Clear();
            var staffFromDb = _dbManager.GetStaff();
            foreach (var staff in staffFromDb)
            {
                StaffList.Add(staff);
            }
        }

        private void LoadShiftList()
        {
            ShiftList.Clear();
            var shiftsFromDb = _dbManager.GetShifts();
            foreach (var shift in shiftsFromDb)
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
                var staffShifts = ShiftList.Where(s => s.AppointedStaff?.StaffID == SelectedStaff.StaffID
                                                    && s.StartTime.Month == SelectedMonth
                                                    && s.StartTime.Year == SelectedYear).ToList();

                double salary = 0;

                if (SelectedStaff is Models.Doctor doctor)
                {
                    salary = await _salaryService.ComputeSalaryDoctorAsync(doctor, staffShifts);
                }
                else if (SelectedStaff is Models.Pharmacyst pharmacist)
                {
                    salary = await _salaryService.ComputeSalaryPharmacistAsync(pharmacist, staffShifts, SelectedMonth, SelectedYear);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported staff type for salary computation.");
                }

                SalaryResult = $"Computed Salary: ${salary:F2}";
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