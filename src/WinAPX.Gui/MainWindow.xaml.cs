using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinAPX.Core;
using WinAPX.Core.Commands;
using Windows.Storage.Pickers;
using WinRT.Interop;
namespace WinAPX.Gui;

public sealed class ExportedAppVm
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Command { get; set; } = "";
    public string EnvName { get; set; } = "";
    public ImageSource? IconSource { get; set; }
}

public sealed partial class MainWindow : Window
{
    private readonly CommandDispatcher dispatcher = new();
    private CancellationTokenSource? cancellationTokenSource;
    private bool _updatingMode;

    public ObservableCollection<ExportedAppVm> ExportedApps { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        SystemBackdrop = new MicaBackdrop();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        UpdateMode();
        ((FrameworkElement)Content).Loaded += (_, _) =>
        {
            // Resize to a DPI-aware size so the window occupies the same logical
            // footprint regardless of display scaling (100 %, 150 %, 200 %, …).
            // Base logical size: 820 × 640 DIPs.
            // At 150 % (developer setup): 1230 × 960 physical px on a 1080p screen.
            var scale = Content.XamlRoot?.RasterizationScale ?? 1.0;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)(820 * scale),
                (int)(640 * scale)));

            _ = PopulateEnvDropdownAsync();
        };

        envNameCombo.SelectionChanged += (_, _) => _ = RefreshAppsListAsync();
    }

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

    private async Task ShowInfoAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Done",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        try { await dialog.ShowAsync(); }
        catch { }
    }

    private void SetBusy(bool busy)
    {
        var enabled = !busy;
        if (runButton is not null)
        {
            runButton.IsEnabled = enabled;
        }
        if (exportEnvButton is not null)
        {
            exportEnvButton.IsEnabled = enabled;
        }
        if (importEnvButton is not null)
        {
            importEnvButton.IsEnabled = enabled;
        }
        if (modeEnterToggle is not null)
        {
            modeEnterToggle.IsEnabled = enabled;
        }
        if (modeCreateToggle is not null)
        {
            modeCreateToggle.IsEnabled = enabled;
        }
        if (modeDeleteToggle is not null)
        {
            modeDeleteToggle.IsEnabled = enabled;
        }
        if (envNameCombo is not null)
        {
            envNameCombo.IsEnabled = enabled;
        }
        if (commandLoadingRing is not null)
        {
            commandLoadingRing.IsActive   = busy;
            commandLoadingRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingMode)
        {
            return;
        }
        _updatingMode = true;

        if (modeEnterToggle is not null)
        {
            modeEnterToggle.IsChecked = ReferenceEquals(sender, modeEnterToggle);
        }
        if (modeCreateToggle is not null)
        {
            modeCreateToggle.IsChecked = ReferenceEquals(sender, modeCreateToggle);
        }
        if (modeDeleteToggle is not null)
        {
            modeDeleteToggle.IsChecked = ReferenceEquals(sender, modeDeleteToggle);
        }

        _updatingMode = false;
        UpdateMode();
        if (modeEnterToggle?.IsChecked == true || modeDeleteToggle?.IsChecked == true)
        {
            _ = PopulateEnvDropdownAsync();
        }
    }

    private void ModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_updatingMode)
        {
            return;
        }
        ((ToggleButton)sender).IsChecked = true;
    }

    private void UpdateMode()
    {
        if (runButton is null || manageOptions is null || createOptions is null || deleteOptions is null)
        {
            return;
        }

        manageOptions.Visibility = Visibility.Collapsed;
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
            runButton.Content        = "Enter";
            manageOptions.Visibility = Visibility.Visible;
        }
        else if (modeCreateToggle?.IsChecked == true)
        {
            runButton.Content        = "Create";
            createOptions.Visibility = Visibility.Visible;
        }
        else if (modeDeleteToggle?.IsChecked == true)
        {
            runButton.Content        = "Delete";
            deleteOptions.Visibility = Visibility.Visible;
        }

        // Footer buttons: Export env → Manage only; Import env → Create only
        if (exportEnvButton is not null)
        {
            exportEnvButton.Visibility = modeEnterToggle?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }
        if (importEnvButton is not null)
        {
            importEnvButton.Visibility = modeCreateToggle?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SwitchToManageMode()
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
            {
                envNameCombo.Items.Add(d);
            }
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

    private async Task RefreshAppsListAsync()
    {
        ExportedApps.Clear();
        var envName = envNameCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(envName)) return;
        await Task.Run(() =>
        {
            foreach (var app in ListAppsCommand.EnumerateAll(envName))
            {
                ImageSource? icon = null;
                try
                {
                    if (File.Exists(app.IconPath))
                        icon = new BitmapImage(new Uri(app.IconPath));
                }
                catch { }
                DispatcherQueue.TryEnqueue(() => ExportedApps.Add(new ExportedAppVm
                {
                    Id = app.Id,
                    DisplayName = app.DisplayName,
                    Command = app.Command,
                    EnvName = app.EnvName,
                    IconSource = icon
                }));
            }
        });
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync().AsTask();
        return folder?.Path;
    }

    private async Task<string?> PickSaveFileAsync(string filterName, string filterExt, string suggestedName)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeChoices.Add(filterName, new List<string> { filterExt });
        picker.SuggestedFileName = suggestedName;
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickOpenFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        foreach (var ext in extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    // ── Footer: export environment ────────────────────────────────────────────

    private async void ExportEnv_Click(object sender, RoutedEventArgs e)
    {
        var envName = envNameCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(envName))
        {
            await ShowErrorAsync("Select an environment first.");
            return;
        }

        var outPath = await PickSaveFileAsync("Tar archive", ".tar", $"{envName}.tar");
        if (outPath is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var cmd = new ExportEnvCommand(envName, outPath);
            var r = await Task.Run(() => dispatcher.RunAsync(cmd, _ => { }, CancellationToken.None));
            if (!r.Ok)
            {
                await ShowErrorAsync(r.Error ?? "Export failed.");
            }
            else
            {
                await ShowInfoAsync($"Exported to:\n{outPath}");
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Manage mode: export new app ───────────────────────────────────────────

    private async void ExportApp_Click(object sender, RoutedEventArgs e)
    {
        var envName = envNameCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(envName))
        {
            await ShowErrorAsync("Select an environment first.");
            return;
        }

        // Build dialog
        var displayNameBox = new TextBox { PlaceholderText = "Display name (e.g. Firefox)", HorizontalAlignment = HorizontalAlignment.Stretch };
        var commandBox     = new TextBox { PlaceholderText = "Command (e.g. firefox --private-window)", HorizontalAlignment = HorizontalAlignment.Stretch };
        var iconPathText   = new TextBlock { Text = "No icon selected", Opacity = 0.6 };
        var iconPickBtn    = new Button { Content = "Choose icon…" };
        var outputDirText  = new TextBlock { Text = "Default: Desktop", Opacity = 0.6 };
        var outputDirBtn   = new Button { Content = "Choose output folder…" };
        var launchCheck    = new CheckBox { Content = "Launch after export", IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };

        string? chosenIcon = null;
        string? chosenOutputDir = null;

        iconPickBtn.Click += async (_, _) =>
        {
            var path = await PickOpenFileAsync(".ico", ".png");
            if (path is not null)
            {
                chosenIcon = path;
                iconPathText.Text = path;
            }
        };
        outputDirBtn.Click += async (_, _) =>
        {
            var path = await PickFolderAsync();
            if (path is not null)
            {
                chosenOutputDir = path;
                outputDirText.Text = path;
            }
        };

        var panel = new StackPanel { Spacing = 6, MinWidth = 500 };
        panel.Children.Add(new TextBlock { Text = "Display name", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(displayNameBox);
        panel.Children.Add(new TextBlock { Text = "Command", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        panel.Children.Add(commandBox);
        panel.Children.Add(new TextBlock { Text = "Icon (optional)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        panel.Children.Add(iconPathText);
        panel.Children.Add(iconPickBtn);
        panel.Children.Add(new TextBlock { Text = "Output folder (optional)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        panel.Children.Add(outputDirText);
        panel.Children.Add(outputDirBtn);
        panel.Children.Add(launchCheck);

        var dialog = new ContentDialog
        {
            Title = $"Export app from '{envName}'",
            Content = new ScrollViewer { Content = panel, MinWidth = 500, MaxHeight = 560 },
            PrimaryButtonText = "Export",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var displayName = displayNameBox.Text?.Trim() ?? "";
        var command     = commandBox.Text?.Trim() ?? "";
        var launchAfter = launchCheck.IsChecked == true;

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(command))
        {
            await ShowErrorAsync("Display name and command are required.");
            return;
        }

        SetBusy(true);
        try
        {
            var cmd = new ExportAppCommand(envName, displayName, command, chosenIcon, chosenOutputDir, launchAfter);
            var r = await Task.Run(() => dispatcher.RunAsync(cmd, _ => { }, CancellationToken.None));
            if (!r.Ok)
            {
                await ShowErrorAsync(r.Error ?? "Export failed.");
                return;
            }
            await RefreshAppsListAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Manage mode: launch app ───────────────────────────────────────────────

    private async void LaunchApp_Click(object sender, RoutedEventArgs e)
    {
        var envName = envNameCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(envName))
        {
            return;
        }
        var appId = (string)((Button)sender).Tag;
        var r = await Task.Run(() => dispatcher.RunAsync(new LaunchAppCommand(envName, appId), _ => { }, CancellationToken.None));
        if (!r.Ok)
        {
            await ShowErrorAsync(r.Error ?? "Launch failed.");
        }
    }

    // ── Manage mode: delete app ───────────────────────────────────────────────

    private async void DeleteApp_Click(object sender, RoutedEventArgs e)
    {
        var envName = envNameCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(envName))
        {
            return;
        }
        var appId = (string)((Button)sender).Tag;
        var vm = ExportedApps.FirstOrDefault(a => a.Id == appId);
        var displayName = vm?.DisplayName ?? appId;

        var confirm = new ContentDialog
        {
            Title = "Remove exported app?",
            Content = $"This will remove the Desktop shortcut and WinAPX's copy for '{displayName}'.\n\n" +
                      $"The app itself remains installed in '{envName}'.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var r = await Task.Run(() => dispatcher.RunAsync(new DeleteAppCommand(envName, appId), _ => { }, CancellationToken.None));
            if (!r.Ok)
            {
                await ShowErrorAsync(r.Error ?? "Remove failed.");
                return;
            }
            await RefreshAppsListAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Footer: import environment ────────────────────────────────────────────

    private async void ImportEnv_Click(object sender, RoutedEventArgs e)
    {
        var nameBox      = new TextBox { PlaceholderText = "New environment name", MinWidth = 440 };
        var archiveText  = new TextBlock { Text = "No archive selected", Opacity = 0.6 };
        var archiveBtn   = new Button { Content = "Select archive…" };
        var installCheck = new CheckBox { Content = "Custom install folder" };

        string? tarPath = null;

        archiveBtn.Click += async (_, _) =>
        {
            var path = await PickOpenFileAsync(".tar", ".gz");
            if (path is not null)
            {
                tarPath = path;
                archiveText.Text = path;
            }
        };

        var panel = new StackPanel { Spacing = 8, MinWidth = 440 };
        panel.Children.Add(new TextBlock { Text = "Environment name", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = "Archive file", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(archiveText);
        panel.Children.Add(archiveBtn);
        panel.Children.Add(installCheck);

        var dialog = new ContentDialog
        {
            Title = "Import environment",
            Content = panel,
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var envName = nameBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(envName))
        {
            await ShowErrorAsync("Enter an environment name.");
            return;
        }
        if (tarPath is null)
        {
            await ShowErrorAsync("Select an archive file.");
            return;
        }

        string? installDir = null;
        if (installCheck.IsChecked == true)
        {
            installDir = await PickFolderAsync();
            if (installDir is null)
            {
                return;
            }
        }

        SetBusy(true);
        try
        {
            var cmd = new ImportEnvCommand(envName, tarPath, installDir);
            var r = await Task.Run(() => dispatcher.RunAsync(cmd, _ => { }, CancellationToken.None));
            if (!r.Ok)
            {
                await ShowErrorAsync(r.Error ?? "Import failed.");
                return;
            }
            SwitchToManageMode();
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
        finally
        {
            SetBusy(false);
        }
    }

    // ── Run button (Manage / Create / Delete) ─────────────────────────────────

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
            envName = selected?.Trim() ?? "";
        }

        if (envName.Length == 0)
        {
            await ShowErrorAsync("Please enter an environment name.");
            return;
        }

        // Confirm before deleting
        if (modeDeleteToggle?.IsChecked == true)
        {
            var confirm = new ContentDialog
            {
                Title = "Delete environment?",
                Content = $"This will permanently unregister '{envName}' from WSL and remove its files.\n\n" +
                          $"All exported app shortcuts for this environment will also be removed.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        SetBusy(true);

        try
        {
            ICommand command;
            string op;

            if (modeEnterToggle?.IsChecked == true)
            {
                op = "Enter";
                string? dir = null;
                if (enterDirCheck.IsChecked == true)
                {
                    dir = await PickFolderAsync();
                    if (dir is null)
                    {
                        return;
                    }
                }
                command = new EnterCommand(envName, dir, newWindow: true);
            }
            else if (modeCreateToggle?.IsChecked == true)
            {
                op = "Create";
                var distroId = (distroCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "ubuntu";
                var distro = DistroSpec.Find(distroId);

                var wantInstallDir  = createInstallDirCheck.IsChecked == true;
                var wantWkdir       = createWkdirCheck.IsChecked == true;
                var recommendedPkgs = createRecommendedPkgsCheck.IsChecked == true;

                string? installDir = null;
                string? wkdir = null;

                if (wantInstallDir)
                {
                    installDir = await PickFolderAsync();
                    if (installDir is null)
                    {
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
                            return;
                        }
                    }
                }

                command = new CreateCommand(envName, installDir, distro, wkdir, installRecommendedPkgs: recommendedPkgs);
            }
            else
            {
                op = "Delete";
                var keepFiles = keepFilesCheck.IsChecked == true;
                command = new DeleteCommand(envName, keepFiles);
            }

            var result = await Task.Run(
                () => dispatcher.RunAsync(command, _ => { }, cancellationTokenSource.Token),
                cancellationTokenSource.Token);

            if (!result.Ok)
            {
                await ShowErrorAsync(result.Error ?? $"{op} failed.");
            }
            else if (modeCreateToggle?.IsChecked == true)
            {
                SwitchToManageMode();
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
        catch (TaskCanceledException) { /* user cancelled folder picker */ }
        finally
        {
            SetBusy(false);
        }
    }
}
