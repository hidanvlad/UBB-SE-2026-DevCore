using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Services
{
    public interface IDialogService
    {
        void SetXamlRoot(XamlRoot xamlRoot);
        Task ShowMessageAsync(string title, string message);
    }
}