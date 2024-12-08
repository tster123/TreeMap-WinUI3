namespace AvaloniaUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";

    
    private string _hoveredItem;
    public string HoveredItem
    {
        get => _hoveredItem;
        set
        {
            _hoveredItem = value;
            OnPropertyChanged(nameof(HoveredItem));
        }
    }
}
