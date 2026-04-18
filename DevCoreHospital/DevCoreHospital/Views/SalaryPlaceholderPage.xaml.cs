using DevCoreHospital.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class SalaryPlaceholderPage : Page
    {
        public SalaryComputationViewModel ViewModel { get; }

        public SalaryPlaceholderPage()
        {
            this.InitializeComponent();

            ViewModel = new SalaryComputationViewModel();
            this.DataContext = ViewModel;
        }
    }
}