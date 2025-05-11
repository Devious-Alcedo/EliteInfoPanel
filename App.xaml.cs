
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

