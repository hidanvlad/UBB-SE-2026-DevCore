using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevCoreHospital.Models;

namespace DevCoreHospital.ViewModels
{
    public class MedicalEvaluationViewModel : INotifyPropertyChanged
    {
        private string _symptoms = string.Empty;
        private string _medsList = string.Empty;
        private string _doctorNotes = string.Empty;

        public string Symptoms
        {
            get => _symptoms;
            set
            {
                _symptoms = value;
                OnPropertyChanged();
            }
        }

        public string MedsList
        {
            get => _medsList;
            set
            {
                _medsList = value;
                OnPropertyChanged();
            }
        }

        public string DoctorNotes
        {
            get => _doctorNotes;
            set
            {
                _doctorNotes = value;
                OnPropertyChanged();
            }
        }

        
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void GenerateRecord()
        {
            
            var newRecord = new MedicalEvaluation
            {
                Symptoms = this.Symptoms,
                MedsList = this.MedsList,
                DoctorNotes = this.DoctorNotes
            };

            System.Diagnostics.Debug.WriteLine($"RECORD GENERATED:");
            System.Diagnostics.Debug.WriteLine($"Symptoms: {Symptoms}");
            System.Diagnostics.Debug.WriteLine($"Meds: {MedsList}");
        }
    }
}