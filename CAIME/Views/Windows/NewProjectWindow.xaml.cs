using System.Windows;
using System.Windows.Controls;

namespace CAIME.Windows
{
    public partial class NewProjectWindow : Window
    {
        private readonly NewProjectViewModel projectVM;

        public NewProjectWindow(ProjectManager projectManager)
        {
            InitializeComponent();

            projectVM = new NewProjectViewModel(projectManager);
            DataContext = projectVM;
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (projectVM.CreateProject())
            {
                Close();
                Owner.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Owner.Focus();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Text = StringHelper.NameFixup(tb.Text);
            }
        }
    }
}
