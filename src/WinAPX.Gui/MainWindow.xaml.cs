using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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

        SystemBackdrop = new MicaBackdrop();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        UpdateMode();
        ((FrameworkElement)Content).Loaded += (_, _) => _ = PopulateEnvDropdownAsync();
    }

    private void Log(CommandEvent e)
        => DispatcherQueue.TryEnqueue(() => logLines.Add($"[{e.At:HH:mm:ss}] {e.Message}"));

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        try { await dialog.ShowAsync(); }
        catch { /* dialog already open or window closing */ }
    }

    private void SetBusy(bool busy)
    {
        var enabled = !busy;
        if (runButton          is not null) runButton.IsEnabled          = enabled;
        if (listButton         is not null) listButton.IsEnabled         = enabled;
        if (modeEnterToggle    is not null) modeEnterToggle.IsEnabled    = enabled;
        if (modeCreateToggle   is not null) modeCreateToggle.IsEnabled   = enabled;
        if (modeDeleteToggle   is not null) modeDeleteToggle.IsEnabled   = enabled;
        if (envNameCombo       is not null) envNameCombo.IsEnabled       = enabled;
        if (commandLoadingRing is not null)
        {
            commandLoadingRing.IsActive   = busy;
            commandLoadingRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
        => logLines.Clear();

    private void ShowConsole_Click(object sender, RoutedEventArgs e)
    {
        if (showConsoleMenuItem.IsChecked)
            advancedPanel.Visibility = Visibility.Visible;
        else
            advancedPanel.Visibility = Visibility.Collapsed;
    }

    private void ModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingMode) return;
        _updatingMode = true;

        if (modeEnterToggle  is not null) modeEnterToggle.IsChecked  = (sender == modeEnterToggle);
        if (modeCreateToggle is not null) modeCreateToggle.IsChecked = (sender == modeCreateToggle);
        if (modeDeleteToggle is not null) modeDeleteToggle.IsChecked = (sender == modeDeleteToggle);

        _updatingMode = false;
        UpdateMode();
        if (modeEnterToggle?.IsChecked == true || modeDeleteToggle?.IsChecked == true)
            _ = PopulateEnvDropdownAsync();
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

        var isCreate = modeCreateToggle?.IsChecked == true;
        if (isCreate)
        {
            envNameBox.Visibility   = Visibility.Visible;
            envNameCombo.Visibility = Visibility.Collapsed;
        }
        else
        {
            envNameBox.Visibility   = Visibility.Collapsed;
            envNameCombo.Visibility = Visibility.Visible;
        }

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

    private void SwitchToEnterMode()
    {
        _updatingMode = true;
        modeEnterToggle.IsChecked  = true;
        modeCreateToggle.IsChecked = false;
        modeDeleteToggle.IsChecked = false;
        _updatingMode = false;
        UpdateMode();
    }

    private async Task PopulateEnvDropdownAsync()
    {
        var previousSelection = envNameCombo.SelectedItem as string;
        envNameCombo.IsEnabled    = false;
        envLoadingRing.IsActive   = true;
        envLoadingRing.Visibility = Visibility.Visible;
        envNameCombo.Items.Clear();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var distros = await dispatcher.ListDistrosAsync(cts.Token);
            foreach (var d in distros)
                envNameCombo.Items.Add(d);
            if (envNameCombo.Items.Count > 0)
            {
                var restore = distros.FindIndex(d => string.Equals(d, previousSelection, StringComparison.OrdinalIgnoreCase));
                envNameCombo.SelectedIndex = restore >= 0 ? restore : 0;
            }
        }
        catch { /* leave empty on failure */ }
        finally
        {
            envLoadingRing.IsActive   = false;
            envLoadingRing.Visibility = Visibility.Collapsed;
            envNameCombo.IsEnabled    = true;
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

        string envName;
        if (modeCreateToggle?.IsChecked == true)
        {
            envName = (envNameBox.Text ?? "").Trim();
        }
        else
        {
            var selected = envNameCombo.SelectedItem as string;
            if (selected is null)
                envName = "";
            else
                envName = selected.Trim();
        }

        if (envName.Length == 0)
        {
            await ShowErrorAsync("Please enter an environment name.");
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
                        return;
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
                            return;
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
                        return;
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
            {
                await ShowErrorAsync(result.Error ?? $"{op} failed.");
            }
            else if (modeCreateToggle?.IsChecked == true)
            {
                SwitchToEnterMode();
                await PopulateEnvDropdownAsync();
                for (int i = 0; i < envNameCombo.Items.Count; i++)
                {
                    if (string.Equals(envNameCombo.Items[i] as string, envName, StringComparison.OrdinalIgnoreCase))
                    {
                        envNameCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (modeDeleteToggle?.IsChecked == true)
            {
                await PopulateEnvDropdownAsync();
            }
        }
        catch (TaskCanceledException) { /* user cancelled folder picker, no popup needed */ }
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
                await ShowErrorAsync(result.Error ?? "List failed.");
        }
        catch (TaskCanceledException) { }
        finally
        {
            SetBusy(false);
        }
    }
}
