
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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
        // Ensure we're on the UI thread
        Current.Dispatcher.Invoke(() =>
        {
            foreach (Window window in Current.Windows)
            {
                // Refresh resource references
                window.Resources.MergedDictionaries.Clear();
                foreach (var dict in Current.Resources.MergedDictionaries)
                {
                    window.Resources.MergedDictionaries.Add(dict);
                }

                // Force refresh of dynamic resources (important for theme changes)
                window.InvalidateVisual();
                window.UpdateLayout();

                // Recursively refresh all child elements that use DynamicResource
                RefreshChildElements(window);
            }

            // Also refresh the Application's main resource dictionary
            // This ensures top-level resource changes are properly applied
            var appDict = new ResourceDictionary();
            foreach (var key in Current.Resources.Keys)
            {
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

