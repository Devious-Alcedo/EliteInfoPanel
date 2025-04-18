using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

namespace EliteInfoPanel;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
    }
    /// <summary>
    /// Forces all windows to refresh their styling from resources
    /// </summary>
    public static void RefreshResources()
    {
        foreach (Window window in Current.Windows)
        {
            // Refresh resource references
            window.Resources.MergedDictionaries.Clear();
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                window.Resources.MergedDictionaries.Add(dict);
            }

            // Force UI update
            window.InvalidateVisual();
            window.UpdateLayout();
        }
    }
}

