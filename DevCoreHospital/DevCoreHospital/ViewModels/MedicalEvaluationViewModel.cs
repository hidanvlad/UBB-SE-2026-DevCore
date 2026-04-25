using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DevCoreHospital.ViewModels
{
    public partial class MedicalEvaluationViewModel : ObservableObject
    {
        private readonly IMedicalEvaluationService evaluationService;
        private readonly ICurrentUserService currentUserService;
        private List<MedicalEvaluation> allRecords = new List<MedicalEvaluation>();

        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; } = new ObservableCollection<MedicalEvaluation>();
        public ObservableCollection<Appointment> AvailableAppointments { get; } = new ObservableCollection<Appointment>();
        public ObservableCollection<Models.Doctor> AllDoctors { get; } = new ObservableCollection<Models.Doctor>();

        #region Properties

        private Models.Doctor? selectedDoctor;
        public Models.Doctor? SelectedDoctor
        {
            get => selectedDoctor;
            set
            {
                if (SetProperty(ref selectedDoctor, value))
                {
                    if (value != null)
                    {
                        CurrentDoctorName = $"Dr. {value.FirstName} {value.LastName}";
                        InitializeSession();
                    }
                }
            }
        }

        private string currentDoctorName = "Physician";
        public string CurrentDoctorName
        {
            get => currentDoctorName;
            set => SetProperty(ref currentDoctorName, value);
        }

        private Appointment? selectedAppointment;
        public Appointment? SelectedAppointment
        {
            get => selectedAppointment;
            set
            {
                if (SetProperty(ref selectedAppointment, value))
                {
                    if (value != null)
                    {
                        PatientId = value.Notes;
                        ResetFormForNewSelection();
                    }
                }
            }
        }

        private string patientId = string.Empty;
        public string PatientId
        {
            get => patientId;
            set => SetProperty(ref patientId, value);
        }

        private MedicalEvaluation? selectedEvaluation;
        public MedicalEvaluation? SelectedEvaluation
        {
            get => selectedEvaluation;
            set
            {
                if (SetProperty(ref selectedEvaluation, value))
                {
                    if (value != null)
                    {
                        Symptoms = value.Symptoms;
                        MedicationsList = value.MedicationsList;
                        DoctorNotes = value.Notes;
                        PatientId = value.PatientId;
                    }
                    else
                    {
                        ResetForm();
                    }

                    RaisePropertyChanged(nameof(IsEditing));
                    DeleteEvaluationCommand.RaiseCanExecuteChanged();
                    SaveDiagnosisCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsEditing => SelectedEvaluation != null;

        private string searchText = string.Empty;
        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private string symptoms = string.Empty;
        public string Symptoms
        {
            get => symptoms;
            set
            {
                if (SetProperty(ref symptoms, value))
                {
                    RefreshButtonState();
                }
            }
        }

        private string medsList = string.Empty;
        public string MedicationsList
        {
            get => medsList;
            set
            {
                if (SetProperty(ref medsList, value))
                {
                    ValidateMedsConflict(value);
                    RefreshButtonState();
                }
            }
        }

        private string doctorNotes = string.Empty;
        public string DoctorNotes
        {
            get => doctorNotes;
            set
            {
                if (SetProperty(ref doctorNotes, value))
                {
                    RefreshButtonState();
                }
            }
        }

        private string validationError = string.Empty;
        public string ValidationError
        {
            get => validationError;
            set => SetProperty(ref validationError, value);
        }

        private string conflictWarning = string.Empty;
        public string ConflictWarning
        {
            get => conflictWarning;
            set => SetProperty(ref conflictWarning, value);
        }

        private bool isConflictVisible;
        public bool IsConflictVisible
        {
            get => isConflictVisible;
            set
            {
                if (SetProperty(ref isConflictVisible, value))
                {
                    RaisePropertyChanged(nameof(NotesBackground));
                    RaisePropertyChanged(nameof(ConflictVisibility));
                    IsRiskAssumed = false;
                    RefreshButtonState();
                }
            }
        }

        public Visibility ConflictVisibility => IsConflictVisible ? Visibility.Visible : Visibility.Collapsed;

        private bool isRiskAssumed;
        public bool IsRiskAssumed
        {
            get => isRiskAssumed;
            set
            {
                if (SetProperty(ref isRiskAssumed, value))
                {
                    RefreshButtonState();
                }
            }
        }

        public Brush NotesBackground => IsConflictVisible
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(100, 255, 255, 0))
            : new SolidColorBrush(Colors.Transparent);

        private bool isFatigued;
        public bool IsFatigued
        {
            get => isFatigued;
            set
            {
                if (SetProperty(ref isFatigued, value))
                {
                    RaisePropertyChanged(nameof(IsFormEnabled));
                    RaisePropertyChanged(nameof(LockoutVisibility));
                    RefreshButtonState();
                }
            }
        }

        public bool IsFormEnabled => !IsFatigued;
        public Visibility LockoutVisibility => IsFatigued ? Visibility.Visible : Visibility.Collapsed;

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set
            {
                if (SetProperty(ref isLoading, value))
                {
                    RaisePropertyChanged(nameof(IsEmptyStateVisible));
                    RaisePropertyChanged(nameof(EmptyStateVisibility));
                }
            }
        }

        public bool IsEmptyStateVisible => !IsLoading && PastEvaluations.Count == 0;
        public Visibility EmptyStateVisibility => IsEmptyStateVisible ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        public RelayCommand SaveDiagnosisCommand { get; }
        public RelayCommand DeleteEvaluationCommand { get; }

        public MedicalEvaluationViewModel(IMedicalEvaluationService evaluationService, ICurrentUserService currentUserService)
        {
            this.evaluationService = evaluationService;
            this.currentUserService = currentUserService;

            bool CanDelete() => IsEditing;
            SaveDiagnosisCommand = new RelayCommand(SaveDiagnosis, CanSaveDiagnosis);
            DeleteEvaluationCommand = new RelayCommand(ExecuteDeletion, CanDelete);

            LoadDoctorList();
            InitializeSession();
        }

        private void InitializeSession()
        {
            LoadAppointments();
            PopulateHistory();
            CheckDoctorFatigue();
        }

        private void LoadDoctorList()
        {
            AllDoctors.ReplaceWith(evaluationService.GetAllDoctors());

            bool IsCurrentUser(Models.Doctor doctor) => doctor.StaffID == currentUserService.UserId;
            selectedDoctor = AllDoctors.FirstOrDefault(IsCurrentUser);
            if (selectedDoctor != null)
            {
                CurrentDoctorName = $"Dr. {selectedDoctor.FirstName} {selectedDoctor.LastName}";
            }
        }

        private void LoadAppointments()
        {
            AvailableAppointments.ReplaceWith(evaluationService.GetAppointmentsByDoctor(currentUserService.UserId));
        }

        private void ValidateMedsConflict(string currentMeds)
        {
            if (string.IsNullOrWhiteSpace(currentMeds) || string.IsNullOrWhiteSpace(PatientId))
            {
                IsConflictVisible = false;
                return;
            }

            string? warning = evaluationService.CheckMedicineConflict(PatientId, currentMeds);

            if (!string.IsNullOrEmpty(warning))
            {
                ConflictWarning = warning;
                IsConflictVisible = true;
            }
            else
            {
                IsConflictVisible = false;
            }
        }

        private bool CanSaveDiagnosis()
        {
            if (IsFatigued)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(PatientId) || PatientId == "N/A" || PatientId == string.Empty)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Symptoms) || string.IsNullOrWhiteSpace(DoctorNotes))
            {
                ValidationError = "?? Symptoms and Doctor Notes are required.";
                return false;
            }

            if (IsConflictVisible && !IsRiskAssumed)
            {
                ValidationError = "?? You must acknowledge the clinical risk.";
                return false;
            }

            ValidationError = string.Empty;
            return true;
        }

        private void SaveDiagnosis()
        {
            if (IsEditing && SelectedEvaluation != null)
            {
                SelectedEvaluation = null;
            }
            else
            {
                var newRecord = new MedicalEvaluation
                {
                    PatientId = this.PatientId,
                    Symptoms = Symptoms,
                    MedicationsList = this.MedicationsList,
                    Notes = this.DoctorNotes,
                    EvaluationDate = DateTime.Now,
                    Evaluator = new Models.Doctor(
                        currentUserService.UserId,
                        string.Empty, string.Empty, string.Empty,
                        true, string.Empty, "Available", DoctorStatus.AVAILABLE, 0),
                };

                evaluationService.SaveEvaluation(newRecord);
            }

            ResetForm();
            PopulateHistory();
        }

        public void ResetForm()
        {
            Symptoms = string.Empty;
            MedicationsList = string.Empty;
            DoctorNotes = string.Empty;
            IsRiskAssumed = false;
            IsConflictVisible = false;
            selectedEvaluation = null;
            SelectedAppointment = null;
            PatientId = string.Empty;

            NotifyAllProperties();
            RefreshButtonState();
        }

        private void ResetFormForNewSelection()
        {
            Symptoms = string.Empty;
            MedicationsList = string.Empty;
            DoctorNotes = string.Empty;
            IsRiskAssumed = false;
            IsConflictVisible = false;
            selectedEvaluation = null;

            NotifyAllProperties();
            RefreshButtonState();
        }

        private void NotifyAllProperties()
        {
            RaisePropertyChanged(nameof(Symptoms));
            RaisePropertyChanged(nameof(MedicationsList));
            RaisePropertyChanged(nameof(DoctorNotes));
            RaisePropertyChanged(nameof(IsRiskAssumed));
            RaisePropertyChanged(nameof(IsConflictVisible));
            RaisePropertyChanged(nameof(ConflictVisibility));
            RaisePropertyChanged(nameof(NotesBackground));
            RaisePropertyChanged(nameof(SelectedEvaluation));
            RaisePropertyChanged(nameof(IsEditing));
            RaisePropertyChanged(nameof(PatientId));
            RaisePropertyChanged(nameof(CurrentDoctorName));
            RaisePropertyChanged(nameof(SelectedDoctor));
            RaisePropertyChanged(nameof(SelectedAppointment));

            DeleteEvaluationCommand.RaiseCanExecuteChanged();
        }

        private void RefreshButtonState()
        {
            SaveDiagnosisCommand.RaiseCanExecuteChanged();
            DeleteEvaluationCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(ValidationError));
        }

        public async void PopulateHistory()
        {
            IsLoading = true;
            PastEvaluations.Clear();
            const int SimulatedLoadingDelayMs = 500;
            await Task.Delay(SimulatedLoadingDelayMs);

            allRecords = evaluationService.GetEvaluationsByDoctor(currentUserService.UserId.ToString());

            ApplyFilter();
            IsLoading = false;
        }

        private void ApplyFilter()
        {
            bool RecordMatchesSearch(MedicalEvaluation record) =>
                record.PatientId.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            var filteredRecords = string.IsNullOrWhiteSpace(SearchText)
                ? allRecords
                : allRecords.Where(RecordMatchesSearch);

            PastEvaluations.ReplaceWith(filteredRecords);

            RaisePropertyChanged(nameof(IsEmptyStateVisible));
            RaisePropertyChanged(nameof(EmptyStateVisibility));
        }

        private void CheckDoctorFatigue()
        {
            IsFatigued = evaluationService.IsDoctorFatigued(currentUserService.UserId.ToString());
        }

        public void ExecuteDeletion()
        {
            if (SelectedEvaluation == null)
            {
                return;
            }

            evaluationService.DeleteEvaluation(SelectedEvaluation.EvaluationID);
            ResetForm();
            PopulateHistory();
        }
    }
}
