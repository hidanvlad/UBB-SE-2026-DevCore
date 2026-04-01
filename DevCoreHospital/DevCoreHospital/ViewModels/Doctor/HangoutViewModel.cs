using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.Repositories;
using DevCoreHospital.ViewModels.Base;
using DevCoreHospital.Configuration; // Needed for AppSettings
using DevCoreHospital.Data;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class HangoutViewModel : ObservableObject
    {
        private readonly HangoutService _hangoutService;
        private readonly DatabaseManager _dbManager; // Added DatabaseManager

        public ObservableCollection<Hangout> Hangouts { get; } = new ObservableCollection<Hangout>();

        // Add Doctor Collection and Selected Doctor
        public ObservableCollection<DoctorScheduleViewModel.DoctorOption> Doctors { get; } = new();

        private DoctorScheduleViewModel.DoctorOption? _selectedDoctor;
        public DoctorScheduleViewModel.DoctorOption? SelectedDoctor
        {
            get => _selectedDoctor;
            set { SetProperty(ref _selectedDoctor, value); CreateCommand.RaiseCanExecuteChanged(); }
        }

        private string _title = string.Empty;
        public string Title { get => _title; set { SetProperty(ref _title, value); CreateCommand.RaiseCanExecuteChanged(); } }

        private string _description = string.Empty;
        public string Description { get => _description; set { SetProperty(ref _description, value); CreateCommand.RaiseCanExecuteChanged(); } }

        private DateTimeOffset _selectedDate = DateTimeOffset.Now.AddDays(7);
        public DateTimeOffset SelectedDate { get => _selectedDate; set { SetProperty(ref _selectedDate, value); CreateCommand.RaiseCanExecuteChanged(); } }

        private int _maxParticipants = 5;
        public int MaxParticipants { get => _maxParticipants; set { SetProperty(ref _maxParticipants, value); CreateCommand.RaiseCanExecuteChanged(); } }

        public ObservableCollection<int> MaxParticipantsOptions { get; } = new ObservableCollection<int> { 2, 3, 4, 5, 10, 15, 20 };

        private string _errorMessage = string.Empty;
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        private string _successMessage = string.Empty;
        public string SuccessMessage { get => _successMessage; set => SetProperty(ref _successMessage, value); }

        public RelayCommand CreateCommand { get; }

        private static HangoutRepository _globalRepo = new HangoutRepository();

        public HangoutViewModel()
        {
            _hangoutService = new HangoutService(_globalRepo);
            _dbManager = new DatabaseManager(AppSettings.ConnectionString); // Initialize DB Manager

            CreateCommand = new RelayCommand(CreateHangout, CanCreateHangout);
            LoadHangouts();
            _ = LoadDoctorsAsync(); // Load Doctors when VM initializes
        }

        private async Task LoadDoctorsAsync()
        {
            Doctors.Clear();
            try
            {
                var allDoctors = await _dbManager.GetAllDoctorsAsync();
                foreach (var d in allDoctors.OrderBy(x => x.DoctorName))
                {
                    Doctors.Add(new DoctorScheduleViewModel.DoctorOption
                    {
                        DoctorId = d.DoctorId,
                        DoctorName = d.DoctorName,
                        FirstName = DoctorScheduleViewModel.DoctorOption.SplitFirstLast(d.DoctorName).FirstName,
                        LastName = DoctorScheduleViewModel.DoctorOption.SplitFirstLast(d.DoctorName).LastName
                    });
                }

                if (Doctors.Any())
                {
                    SelectedDoctor = Doctors.First();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load doctors: {ex.Message}";
            }
        }

        private void LoadHangouts()
        {
            Hangouts.Clear();
            foreach (var h in _hangoutService.GetAllHangouts())
            {
                Hangouts.Add(h);
            }
        }

        // Must have a title, description, and a doctor selected
        private bool CanCreateHangout() => Title.Length >= 5 && Title.Length <= 25 && Description.Length <= 100 && SelectedDoctor != null;

        private void CreateHangout()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            try
            {
                // Create a doctor object based on the SelectedDoctor dropdown
                var currentDoctor = new Models.Doctor
                {
                    StaffID = SelectedDoctor!.DoctorId,
                    FirstName = SelectedDoctor.FirstName,
                    LastName = SelectedDoctor.LastName
                };

                _hangoutService.CreateHangout(Title, Description, SelectedDate.DateTime, MaxParticipants, currentDoctor);
                SuccessMessage = "Hangout created successfully!";
                LoadHangouts();

                Title = string.Empty;
                Description = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public void JoinHangoutById(int id)
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (SelectedDoctor == null)
            {
                ErrorMessage = "Please select a doctor to join the hangout.";
                return;
            }

            try
            {
                // Create a doctor object based on the SelectedDoctor dropdown
                var currentDoctor = new Models.Doctor
                {
                    StaffID = SelectedDoctor.DoctorId,
                    FirstName = SelectedDoctor.FirstName,
                    LastName = SelectedDoctor.LastName
                };

                _hangoutService.JoinHangout(id, currentDoctor);
                SuccessMessage = "Joined hangout successfully!";
                LoadHangouts();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}