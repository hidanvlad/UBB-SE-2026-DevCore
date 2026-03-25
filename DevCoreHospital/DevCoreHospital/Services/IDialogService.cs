using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public interface IDialogService
    {
        void SetXamlRoot(XamlRoot xamlRoot);
        Task ShowMessageAsync(string title, string message);
    }
}