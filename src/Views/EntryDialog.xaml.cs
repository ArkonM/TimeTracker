using System.Windows;
using TimeTracker.Models;
using TimeTracker.ViewModels;

namespace TimeTracker.Views;

public partial class EntryDialog : Window
{
    public EntryDialogViewModel ViewModel { get; }

    public EntryDialog(TimeEntry? existing, DateOnly date, double? vacationBalance = null)
    {
        InitializeComponent();
        ViewModel = new EntryDialogViewModel { VacationBalance = vacationBalance };

        if (existing != null)
            ViewModel.LoadFromEntry(existing);
        else
        {
            ViewModel.Date = date;
            ViewModel.BreakMinutes = 30;
        }

        DataContext = ViewModel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid)
        {
            System.Windows.MessageBox.Show("Please enter valid start and end times (HH:mm) where end is after start.",
                "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
