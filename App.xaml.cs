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
}

