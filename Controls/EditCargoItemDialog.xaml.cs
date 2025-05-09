using System.Windows;
using System.Windows.Controls;

namespace EliteInfoPanel.Controls
{
    public partial class EditCargoItemDialog : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(EditCargoItemDialog),
                new PropertyMetadata("Edit Cargo Item"));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public EditCargoItemDialog()
        {
            InitializeComponent();
            this.Loaded += EditCargoItemDialog_Loaded;
        }

        private void EditCargoItemDialog_Loaded(object sender, RoutedEventArgs e)
        {
            DialogTitle.Text = Title;
        }
    }
}