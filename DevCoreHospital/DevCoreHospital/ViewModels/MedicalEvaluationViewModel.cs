using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Configuration;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public partial class MedicalEvaluationViewModel : ObservableObject
    {
        private readonly EvaluationsRepository _repository = new();
        private List<MedicalEvaluation> _allRecords = new List<MedicalEvaluation>();

        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; } = new();
        public ObservableCollection<Appointment> AvailableAppointments { get; } = new();
        public ObservableCollection<DevCoreHospital.Models.Doctor> AllDoctors { get; } = new();

        #region Properties

        private DevCoreHospital.Models.Doctor? _selectedDoctor;
        public DevCoreHospital.Models.Doctor? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value))
                {
                    if (value != null)
                    {
                        CurrentDoctorName = $"Dr. {value.FirstName} {value.LastName}";
                        InitializeSession();
                    }
                }
            }
        }

        private string _currentDoctorName = "Physician";
        public string CurrentDoctorName
        {
            get => _currentDoctorName;
            set => SetProperty(ref _currentDoctorName, value);
        }

        private Appointment? _selectedAppointment;
        public Appointment? SelectedAppointment
        {
            get => _selectedAppointment;
            set
            {
                if (SetProperty(ref _selectedAppointment, value))
                {
                    if (value != null)
                    {
                        PatientId = value.Notes;
                        ResetFormForNewSelection();
                    }
                }
            }
        }

        private string _patientId = string.Empty;
        public string PatientId
        {
            get => _patientId;
            set => SetProperty(ref _patientId, value);
        }

        private MedicalEvaluation? _selectedEvaluation;
        public MedicalEvaluation? SelectedEvaluation
        {
            get => _selectedEvaluation;
            set
            {
                if (SetProperty(ref _selectedEvaluation, value))
                {
                    if (value != null)
                    {
                        Symptoms = value.Symptoms;
                        MedsList = value.MedsList;
                        DoctorNotes = value.Notes;
                        PatientId = value.PatientId;
                    }
                    else
                    {
                        ResetForm();
                    }
                    // CRITICAL FIX: Refresh buttons when history item is selected
                    RaisePropertyChanged(nameof(IsEditing));
                    DeleteEvaluationCommand.RaiseCanExecuteChanged();
                    SaveDiagnosisCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsEditing => SelectedEvaluation != null;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        private string _symptoms = string.Empty;
        public string Symptoms
        {
            get => _symptoms;
            set { if (SetProperty(ref _symptoms, value)) RefreshButtonState(); }
        }

        private string _medsList = string.Empty;
        public string MedsList
        {
            get => _medsList;
            set { if (SetProperty(ref _medsList, value)) { ValidateMedsConflict(value); RefreshButtonState(); } }
        }

        private string _doctorNotes = string.Empty;
        public string DoctorNotes
        {
            get => _doctorNotes;
            set { if (SetProperty(ref _doctorNotes, value)) RefreshButtonState(); }
        }

        private string _validationError = string.Empty;
        public string ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
        }

        private string _conflictWarning = string.Empty;
        public string ConflictWarning
        {
            get => _conflictWarning;
            set => SetProperty(ref _conflictWarning, value);
        }

        private bool _isConflictVisible;
        public bool IsConflictVisible
        {
            get => _isConflictVisible;
            set
            {
                if (SetProperty(ref _isConflictVisible, value))
                {
                    RaisePropertyChanged(nameof(NotesBackground));
                    RaisePropertyChanged(nameof(ConflictVisibility));
                    IsRiskAssumed = false;
                    RefreshButtonState();
                }
            }
        }

        public Visibility ConflictVisibility => IsConflictVisible ? Visibility.Visible : Visibility.Collapsed;

        private bool _isRiskAssumed;
        public bool IsRiskAssumed
        {
            get => _isRiskAssumed;
            set { if (SetProperty(ref _isRiskAssumed, value)) RefreshButtonState(); }
        }

        public Brush NotesBackground => IsConflictVisible
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(100, 255, 255, 0))
            : new SolidColorBrush(Colors.Transparent);

        private bool _isFatigued;
        public bool IsFatigued
        {
            get => _isFatigued;
            set
            {
                if (SetProperty(ref _isFatigued, value))
                {
                    RaisePropertyChanged(nameof(IsFormEnabled));
                    RaisePropertyChanged(nameof(LockoutVisibility));
                    RefreshButtonState();
                }
            }
        }

        public bool IsFormEnabled => !IsFatigued;
        public Visibility LockoutVisibility => IsFatigued ? Visibility.Visible : Visibility.Collapsed;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (SetProperty(ref _isLoading, value)) { RaisePropertyChanged(nameof(IsEmptyStateVisible)); RaisePropertyChanged(nameof(EmptyStateVisibility)); } }
        }

        public bool IsEmptyStateVisible => !IsLoading && PastEvaluations.Count == 0;
        public Visibility EmptyStateVisibility => IsEmptyStateVisible ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        public RelayCommand SaveDiagnosisCommand { get; }
        public RelayCommand DeleteEvaluationCommand { get; }

        public MedicalEvaluationViewModel()
        {
            SaveDiagnosisCommand = new RelayCommand(SaveDiagnosis, CanSaveDiagnosis);
            DeleteEvaluationCommand = new RelayCommand(ExecuteDeletion, () => IsEditing);

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
            AllDoctors.Clear();
            var doctors = _repository.GetAllDoctors();
            foreach (var doc in doctors) AllDoctors.Add(doc);

            _selectedDoctor = AllDoctors.FirstOrDefault(d => d.StaffID == AppSettings.DefaultDoctorId);
            if (_selectedDoctor != null)
            {
                CurrentDoctorName = $"Dr. {_selectedDoctor.FirstName} {_selectedDoctor.LastName}";
            }
        }

        private void LoadAppointments()
        {
            AvailableAppointments.Clear();
            var appointments = _repository.GetAppointmentsByDoctor(AppSettings.DefaultDoctorId);
            foreach (var app in appointments)
            {
                AvailableAppointments.Add(app);
            }
        }

        private void ValidateMedsConflict(string currentMeds)
        {
            if (string.IsNullOrWhiteSpace(currentMeds) || string.IsNullOrWhiteSpace(PatientId))
            {
                IsConflictVisible = false;
                return;
            }

            //Check the High-Risk Medicine Table 
            string? warning = _repository.GetHighRiskMedicineWarning(currentMeds);

            //Check the Patient's actual History for "Allergy" or "Adverse Reactions"
            string? historyWarning = _repository.CheckPatientHistoryForRisk(PatientId, currentMeds);

            if (!string.IsNullOrEmpty(warning) || !string.IsNullOrEmpty(historyWarning))
            {
                ConflictWarning = warning ?? historyWarning ?? string.Empty;
                IsConflictVisible = true;
            }
            else
            {
                IsConflictVisible = false;
            }
        }

        private bool CanSaveDiagnosis()
        {
            if (IsFatigued) return false;
            if (string.IsNullOrWhiteSpace(PatientId) || PatientId == "N/A" || PatientId == string.Empty) return false;
            if (string.IsNullOrWhiteSpace(Symptoms) || string.IsNullOrWhiteSpace(DoctorNotes))
            {
                ValidationError = "⚠️ Symptoms and Doctor Notes are required.";
                return false;
            }
            if (IsConflictVisible && !IsRiskAssumed)
            {
                ValidationError = "⚠️ You must acknowledge the clinical risk.";
                return false;
            }
            ValidationError = string.Empty;
            return true;
        }

        private void SaveDiagnosis()
        {
            if (IsEditing && SelectedEvaluation != null)
            {
                // This would be an update function in a full system
                SelectedEvaluation = null;
            }
            else
            {
                var newRecord = new MedicalEvaluation
                {
                    PatientId = this.PatientId,
                    Symptoms = Symptoms,
                    MedsList = this.MedsList,
                    Notes = this.DoctorNotes,
                    EvaluationDate = DateTime.Now,
                    Evaluator = new DevCoreHospital.Models.Doctor(AppSettings.DefaultDoctorId,"", "", "", "",true,"","Available",DoctorStatus.AVAILABLE,0)
                };

                _repository.SaveEvaluation(newRecord);
            }

            ResetForm();
            PopulateHistory();
        }

        public void ResetForm()
        {
            Symptoms = string.Empty;
            MedsList = string.Empty;
            DoctorNotes = string.Empty;
            IsRiskAssumed = false;
            IsConflictVisible = false;
            _selectedEvaluation = null; // Use backing field to avoid triggering logic
            SelectedAppointment = null;
            PatientId = string.Empty;

            NotifyAllProperties();
            RefreshButtonState();
        }

        private void ResetFormForNewSelection()
        {
            Symptoms = string.Empty;
            MedsList = string.Empty;
            DoctorNotes = string.Empty;
            IsRiskAssumed = false;
            IsConflictVisible = false;
            _selectedEvaluation = null;

            NotifyAllProperties();
            RefreshButtonState();
        }

        private void NotifyAllProperties()
        {
            RaisePropertyChanged(nameof(Symptoms));
            RaisePropertyChanged(nameof(MedsList));
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
            await Task.Delay(500);

            _allRecords = _repository.GetEvaluationsByDoctor(AppSettings.DefaultDoctorId.ToString());

            ApplyFilter();
            IsLoading = false;
        }

        private void ApplyFilter()
        {
            PastEvaluations.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allRecords
                : _allRecords.Where(r => r.PatientId.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var record in filtered) { PastEvaluations.Add(record); }
            RaisePropertyChanged(nameof(IsEmptyStateVisible));
            RaisePropertyChanged(nameof(EmptyStateVisibility));
        }

        private void CheckDoctorFatigue()
        {
            double fatigueHours = _repository.GetDoctorFatigueHours(AppSettings.DefaultDoctorId.ToString());
            IsFatigued = fatigueHours >= 12.0;
        }

        public void ExecuteDeletion()
        {
            if (SelectedEvaluation == null) return;
            _repository.DeleteEvaluation(SelectedEvaluation.EvaluationID);
            ResetForm();
            PopulateHistory();
        }



    }
}