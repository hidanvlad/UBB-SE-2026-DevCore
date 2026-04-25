using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class HangoutViewModel : ObservableObject
    {
        private const int MinHangoutTitleLength = 5;
        private const int MaxHangoutTitleLength = 25;
        private const int MaxHangoutDescriptionLength = 100;
        private const int MinDaysAheadForHangoutCreation = 7;

        private readonly IHangoutService hangoutService;
        private readonly IDoctorAppointmentService? doctorService;

        public ObservableCollection<int> MaximumParticipantsOptions { get; } = new ObservableCollection<int> { 2, 3, 4, 5, 10, 15, 20 };

        public ObservableCollection<Hangout> Hangouts { get; } = new ObservableCollection<Hangout>();
        public ObservableCollection<DoctorScheduleViewModel.DoctorOption> Doctors { get; } = new ObservableCollection<DoctorScheduleViewModel.DoctorOption>();

        private DoctorScheduleViewModel.DoctorOption? selectedDoctor;
        public DoctorScheduleViewModel.DoctorOption? SelectedDoctor
        {
            get => selectedDoctor;
            set
            {
                SetProperty(ref selectedDoctor, value);
                CreateCommand.RaiseCanExecuteChanged();
            }
        }

        private string title = string.Empty;
        public string Title
        {
            get => title;
            set
            {
                SetProperty(ref title, value);
                CreateCommand.RaiseCanExecuteChanged();
            }
        }

        private string description = string.Empty;
        public string Description
        {
            get => description;
            set
            {
                SetProperty(ref description, value);
                CreateCommand.RaiseCanExecuteChanged();
            }
        }

        private DateTimeOffset selectedDate = DateTimeOffset.Now.AddDays(MinDaysAheadForHangoutCreation);
        public DateTimeOffset SelectedDate
        {
            get => selectedDate;
            set
            {
                SetProperty(ref selectedDate, value);
                CreateCommand.RaiseCanExecuteChanged();
            }
        }

        private int maximumParticipants = 5;
        public int MaximumParticipants
        {
            get => maximumParticipants;
            set
            {
                SetProperty(ref maximumParticipants, value);
                CreateCommand.RaiseCanExecuteChanged();
            }
        }

        private string errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => errorMessage;
            set => SetProperty(ref errorMessage, value);
        }

        private string successMessage = string.Empty;
        public string SuccessMessage
        {
            get => successMessage;
            set => SetProperty(ref successMessage, value);
        }

        public RelayCommand CreateCommand { get; }

        public HangoutViewModel(IHangoutService hangoutService, IDoctorAppointmentService doctorService)
        {
            this.hangoutService = hangoutService;
            this.doctorService = doctorService;

            CreateCommand = new RelayCommand(CreateHangout, CanCreateHangout);
            LoadHangouts();
            _ = LoadDoctorsAsync();
        }

        public HangoutViewModel(IHangoutService hangoutService)
        {
            this.hangoutService = hangoutService;
            this.doctorService = null;
            CreateCommand = new RelayCommand(CreateHangout, CanCreateHangout);
            LoadHangouts();
        }

        private async Task LoadDoctorsAsync()
        {
            if (doctorService == null)
            {
                return;
            }

            Doctors.Clear();
            try
            {
                var allDoctors = await doctorService.GetAllDoctorsAsync();
                string GetDoctorName((int DoctorId, string DoctorName) doctor) => doctor.DoctorName;
                foreach (var doctor in allDoctors.OrderBy(GetDoctorName))
                {
                    Doctors.Add(new DoctorScheduleViewModel.DoctorOption
                    {
                        DoctorId = doctor.DoctorId,
                        DoctorName = doctor.DoctorName,
                        FirstName = DoctorScheduleViewModel.DoctorOption.SplitFirstLast(doctor.DoctorName).FirstName,
                        LastName = DoctorScheduleViewModel.DoctorOption.SplitFirstLast(doctor.DoctorName).LastName
                    });
                }

                if (Doctors.Any())
                {
                    SelectedDoctor = Doctors.First();
                }
            }
            catch (Exception exception)
            {
                ErrorMessage = $"Failed to load doctors: {exception.Message}";
            }
        }

        private void LoadHangouts()
        {
            Hangouts.Clear();
            foreach (var hangout in hangoutService.GetAllHangouts())
            {
                Hangouts.Add(hangout);
            }
        }

        private bool CanCreateHangout() =>
            Title.Length >= MinHangoutTitleLength &&
            Title.Length <= MaxHangoutTitleLength &&
            Description.Length <= MaxHangoutDescriptionLength &&
            SelectedDoctor != null;

        private void CreateHangout()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            try
            {
                var currentDoctor = new Models.Doctor
                {
                    StaffID = SelectedDoctor!.DoctorId,
                    FirstName = SelectedDoctor.FirstName,
                    LastName = SelectedDoctor.LastName,
                };

                hangoutService.CreateHangout(Title, Description, SelectedDate.DateTime, MaximumParticipants, currentDoctor);
                SuccessMessage = "Hangout created successfully!";
                LoadHangouts();

                Title = string.Empty;
                Description = string.Empty;
            }
            catch (Exception exception)
            {
                ErrorMessage = exception.Message;
            }
        }

        public void JoinHangoutById(int hangoutId)
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
                var currentDoctor = new Models.Doctor
                {
                    StaffID = SelectedDoctor.DoctorId,
                    FirstName = SelectedDoctor.FirstName,
                    LastName = SelectedDoctor.LastName,
                };

                hangoutService.JoinHangout(hangoutId, currentDoctor);
                SuccessMessage = "Joined hangout successfully!";
                LoadHangouts();
            }
            catch (Exception exception)
            {
                ErrorMessage = exception.Message;
            }
        }
    }
}
