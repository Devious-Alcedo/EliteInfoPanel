using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace EliteInfoPanel.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> _propertyValues = new Dictionary<string, object>();
        private bool _suppressNotifications = false;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (!_suppressNotifications && PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets property value with change tracking and value caching to avoid unnecessary UI updates
        /// </summary>
        private static readonly HashSet<string> NoisyProperties = new()
{
    "Content",
    "CurrentPage"
};

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;

            if (!NoisyProperties.Contains(propertyName))
            {
                Log.Debug("📦 Property changed: {Property} = {Value}", propertyName, value);
            }

            OnPropertyChanged(propertyName);
            return true;
        }



        /// <summary>
        /// Sets property with caching to avoid unnecessary updates
        /// </summary>
        protected bool SetCachedProperty<T>(T value, [CallerMemberName] string propertyName = null)
        {
            if (_propertyValues.TryGetValue(propertyName, out var existingValue) &&
                Equals(existingValue, value))
                return false;

            _propertyValues[propertyName] = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Get a cached property value
        /// </summary>
        protected T GetCachedProperty<T>([CallerMemberName] string propertyName = null)
        {
            if (_propertyValues.TryGetValue(propertyName, out var value) && value is T typedValue)
                return typedValue;

            return default;
        }

        /// <summary>
        /// Suppresses property change notifications during batch updates
        /// </summary>
        protected void BatchUpdate(Action updateAction)
        {
            _suppressNotifications = true;
            try
            {
                updateAction?.Invoke();
            }
            finally
            {
                _suppressNotifications = false;
                OnPropertyChanged(string.Empty); // Notify that multiple properties may have changed
            }
        }
        public bool IsSuppressingNotifications
        {
            get => _suppressNotifications;
            set => _suppressNotifications = value;
        }
        /// <summary>
        /// Ensures the action runs on the UI thread
        /// </summary>
        protected void RunOnUIThread(Action action)
        {
            if (action == null) return;

            // Get the application dispatcher
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                // Fallback to current dispatcher if Application.Current is null (e.g., in unit tests)
                dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            }

            if (dispatcher.CheckAccess())
            {
                // We're already on the UI thread
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error executing action on UI thread");
                }
            }
            else
            {
                // We need to invoke the action on the UI thread
                // Use BeginInvoke for async or Invoke for sync execution
                try
                {
                    dispatcher.Invoke(action, System.Windows.Threading.DispatcherPriority.DataBind);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error dispatching action to UI thread");
                }
            }
        }
    }
}