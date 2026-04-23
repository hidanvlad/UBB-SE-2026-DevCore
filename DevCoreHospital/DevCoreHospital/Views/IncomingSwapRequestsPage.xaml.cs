using DevCoreHospital.ViewModels.Doctor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class IncomingSwapRequestsPage : Page
    {
        public IncomingSwapRequestsViewModel ViewModel { get; }

        public IncomingSwapRequestsPage()
        {
            this.InitializeComponent();

            ViewModel = App.Services.GetRequiredService<IncomingSwapRequestsViewModel>();
            DataContext = ViewModel;
        }
    }
}
