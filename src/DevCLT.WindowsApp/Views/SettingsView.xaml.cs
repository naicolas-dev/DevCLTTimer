using System.Windows.Input;
using DevCLT.WindowsApp.ViewModels;

namespace DevCLT.WindowsApp.Views;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.IsRecording)
        {
            vm.OnKeyRecorded(e);
            e.Handled = true;
        }
    }
}
