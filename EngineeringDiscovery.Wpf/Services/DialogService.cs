using System.Windows;

namespace EngineeringDiscovery.Wpf.Services;

public class DialogService : IDialogService
{
    public void ShowMessage(string title, string message)
    {
        MessageBox.Show(message, title);
    }
}
