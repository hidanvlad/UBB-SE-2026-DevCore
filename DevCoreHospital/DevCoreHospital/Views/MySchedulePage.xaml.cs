using DevCoreHospital.ViewModels.Doctor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class MySchedulePage : Page
    {
        public MyScheduleViewModel ViewModel { get; }

        public MySchedulePage()
        {
            InitializeComponent();

            ViewModel = App.Services.GetRequiredService<MyScheduleViewModel>();
            DataContext = ViewModel;
        }
    }
}
