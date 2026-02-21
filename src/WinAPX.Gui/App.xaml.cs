using Microsoft.UI.Xaml;

namespace WinAPX.Gui;

public partial class App : Application
{
    private Window? mainWindow;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        mainWindow = new MainWindow();
        mainWindow.Activate();
    }
}
