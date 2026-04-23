using DevCoreHospital.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class SalaryPlaceholderPage : Page
    {
        public SalaryComputationViewModel ViewModel { get; }

        public SalaryPlaceholderPage()
        {
            this.InitializeComponent();

            ViewModel = App.Services.GetRequiredService<SalaryComputationViewModel>();
            this.DataContext = ViewModel;
        }
    }
}
