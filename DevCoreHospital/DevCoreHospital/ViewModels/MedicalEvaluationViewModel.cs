using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

// Toolkit namespaces
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Configuration;

namespace DevCoreHospital.ViewModels
{
    public partial class MedicalEvaluationViewModel : ObservableObject
    {
        private readonly EvaluationsRepository _repository = new();
        private List<MedicalEvaluation> _allRecords = new List<MedicalEvaluation>();

        // 1. Using [ObservableProperty] - The toolkit creates the Public version (e.g. Symptoms) automatically.
        [ObservableProperty] private string _patientId = string.Empty;
        [ObservableProperty] private string _symptoms = string.Empty;
        [ObservableProperty] private string _medsList = string.Empty;
        [ObservableProperty] private string _doctorNotes = string.Empty;
        [ObservableProperty] private string _validationError = string.Empty;
        [ObservableProperty] private string _conflictWarning = string.Empty;
        [ObservableProperty] private bool _isConflictVisible;
        [ObservableProperty] private bool _isRiskAssumed;
        [ObservableProperty] private bool _isFatigued;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _searchText = string.Empty;

        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; } = new();

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
                        Symptoms = value.Symptoms ?? string.Empty;
                        MedsList = value.MedsList ?? string.Empty;
                        DoctorNotes = value.Notes ?? string.Empty;
                    }
                    else
                    {
                        ResetForm();
                    }
                    OnPropertyChanged(nameof(IsEditing));
                    SaveDiagnosisCommand.NotifyCanExecuteChanged();
                    DeleteEvaluationCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // 2. Computed Properties
        public bool IsEditing => SelectedEvaluation != null;
        public bool IsFormEnabled => !IsFatigued;
        public Visibility LockoutVisibility => IsFatigued ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ConflictVisibility => IsConflictVisible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EmptyStateVisibility => (!IsLoading && PastEvaluations.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

        public Brush NotesBackground => IsConflictVisible
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(100, 255, 255, 0))
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        // 3. Use standard IRelayCommand from the Toolkit
        public IRelayCommand SaveDiagnosisCommand { get; }
        public IRelayCommand DeleteEvaluationCommand { get; }

        public MedicalEvaluationViewModel()
        {
            // Use standard RelayCommand (ensure you deleted any local 'RelayCommand.cs' file)
            SaveDiagnosisCommand = new RelayCommand(SaveDiagnosis, CanSaveDiagnosis);
            DeleteEvaluationCommand = new RelayCommand(ExecuteDeletion, () => IsEditing);

            InitializeSession();
        }

        private void InitializeSession()
        {
            PatientId = _repository.GetActivePatientId(AppSettings.DefaultDoctorId);
            PopulateHistory();
            CheckDoctorFatigue();
        }

        partial void OnSearchTextChanged(string value)
        {
            PastEvaluations.Clear();
            var filtered = string.IsNullOrWhiteSpace(value)
                ? _allRecords
                : _allRecords.Where(r => r.PatientId.Contains(value, StringComparison.OrdinalIgnoreCase));

            foreach (var record in filtered) { PastEvaluations.Add(record); }
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }

        partial void OnMedsListChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { IsConflictVisible = false; return; }

            string? warning = _repository.GetHighRiskMedicineWarning(value);
            if (warning != null)
            {
                ConflictWarning = warning;
                IsConflictVisible = true;
            }
            else
            {
                IsConflictVisible = false;
            }
            SaveDiagnosisCommand.NotifyCanExecuteChanged();
        }

        private bool CanSaveDiagnosis()
        {
            if (IsFatigued) return false;
            if (string.IsNullOrWhiteSpace(Symptoms) || string.IsNullOrWhiteSpace(DoctorNotes)) return false;
            if (IsConflictVisible && !IsRiskAssumed) return false;
            return true;
        }

        private void SaveDiagnosis()
        {
            if (IsEditing && SelectedEvaluation != null)
            {
                _repository.UpdateEvaluationNotes(SelectedEvaluation.EvaluationID, DoctorNotes);
            }
            else
            {
                var eval = new MedicalEvaluation
                {
                    PatientId = PatientId,
                    Symptoms = IsConflictVisible && IsRiskAssumed ? $"⚠️ [RISK] - {Symptoms}" : Symptoms,
                    MedsList = MedsList,
                    Notes = DoctorNotes,
                    EvaluationDate = DateTime.Now,
                    Evaluator = new DevCoreHospital.Models.Doctor { StaffID = AppSettings.DefaultDoctorId }
                };
                _repository.SaveEvaluation(eval);
            }

            ResetForm();
            PopulateHistory();
        }

        public async void PopulateHistory()
        {
            IsLoading = true;
            PastEvaluations.Clear();
            await Task.Delay(500);
            _allRecords = _repository.GetEvaluationsByDoctor(AppSettings.DefaultDoctorId.ToString());
            foreach (var item in _allRecords) { PastEvaluations.Add(item); }
            IsLoading = false;
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }

        private void CheckDoctorFatigue()
        {
            double hours = _repository.GetDoctorFatigueHours(AppSettings.DefaultDoctorId.ToString());
            IsFatigued = hours >= 12.0;
        }

        public void ExecuteDeletion()
        {
            if (SelectedEvaluation != null)
            {
                _repository.DeleteEvaluation(SelectedEvaluation.EvaluationID);
                ResetForm();
                PopulateHistory();
            }
        }

        public void ResetForm()
        {
            Symptoms = MedsList = DoctorNotes = string.Empty;
            IsRiskAssumed = IsConflictVisible = IsEditing = false;
            SelectedEvaluation = null;
            SaveDiagnosisCommand.NotifyCanExecuteChanged();
        }
    }
}