using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.Threading;
using WinAPX.Core;
using WinAPX.Core.Commands;

namespace WinAPX.Gui;

public sealed partial class MainWindow : Window
{
    private readonly CommandDispatcher dispatcher = new();
    private CancellationTokenSource? cancellationTokenSource;

    public ObservableCollection<string> logLines { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Log(CommandEvent commandEvent)
        => logLines.Add($"[{commandEvent.At:HH:mm:ss}] {commandEvent.Message}");

    private void LogError(string message)
        => logLines.Add($"[ERROR] {message}");

    private void Clear_Click(object sender, RoutedEventArgs e)
        => logLines.Clear();

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        var envName = (envNameBox.Text ?? "").Trim();
        if (envName.Length == 0)
        {
            LogError("Missing environment name.");
            return;
        }

        // simplified CreateCommand: CreateCommand(string envName)
        var command = new CreateCommand(envName);
        var result = await dispatcher.RunAsync(command, Log, cancellationTokenSource.Token);

        if (!result.Ok)
            LogError(result.Error ?? "Create failed.");
    }

    private async void Enter_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        var envName = (envNameBox.Text ?? "").Trim();
        if (envName.Length == 0)
        {
            LogError("Missing environment name.");
            return;
        }

        var command = new EnterCommand(envName);
        var result = await dispatcher.RunAsync(command, Log, cancellationTokenSource.Token);

        if (!result.Ok)
            LogError(result.Error ?? "Enter failed.");
    }
}
