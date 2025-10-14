
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Services;
using System.Windows.Media;

namespace EliteInfoPanel;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static ServiceProvider Services { get; private set; }
    public App()
    {
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        // Load settings first (uses fallback if DI not yet available)
        var loadedSettings = EliteInfoPanel.Util.SettingsManager.Load();
        var gamePath = EliteInfoPanel.Core.EliteDangerousPaths.GetSavedGamesPath(loadedSettings.DevelopmentMode);

        // Register core services with the correct game path
        services.AddSingleton<IGameFilesService>(_ => new GameFilesService(gamePath));
        services.AddSingleton<ISettingsStorage, SettingsStorage>();
        services.AddSingleton<IFileWatcherService>(_ => new FileWatcherService(gamePath));
        services.AddSingleton<IJournalReader, JournalReader>();
        services.AddSingleton<ICarrierCargoService>(sp =>
        {
            var files = sp.GetRequiredService<IGameFilesService>();
            var cargoPath = files.AppDataPathFor("CarrierCargo.json");
            return new CarrierCargoService(new Core.CarrierCargoTracker(), cargoPath);
        });
        services.AddSingleton<IColonizationService, ColonizationService>();
        services.AddSingleton<IRouteProgressService>(sp =>
        {
            var files = sp.GetRequiredService<IGameFilesService>();
            var routePath = files.AppDataPathFor("RouteProgress.json");
            return new RouteProgressService(routePath);
        });
        services.AddSingleton<IStatusService, StatusService>();
        services.AddSingleton<ICarrierJumpManager>(sp => new CarrierJumpManager(Current.Dispatcher,
            () => sp.GetRequiredService<GameStateService>().IsOnFleetCarrier,
            () =>
            {
                var gs = sp.GetRequiredService<GameStateService>();
                gs.GetType().GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  ?.Invoke(gs, new object[] { nameof(GameStateService.ShowCarrierJumpOverlay) });
                gs.GetType().GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  ?.Invoke(gs, new object[] { nameof(GameStateService.CarrierJumpCountdownSeconds) });
                gs.GetType().GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  ?.Invoke(gs, new object[] { nameof(GameStateService.CarrierJumpDestination) });
            }));

        // GameStateService from DI with injected services
        services.AddSingleton<GameStateService>(sp => new GameStateService(
            gamePath,
            sp.GetRequiredService<IGameFilesService>(),
            sp.GetRequiredService<IFileWatcherService>(),
            sp.GetRequiredService<ICarrierCargoService>(),
            sp.GetRequiredService<IColonizationService>(),
            sp.GetRequiredService<IRouteProgressService>(),
            sp.GetRequiredService<IStatusService>(),
            sp.GetRequiredService<IJournalReader>(),
            sp.GetRequiredService<ICarrierJumpManager>()));

        Services = services.BuildServiceProvider();
    }

    /// <summary>
    /// Forces all windows to refresh their styling from resources
    /// </summary>
    public static void RefreshResources()
    {
        Current.Dispatcher.Invoke(() =>
        {
            // Preserve BundledTheme
            var bundledTheme = Current.Resources.MergedDictionaries
                .OfType<MaterialDesignThemes.Wpf.BundledTheme>()
                .FirstOrDefault();

            // Clear window dictionaries
            foreach (Window window in Current.Windows)
            {
                window.Resources.MergedDictionaries.Clear();

                // Re-apply all application-level dictionaries EXCEPT BundledTheme
                foreach (var dict in Current.Resources.MergedDictionaries)
                {
                    if (dict is not MaterialDesignThemes.Wpf.BundledTheme)
                        window.Resources.MergedDictionaries.Add(dict);
                }

                window.InvalidateVisual();
                window.UpdateLayout();
                RefreshChildElements(window);
            }

            // Now reset the Application dictionary itself, preserving the BundledTheme
            var appDict = new ResourceDictionary();
            if (bundledTheme != null)
                appDict.MergedDictionaries.Add(bundledTheme);

            foreach (var key in Current.Resources.Keys)
            {
                if (Current.Resources[key] is not MaterialDesignThemes.Wpf.BundledTheme)
                    appDict[key] = Current.Resources[key];
            }

            Current.Resources = appDict;
        });
    }


    // Helper method to refresh all child elements
    private static void RefreshChildElements(DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            // Force refresh any elements that use DynamicResource
            if (child is FrameworkElement element)
            {
                element.InvalidateVisual();
            }

            // Recursively process children
            RefreshChildElements(child);
        }
    }
}

