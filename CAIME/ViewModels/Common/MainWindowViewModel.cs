using System;

namespace CAIME.ViewModels
{
    public class MainWindowViewModel
    {
        public EditorViewModel      EditorViewModel         { get; private set; }
        public MenuViewModel        MenuViewModel           { get; private set; }
        public PreferencesViewModel PreferencesViewModel    { get; private set; }

        public MainWindowViewModel()
        {
            PreferencesViewModel    = PreferencesViewModel.Instance;
            EditorViewModel         = new EditorViewModel(PreferencesViewModel);
            MenuViewModel           = new MenuViewModel(EditorViewModel.ProjectManager, EditorViewModel);
        }
    }
}
