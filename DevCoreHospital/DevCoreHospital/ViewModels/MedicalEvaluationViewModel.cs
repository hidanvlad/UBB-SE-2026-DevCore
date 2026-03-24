using System;
using System.Collections.ObjectModel; 
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevCoreHospital.Models;
using DevCoreHospital.Data;

namespace DevCoreHospital.ViewModels
{
    public class MedicalEvaluationViewModel : INotifyPropertyChanged
    {
        private readonly MedicalDataService _dataService = new MedicalDataService();

        //The collection that gets populated when the window loads
        public ObservableCollection<MedicalEvaluation> PastEvaluations { get; set; } = new ObservableCollection<MedicalEvaluation>();

        private string _symptoms = string.Empty;
        public string Symptoms { get => _symptoms; set { _symptoms = value; OnPropertyChanged(); } }

        public ICommand SaveDiagnosisCommand { get; }

        public MedicalEvaluationViewModel()
        {
            SaveDiagnosisCommand = new RelayCommand(SaveDiagnosis);

            //TASK 8: Populate the list with past evaluations
            PopulateHistory();
        }

        //Task 8 !
        public void PopulateHistory()
        {
            PastEvaluations.Clear();

            // Get the records from DB 
            var records = _dataService.GetEvaluationsByDoctor("DOC001");

            foreach (var record in records)
            {
                PastEvaluations.Add(record);
            }
        }

        private void SaveDiagnosis()
        {
            var newRecord = new MedicalEvaluation
            {
                Symptoms = this.Symptoms,
                EvaluationDate = DateTime.Now,
                Evaluator = new Doctor { Id = "DOC001", Name = "Dr. Vlad" }
            };

            _dataService.SaveEvaluation(newRecord);
            PastEvaluations.Insert(0, newRecord);

            Symptoms = string.Empty; // Clear the box for the next entry
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}