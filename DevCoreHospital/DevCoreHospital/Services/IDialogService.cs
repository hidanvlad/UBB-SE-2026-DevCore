using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public interface IDialogService
    {
        Task ShowMessageAsync(string title, string message);
    }
}