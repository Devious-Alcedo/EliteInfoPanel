// CardViewModel.cs
namespace EliteInfoPanel.ViewModels
{
    public abstract class CardViewModel : ViewModelBase
    {
        private string _title;
        private bool _isVisible;

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

        protected CardViewModel(string title)
        {
            _title = title;
            _isVisible = true;
        }
    }
}