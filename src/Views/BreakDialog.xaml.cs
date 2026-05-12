using System.Windows;
using TimeTracker.ViewModels;

namespace TimeTracker.Views;

public partial class BreakDialog : Window
{
    public BreakDialogViewModel ViewModel { get; }

    public BreakDialog(int defaultBreakMinutes)
    {
        InitializeComponent();
        ViewModel = new BreakDialogViewModel(defaultBreakMinutes);
        BreakMinutesBox.Text = defaultBreakMinutes.ToString();
    }

    private void AlreadyDone_Click(object sender, RoutedEventArgs e)
    {
        ApplyBreakMinutes();
        ViewModel.ConfirmAlreadyTaken();
        DialogResult = true;
    }

    private void AddToEnd_Click(object sender, RoutedEventArgs e)
    {
        ApplyBreakMinutes();
        ViewModel.AddBreakToEnd();
        DialogResult = true;
    }

    private void ApplyBreakMinutes()
    {
        if (int.TryParse(BreakMinutesBox.Text, out var minutes) && minutes >= 0)
            ViewModel.BreakMinutes = minutes;
    }
}
