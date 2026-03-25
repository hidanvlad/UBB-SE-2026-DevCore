using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevCoreHospital.Models;
using DevCoreHospital.Data;
using System.Diagnostics;
using Microsoft.UI.Xaml; // Required for Visibility

namespace DevCoreHospital.ViewModels
{
    public class MedicalEvaluationViewModel : INotifyPropertyChanged
    {
        private readonly MedicalDataService _dataService = new MedicalDataService();
        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; set; } = new ObservableCollection<MedicalEvaluation>();

        private string _symptoms = string.Empty;
        public string Symptoms { get => _symptoms; set { _symptoms = value; OnPropertyChanged(); } }

        private bool _isFatigued;
        public bool IsFatigued
        {
            get => _isFatigued;
            set
            {
                _isFatigued = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(LockoutVisibility));
            }
        }

        // Logic switch for TextBoxes and Buttons
        public bool IsFormEnabled => !IsFatigued;

        // Visibility switch for the Red Overlay
        public Visibility LockoutVisibility => IsFatigued ? Visibility.Visible : Visibility.Collapsed;

        public RelayCommand SaveDiagnosisCommand { get; }

        public MedicalEvaluationViewModel()
        {
            SaveDiagnosisCommand = new RelayCommand(SaveDiagnosis, CanSaveDiagnosis);
            PopulateHistory();
            CheckDoctorFatigue();
        }

        // returns TRUE if fatigue is under 12 hours
        public bool CanSaveDiagnosis()
        {
            double fatigueHours = _dataService.GetDoctorFatigueHours("DOC001");
            return fatigueHours < 12.0;
        }

        public void PopulateHistory()
        {
            PastEvaluations.Clear();
            var records = _dataService.GetEvaluationsByDoctor("DOC001");

            foreach (var record in records)
            {
                PastEvaluations.Add(record);
            }
        }

        private void SaveDiagnosis()
        {
            if (!CanSaveDiagnosis()) return;

            var newRecord = new MedicalEvaluation
            {
                Symptoms = Symptoms,
                EvaluationDate = DateTime.Now,
                Evaluator = "DOC001" // store doctor id as string
            };

            _dataService.SaveEvaluation(newRecord);
            PastEvaluations.Insert(0, newRecord);

            Symptoms = string.Empty;

            CheckDoctorFatigue();
            SaveDiagnosisCommand.RaiseCanExecuteChanged();
        }

        private void CheckDoctorFatigue()
        {
            double fatigueHours = _dataService.GetDoctorFatigueHours("DOC001");
            IsFatigued = fatigueHours >= 12.0;

            Debug.WriteLine("***************************************");
            Debug.WriteLine($"Total Duty Time: {fatigueHours:F1} hours");
            if (IsFatigued) Debug.WriteLine("!! LOCKOUT TRIGGERED !!");
            Debug.WriteLine("***************************************");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}