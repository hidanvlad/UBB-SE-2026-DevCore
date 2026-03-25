using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class DialogService : IDialogService
    {
        private XamlRoot? _xamlRoot;

        public void SetXamlRoot(XamlRoot xamlRoot) => _xamlRoot = xamlRoot;

        public async Task ShowMessageAsync(string title, string message)
        {
            if (_xamlRoot == null)
                return;

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = _xamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}