// CardViewModel.cs
namespace EliteInfoPanel.ViewModels
{
    public abstract class CardViewModel : ViewModelBase
    {
        private string _title;
        private bool _isVisible;
        private int _displayColumn;
        private int _columnSpan;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public int DisplayColumn
        {
            get => _displayColumn;
            set => SetProperty(ref _displayColumn, value);
        }

        public int ColumnSpan
        {
            get => _columnSpan;
            set => SetProperty(ref _columnSpan, value);
        }

        protected CardViewModel(string title)
        {
            _title = title;
            _isVisible = true;
            _columnSpan = 1;
        }
    }
}