using System.ComponentModel;
using System.Windows;
using TimeTracker.ViewModels;

namespace TimeTracker;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ScrollToToday();
    }

    private void ScrollToToday()
    {
        var vm = (MainViewModel)DataContext;
        var today = vm.DayRows.FirstOrDefault(r => r.IsToday);
        if (today == null) return;

        DayScrollViewer.UpdateLayout();
        var container = DayScrollViewer.FindName("DayScrollViewer") as FrameworkElement;
        // Use a simpler scroll approach: scroll to the item index
        var index = vm.DayRows.IndexOf(today);
        if (index > 0)
        {
            var offset = index * 33.0;
            DayScrollViewer.ScrollToVerticalOffset(Math.Max(0, offset - 100));
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (App.Current.IsExiting) return;
        e.Cancel = true;
        Hide();
    }
}
