using System;
using System.Collections.ObjectModel;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.Repositories;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class HangoutViewModel : ObservableObject
    {
        private readonly HangoutService _hangoutService;
        private readonly ICurrentUserService _currentUserService;

        public ObservableCollection<Hangout> Hangouts { get; } = new ObservableCollection<Hangout>();

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

        public OldRelayCommand CreateCommand { get; }

        // Shared static repo instance across page navigations to persist memory
        private static HangoutRepository _globalRepo = new HangoutRepository();

        public HangoutViewModel()
        {
            _hangoutService = new HangoutService(_globalRepo);
            _currentUserService = new CurrentUserService();

            CreateCommand = new OldRelayCommand(CreateHangout, CanCreateHangout);
            LoadHangouts();
        }

        private void LoadHangouts()
        {
            Hangouts.Clear();
            foreach (var h in _hangoutService.GetAllHangouts())
            {
                Hangouts.Add(h);
            }
        }

        private bool CanCreateHangout() => Title.Length >= 5 && Title.Length <= 25 && Description.Length <= 100;

        private void CreateHangout()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            try
            {
                var currentDoctor = new Models.Doctor { StaffID = _currentUserService.UserId, FirstName = "Current", LastName = "User" };
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
            try
            {
                var currentDoctor = new Models.Doctor { StaffID = _currentUserService.UserId, FirstName = "Current", LastName = "User" };
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