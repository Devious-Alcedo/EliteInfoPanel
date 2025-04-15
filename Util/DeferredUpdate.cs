using EliteInfoPanel.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    /// <summary>
    /// Helper class to batch property change notifications to reduce UI updates
    /// Use with a using statement to defer property change notifications
    /// </summary>
    public class DeferredUpdate : IDisposable
    {
        private readonly ViewModelBase _viewModel;
        private readonly bool _originalState;

        public DeferredUpdate(ViewModelBase viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _originalState = _viewModel.IsSuppressingNotifications;
            _viewModel.IsSuppressingNotifications = true;
        }

        public void Dispose()
        {
            _viewModel.IsSuppressingNotifications = _originalState;

            // If we were originally not suppressing notifications,
            // raise a property changed notification for all properties
            if (!_originalState)
            {
                // Use a method in ViewModelBase to raise the PropertyChanged event
                _viewModel.RaisePropertyChangedForAll();
            }
        }
    }

    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> _propertyValues;
        private bool _suppressNotifications;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (!_suppressNotifications)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public bool IsSuppressingNotifications
        {
            get => _suppressNotifications;
            set => _suppressNotifications = value;
        }

        // New method to raise PropertyChanged for all properties
        public void RaisePropertyChangedForAll()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }
}
