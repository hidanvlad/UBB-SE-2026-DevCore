using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevCoreHospital.Models;
using DevCoreHospital.Data;
using System.Threading.Tasks;

namespace DevCoreHospital.ViewModels
{
    public partial class MedicalEvaluationViewModel : ObservableObject
    {
        private readonly MedicalDataService _dataService = new();
        private const string CurrentDoctorId = "DOC001";
        private const string CurrentPatientId = "7759376";

        // Master list to hold all records for filtering
        private List<MedicalEvaluation> _allRecords = new List<MedicalEvaluation>();

        private const int MaxSymptomsLength = 500;
        private const int MaxMedsLength = 200;
        private const int MaxNotesLength = 1000;

        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; } = new();

        // --- TASK 36: Search Logic ---
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private void ApplyFilter()
        {
            PastEvaluations.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allRecords
                : _allRecords.Where(r => r.PatientId.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var record in filtered)
            {
                PastEvaluations.Add(record);
            }

            // Ensure empty state appears if search finds nothing
            OnPropertyChanged(nameof(IsEmptyStateVisible));
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
            set
            {
                if (SetProperty(ref _medsList, value))
                {
                    ValidateMedsConflict(value);
                    RefreshButtonState();
                }
            }
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
                    OnPropertyChanged(nameof(NotesBackground));
                    IsRiskAssumed = false;
                    RefreshButtonState();
                }
            }
        }

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
                    OnPropertyChanged(nameof(IsFormEnabled));
                    OnPropertyChanged(nameof(LockoutVisibility));
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
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(IsEmptyStateVisible));
                }
            }
        }

        public bool IsEmptyStateVisible => !IsLoading && PastEvaluations.Count == 0;

        public MedicalEvaluationViewModel()
        {
            PopulateHistory();
            CheckDoctorFatigue();
        }

        private void ValidateMedsConflict(string currentMeds)
        {
            if (string.IsNullOrWhiteSpace(currentMeds))
            {
                IsConflictVisible = false;
                return;
            }

            var history = _dataService.GetPatientMedicalHistory(CurrentPatientId);
            var riskKeywords = new[] { "Allergy", "Adverse Reaction", "Allergic" };

            foreach (var record in history)
            {
                bool hasRiskKeyword = riskKeywords.Any(k =>
                    (record.Symptoms?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));

                if (hasRiskKeyword)
                {
                    var drugsTyped = currentMeds.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var drug in drugsTyped)
                    {
                        if (drug.Length > 3 && record.Symptoms.Contains(drug, StringComparison.OrdinalIgnoreCase))
                        {
                            ConflictWarning = $"⚠️ CONFLICT: Historical allergy to '{drug}' detected!";
                            IsConflictVisible = true;
                            return;
                        }
                    }
                }
            }
            IsConflictVisible = false;
        }

        private bool CanSaveDiagnosis()
        {
            if (IsFatigued) return false;
            if (string.IsNullOrWhiteSpace(Symptoms) || string.IsNullOrWhiteSpace(DoctorNotes))
            {
                ValidationError = "⚠️ Symptoms and Doctor Notes are required.";
                return false;
            }
            if (Symptoms.Length > MaxSymptomsLength || DoctorNotes.Length > MaxNotesLength || MedsList.Length > MaxMedsLength)
            {
                ValidationError = "⚠️ Text exceeds database limits.";
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

        [RelayCommand(CanExecute = nameof(CanSaveDiagnosis))]
        private void SaveDiagnosis()
        {
            string finalSymptoms = this.Symptoms;
            if (IsConflictVisible && IsRiskAssumed)
            {
                finalSymptoms = $"⚠️ [RISK ACKNOWLEDGED] - {finalSymptoms}";
            }

            var newRecord = new MedicalEvaluation
            {
                PatientId = CurrentPatientId,
                Symptoms = finalSymptoms,
                MedsList = this.MedsList,
                Notes = this.DoctorNotes,
                EvaluationDate = DateTime.Now,
                Evaluator = new DevCoreHospital.Models.Doctor { Id = CurrentDoctorId, Name = "Dr. Vlad" }
            };

            _dataService.SaveEvaluation(newRecord);

            // TASK 36: Add to master list and re-filter
            _allRecords.Insert(0, newRecord);
            ApplyFilter();

            _dataService.UpdateAppointmentStatus(CurrentPatientId, "Finished");
            _dataService.UpdateDoctorAvailability(CurrentDoctorId);

            ResetForm();
            CheckDoctorFatigue();
        }

        private void ResetForm()
        {
            _symptoms = string.Empty;
            _medsList = string.Empty;
            _doctorNotes = string.Empty;
            _isRiskAssumed = false;
            _isConflictVisible = false;

            OnPropertyChanged(nameof(Symptoms));
            OnPropertyChanged(nameof(MedsList));
            OnPropertyChanged(nameof(DoctorNotes));
            OnPropertyChanged(nameof(IsRiskAssumed));
            OnPropertyChanged(nameof(IsConflictVisible));
            OnPropertyChanged(nameof(NotesBackground));

            RefreshButtonState();
        }

        private void RefreshButtonState()
        {
            SaveDiagnosisCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ValidationError));
        }

        public async void PopulateHistory()
        {
            IsLoading = true;
            _allRecords.Clear();
            PastEvaluations.Clear();

            await System.Threading.Tasks.Task.Delay(1500);

            var records = _dataService.GetEvaluationsByDoctor(CurrentDoctorId);
            _allRecords = records; // Store in master list

            ApplyFilter(); // Sync ObservableCollection
            IsLoading = false;
        }

        private void CheckDoctorFatigue()
        {
            double fatigueHours = _dataService.GetDoctorFatigueHours(CurrentDoctorId);
            IsFatigued = fatigueHours >= 12.0;
            if (IsFatigued) _dataService.CreateAdminFatigueAlert(CurrentDoctorId);
        }
    }
}