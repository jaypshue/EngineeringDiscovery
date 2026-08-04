using System.Windows;
using WinForms = System.Windows.Forms;

namespace EngineeringDiscovery.Wpf.Services;

public class DialogService : IDialogService
{
    public void ShowMessage(string title, string message)
    {
        // Prefer WPF MessageBox to keep dialogs consistent with WPF host
        System.Windows.MessageBox.Show(message, title);
    }
}
