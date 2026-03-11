using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.ObjectModel;
using System.IO;
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
    private bool _updatingMode;

    public ObservableCollection<string> logLines { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        UpdateMode();
    }

    private void Log(CommandEvent e)
        => DispatcherQueue.TryEnqueue(() => logLines.Add($"[{e.At:HH:mm:ss}] {e.Message}"));

    private void LogError(string message)
        => DispatcherQueue.TryEnqueue(() => logLines.Add($"[ERROR] {message}"));

    private void SetBusy(bool busy)
    {
        var enabled = !busy;
        if (runButton        is not null) runButton.IsEnabled        = enabled;
        if (listButton       is not null) listButton.IsEnabled       = enabled;
        if (modeEnterToggle  is not null) modeEnterToggle.IsEnabled  = enabled;
        if (modeCreateToggle is not null) modeCreateToggle.IsEnabled = enabled;
        if (modeDeleteToggle is not null) modeDeleteToggle.IsEnabled = enabled;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
        => logLines.Clear();

    private void ModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingMode) return;
        _updatingMode = true;

        if (modeEnterToggle  is not null) modeEnterToggle.IsChecked  = (sender == modeEnterToggle);
        if (modeCreateToggle is not null) modeCreateToggle.IsChecked = (sender == modeCreateToggle);
        if (modeDeleteToggle is not null) modeDeleteToggle.IsChecked = (sender == modeDeleteToggle);

        _updatingMode = false;
        UpdateMode();
    }

    private void ModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_updatingMode) return;
        ((ToggleButton)sender).IsChecked = true;
    }

    private void UpdateMode()
    {
        if (runButton is null || enterOptions is null || createOptions is null || deleteOptions is null)
            return;

        enterOptions.Visibility  = Visibility.Collapsed;
        createOptions.Visibility = Visibility.Collapsed;
        deleteOptions.Visibility = Visibility.Collapsed;

        if (modeEnterToggle?.IsChecked == true)
        {
            runButton.Content = "Enter";
            enterOptions.Visibility = Visibility.Visible;
        }
        else if (modeCreateToggle?.IsChecked == true)
        {
            runButton.Content = "Create";
            createOptions.Visibility = Visibility.Visible;
        }
        else if (modeDeleteToggle?.IsChecked == true)
        {
            runButton.Content = "Delete";
            deleteOptions.Visibility = Visibility.Visible;
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

        SetBusy(true);

        try
        {
            ICommand command;
            string op;

            if (modeCreateToggle?.IsChecked == true)
            {
                op = "Create";
                var distroId = (distroCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "ubuntu";
                var distro = DistroSpec.Find(distroId);

                var wantInstallDir = createInstallDirCheck.IsChecked == true;
                var wantWkdir      = createWkdirCheck.IsChecked == true;

                string? installDir = null;
                string? wkdir = null;

                if (wantInstallDir)
                {
                    installDir = await PickFolderAsync();
                    if (installDir is null)
                    {
                        LogError("Operation cancelled.");
                        return;
                    }
                }

                if (wantWkdir)
                {
                    if (wantInstallDir && installDir is not null)
                    {
                        wkdir = Path.Combine(installDir, envName, "wkdir");
                    }
                    else
                    {
                        wkdir = await PickFolderAsync();
                        if (wkdir is null)
                        {
                            LogError("Operation cancelled.");
                            return;
                        }
                    }
                }

                command = new CreateCommand(envName, installDir, distro, wkdir);
            }
            else if (modeEnterToggle?.IsChecked == true)
            {
                op = "Enter";
                string? dir = null;
                if (enterDirCheck.IsChecked == true)
                {
                    dir = await PickFolderAsync();
                    if (dir is null)
                    {
                        LogError("Operation cancelled.");
                        return;
                    }
                }
                command = new EnterCommand(envName, dir, newWindow: true);
            }
            else
            {
                op = "Delete";
                var keepFiles = keepFilesCheck.IsChecked == true;
                command = new DeleteCommand(envName, keepFiles);
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
            SetBusy(false);
        }
    }

    private async void List_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        SetBusy(true);

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
            SetBusy(false);
        }
    }
}
