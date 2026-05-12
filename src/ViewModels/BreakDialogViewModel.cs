namespace TimeTracker.ViewModels;

public class BreakDialogViewModel
{
    public int BreakMinutes { get; set; }
    public bool BreakAlreadyTaken { get; private set; }
    public bool Confirmed { get; private set; }

    public BreakDialogViewModel(int defaultMinutes) => BreakMinutes = defaultMinutes;

    public void ConfirmAlreadyTaken() { BreakAlreadyTaken = true; Confirmed = true; }
    public void AddBreakToEnd() { BreakAlreadyTaken = false; Confirmed = true; }
}
