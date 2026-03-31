using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories; // Points to your new Repository
using DevCoreHospital.Configuration; // For AppSettings
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public partial class MedicalEvaluationViewModel : ObservableObject
    {
        // 1. Switch from DataService to Repository
        private readonly EvaluationsRepository _repository = new();

        private List<MedicalEvaluation> _allRecords = new List<MedicalEvaluation>();
        private const int MaxSymptomsLength = 500;
        private const int MaxMedsLength = 200;
        private const int MaxNotesLength = 1000;

        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; } = new();

        // 2. Dynamic Patient ID (No longer a constant!)
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
                    }
                    else
                    {
                        ResetForm();
                    }
                    RaisePropertyChanged(nameof(IsEditing));
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

        public RelayCommand SaveDiagnosisCommand { get; }
        public RelayCommand DeleteEvaluationCommand { get; } // Added back

        public MedicalEvaluationViewModel()
        {
            SaveDiagnosisCommand = new RelayCommand(SaveDiagnosis, CanSaveDiagnosis);
            DeleteEvaluationCommand = new RelayCommand(ExecuteDeletion, () => IsEditing);

            InitializeSession();
        }

        private void InitializeSession()
        {
            // Task: Fetch actual active patient from SQL
            PatientId = _repository.GetActivePatientId(AppSettings.DefaultDoctorId);
            PopulateHistory();
            CheckDoctorFatigue();
        }

        private void ValidateMedsConflict(string currentMeds)
        {
            if (string.IsNullOrWhiteSpace(currentMeds)) { IsConflictVisible = false; return; }

            // Task 12: Check database for high-risk medicine warnings
            string warning = _repository.GetHighRiskMedicineWarning(currentMeds);
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
            if (IsFatigued) return false;
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
                _repository.UpdateEvaluationNotes(SelectedEvaluation.EvaluationID, this.DoctorNotes);
                SelectedEvaluation = null;
            }
            else
            {
                var newRecord = new MedicalEvaluation
                {
                    PatientId = this.PatientId, // Use dynamic ID
                    Symptoms = IsConflictVisible && IsRiskAssumed ? $"⚠️ [RISK] - {Symptoms}" : Symptoms,
                    MedsList = this.MedsList,
                    Notes = this.DoctorNotes,
                    EvaluationDate = DateTime.Now,
                    Evaluator = new DevCoreHospital.Models.Doctor { StaffID = AppSettings.DefaultDoctorId }
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
            SelectedEvaluation = null;

            RaisePropertyChanged(nameof(Symptoms));
            RaisePropertyChanged(nameof(MedsList));
            RaisePropertyChanged(nameof(DoctorNotes));
            RaisePropertyChanged(nameof(IsRiskAssumed));
            RaisePropertyChanged(nameof(IsConflictVisible));
            RaisePropertyChanged(nameof(ConflictVisibility));
            RaisePropertyChanged(nameof(NotesBackground));
            RaisePropertyChanged(nameof(SelectedEvaluation));
            RaisePropertyChanged(nameof(IsEditing));

            RefreshButtonState();
        }

        private void RefreshButtonState()
        {
            SaveDiagnosisCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(ValidationError));
        }

        public async void PopulateHistory()
        {
            IsLoading = true;
            _allRecords.Clear();
            PastEvaluations.Clear();
            await Task.Delay(800);

            // Pull real history from SQL
            _allRecords = _repository.GetEvaluationsByDoctor(AppSettings.DefaultDoctorId.ToString());

            ApplyFilter();
            IsLoading = false;
        }

        private void CheckDoctorFatigue()
        {
            // Task 33: Check SQL for total duty hours
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