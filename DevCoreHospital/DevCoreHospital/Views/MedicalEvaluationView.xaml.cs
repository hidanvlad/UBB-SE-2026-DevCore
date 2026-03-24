using Microsoft.UI.Xaml.Controls;
using DevCoreHospital.ViewModels;

namespace DevCoreHospital.Views
{
    public sealed partial class MedicalEvaluationView : UserControl
    {
        
        public MedicalEvaluationViewModel ViewModel { get; } = new MedicalEvaluationViewModel();

        public MedicalEvaluationView()
        {
            this.InitializeComponent();
        }
    }
}