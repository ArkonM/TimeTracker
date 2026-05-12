using System.Windows;
using TimeTracker.ViewModels;

namespace TimeTracker.Views;

public partial class ExportDialog : Window
{
    public ExportDialogViewModel ViewModel { get; }

    public ExportDialog(int currentYear, int currentMonth)
    {
        InitializeComponent();
        ViewModel = new ExportDialogViewModel(currentYear, currentMonth);
        DataContext = ViewModel;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.SelectedMonths.Any())
        {
            System.Windows.MessageBox.Show("Please select at least one month.", "Export",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
