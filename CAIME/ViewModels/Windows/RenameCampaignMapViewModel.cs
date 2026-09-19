using System;

namespace CAIME.ViewModels
{
    public class RenameCampaignMapViewModel : BaseViewModel
    {
        private readonly ProjectManager _projectManager;

        private string oldMapName;
        public string OldMapName
        {
            get
            {
                return oldMapName;
            }
            set
            {
                oldMapName = value;
                OnPropertyChanged(nameof(OldMapName));
            }
        }

        private string newMapName;
        public string NewMapName
        {
            get
            {
                return newMapName;
            }
            set
            {
                newMapName = value;
                OnPropertyChanged(nameof(NewMapName));
            }
        }

        public RenameCampaignMapViewModel(ProjectManager projectManager)
        {
            _projectManager = projectManager;

            OldMapName = projectManager.Project.MapName;
        }

        public bool ConfirmRenameMap()
        {
            if (string.IsNullOrEmpty(NewMapName))
            {
                return false;
            }

            if (_projectManager.RenameCampaignMap(NewMapName) == false)
            {
                return false;
            }

            return true;
        }
    }
}
