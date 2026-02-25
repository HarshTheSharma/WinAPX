using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using WinAPX.Core;
using WinAPX.Core.Commands;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WinAPX.Gui;

public sealed partial class MainWindow : Window
{
    private readonly CommandDispatcher dispatcher = new();
    private CancellationTokenSource? cancellationTokenSource;

    public ObservableCollection<string> logLines { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Mode_Checked(this, new RoutedEventArgs());
    }

    private void Log(CommandEvent commandEvent)
        => DispatcherQueue.TryEnqueue(() => logLines.Add($"[{commandEvent.At:HH:mm:ss}] {commandEvent.Message}"));

    private void LogError(string message)
        => DispatcherQueue.TryEnqueue(() => logLines.Add($"[ERROR] {message}"));

    private void SetButtonsEnabled(bool enabled)
    {
        modeEnterRadio?.SetValue(Control.IsEnabledProperty, enabled);
        modeCreateRadio?.SetValue(Control.IsEnabledProperty, enabled);
        modeDeleteRadio?.SetValue(Control.IsEnabledProperty, enabled);

        runButton?.SetValue(Control.IsEnabledProperty, enabled);
        listButton?.SetValue(Control.IsEnabledProperty, enabled);

        useCustomPathCheckBox?.SetValue(Control.IsEnabledProperty, enabled);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
        => logLines.Clear();

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (runButton is null || useCustomPathCheckBox is null ||
            modeEnterRadio is null || modeCreateRadio is null || modeDeleteRadio is null)
            return;

        // Reset checkbox whenever mode changes
        useCustomPathCheckBox.IsChecked = false;
        useCustomPathCheckBox.Visibility = Visibility.Visible;

        if (modeEnterRadio.IsChecked == true)
        {
            runButton.Content = "Enter";
            useCustomPathCheckBox.Content = "Start in a custom folder (Windows path)";
            useCustomPathCheckBox.IsEnabled = true;
        }
        else if (modeCreateRadio.IsChecked == true)
        {
            runButton.Content = "Create";
            useCustomPathCheckBox.Content = "Select install folder (stores VHDX + wkdir)";
            useCustomPathCheckBox.IsEnabled = true;
        }
        else
        {
            runButton.Content = "Delete";
            useCustomPathCheckBox.Content = "(not used for Delete)";
            useCustomPathCheckBox.Visibility= Visibility.Collapsed;
            useCustomPathCheckBox.IsEnabled = false;
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync().AsTask();
        return folder?.Path;
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        var envName = (envNameBox.Text ?? "").Trim();
        if (envName.Length == 0)
        {
            LogError("Missing environment name.");
            return;
        }

        SetButtonsEnabled(false);

        try
        {
            // If checkbox selected, prompt for a folder; else behave like original code.
            string? selectedPath = null;
            if (useCustomPathCheckBox?.IsChecked == true)
            {
                selectedPath = await PickFolderAsync();
                if (selectedPath is null)
                {
                    LogError("Operation cancelled.");
                    return;
                }
            }

            ICommand command;
            var op = "";

            if (modeCreateRadio?.IsChecked == true)
            {
                op = "Create";
                command = selectedPath is null
                    ? new CreateCommand(envName)
                    : new CreateCommand(envName, selectedPath);
            }
            else if (modeEnterRadio?.IsChecked == true)
            {
                op = "Enter";
                command = selectedPath is null
                    ? new EnterCommand(envName)
                    : new EnterCommand(envName, selectedPath);
            }
            else
            {
                op = "Delete";
                command = new DeleteCommand(envName);
            }

            var result = await Task.Run(
                () => dispatcher.RunAsync(command, Log, cancellationTokenSource.Token),
                cancellationTokenSource.Token);

            if (!result.Ok)
                LogError(result.Error ?? $"{op} failed.");
        }
        catch (TaskCanceledException)
        {
            LogError("Operation cancelled.");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void List_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        SetButtonsEnabled(false);

        try
        {
            var command = new ListCommand();

            var result = await Task.Run(
                () => dispatcher.RunAsync(command, Log, cancellationTokenSource.Token),
                cancellationTokenSource.Token);

            if (!result.Ok)
                LogError(result.Error ?? "List failed.");
        }
        catch (TaskCanceledException)
        {
            LogError("List cancelled.");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }
}