using System;
using System.Windows;

namespace CAIME
{
    class ResizeViewModel : BaseViewModel
    {
        private readonly ProjectManager _projectManager;

        private uint oldWidth;
        public string OldWidth
        {
            get
            {
                return oldWidth.ToString();
            }
            set
            {
                if (!uint.TryParse(value, out oldWidth))
                {
                    LoggerViewModel.Log("Old width value should be a positive integer!", LogLevel.Error);
                    MessageBox.Show("Error. Old width value should be a positive integer!", "Parsing error");
                }
                else if ((uint)oldWidth % 2 == 1)
                {
                    LoggerViewModel.Log("Old width value needs to be an even number!", LogLevel.Error);
                    MessageBox.Show("Error. Old width value needs to be an even number!", "Parsing error");
                }
                else
                {
                    OnPropertyChanged(nameof(OldWidth));
                }
            }
        }

        private uint oldHeight;
        public string OldHeight
        {
            get
            {
                return oldHeight.ToString();
            }
            set
            {
                if (!uint.TryParse(value, out oldHeight))
                {
                    LoggerViewModel.Log("Old height value should be a positive integer!", LogLevel.Error);
                    MessageBox.Show("Error. Old height value should be a positive integer!", "Parsing error");
                }
                else
                {
                    OnPropertyChanged(nameof(OldHeight));
                }
            }
        }

        private uint newWidth;
        public string NewWidth
        {
            get
            {
                return newWidth.ToString();
            }
            set
            {
                if (!uint.TryParse(value, out newWidth))
                {
                    LoggerViewModel.Log("New width value should be a positive integer!", LogLevel.Error);
                    MessageBox.Show("Error. New width value should be a positive integer!", "Parsing error");
                }
                else if ((uint)newWidth % 2 == 1)
                {
                    LoggerViewModel.Log("New width value needs to be an even number!", LogLevel.Error);
                    MessageBox.Show("Error. New width value needs to be an even number!", "Parsing error");
                }
                else
                {
                    OnPropertyChanged(nameof(NewWidth));
                }
            }
        }
        
        private uint newHeight;
        public string NewHeight
        {
            get
            {
                return newHeight.ToString();
            }
            set
            {
                if (!uint.TryParse(value, out newHeight))
                {
                    LoggerViewModel.Log("New height value should be a positive integer!", LogLevel.Error);
                    MessageBox.Show("Error. New height value should be a positive integer!", "Parsing error");
                }
                else
                {
                    OnPropertyChanged(nameof(NewHeight));
                }
            }
        }

        private int newPadRight;
        public string NewPadRight
        {
            get
            {
                return newPadRight.ToString();
            }
            set
            {
                if (!int.TryParse(value, out newPadRight))
                {
                    LoggerViewModel.Log("Pad right value should be an integer!", LogLevel.Error);
                    MessageBox.Show("Error. Pad right value should be an integer!", "Parsing error");
                }
                else
                {
                    
                    if (((uint)oldWidth + newPadRight + newPadLeft) <= 0)
                    {
                        LoggerViewModel.Log("Pad right value would result in a non-positive width!", LogLevel.Error);
                        MessageBox.Show("Error. Pad right value would result in a non-positive width!", "Parsing error");
                    }
                    if (((uint)oldWidth + newPadRight + newPadLeft) % 2 == 1)
                    {
                        LoggerViewModel.Log("Pad right value would result in an odd-valued width!", LogLevel.Error);
                        MessageBox.Show("Error. Pad right value would result in an odd-valued width!", "Parsing error");
                    }
                    else
                    {
                        newWidth = (uint)((int)oldWidth + newPadRight + newPadLeft);
                        OnPropertyChanged(nameof(NewPadRight));
                        OnPropertyChanged(nameof(NewWidth));
                    }
                }
            }
        }

        private int newPadLeft;
        public string NewPadLeft
        {
            get
            {
                return newPadLeft.ToString();
            }
            set
            {
                if (!int.TryParse(value, out newPadLeft))
                {
                    LoggerViewModel.Log("Pad left value should be an integer!", LogLevel.Error);
                    MessageBox.Show("Error. Pad left value should be an integer!", "Parsing error");
                }
                else
                {
                    if (((int)oldWidth + newPadRight + newPadLeft) <= 0)
                    {
                        LoggerViewModel.Log("Pad left value would result in a non-positive width!", LogLevel.Error);
                        MessageBox.Show("Error. Pad left value would result in a non-positive width!", "Parsing error");
                    }
                    else if (((uint)oldWidth + newPadRight + newPadLeft) % 2 == 1)
                    {
                        LoggerViewModel.Log("Pad left value would result in an odd-valued width!", LogLevel.Error);
                        MessageBox.Show("Error. Pad left value would result in an odd-valued width!", "Parsing error");
                    }
                    else
                    {
                        newWidth = (uint)((int)oldWidth + newPadRight + newPadLeft);
                        OnPropertyChanged(nameof(NewPadLeft));
                        OnPropertyChanged(nameof(NewWidth));
                    }
                }
            }
        }

        private int newPadTop;
        public string NewPadTop
        {
            get
            {
                return newPadTop.ToString();
            }
            set
            {
                if (!int.TryParse(value, out newPadTop))
                {
                    LoggerViewModel.Log("Pad top value should be an integer!", LogLevel.Error);
                    MessageBox.Show("Error. Pad top value should be an integer!", "Parsing error");
                }
                else
                {
                    if (((int)oldHeight + newPadTop + newPadBottom) <= 0)
                    {
                        LoggerViewModel.Log("Pad top value would result in a non-positive height!", LogLevel.Error);
                        MessageBox.Show("Error. Pad top value would result in a non-positive height!", "Parsing error");
                    }
                    else
                    {
                        newHeight = (uint)((int)oldHeight + newPadTop + newPadBottom);
                        OnPropertyChanged(nameof(NewPadTop));
                        OnPropertyChanged(nameof(NewHeight));
                    }
                }
            }
        }

        private int newPadBottom;
        public string NewPadBottom
        {
            get
            {
                return newPadBottom.ToString();
            }
            set
            {
                if (!int.TryParse(value, out newPadBottom))
                {
                    LoggerViewModel.Log("Pad bottom value should be an integer!", LogLevel.Error);
                    MessageBox.Show("Error. Pad bottom value should be an integer!", "Parsing error");
                }
                else
                {
                    if (((int)oldHeight + newPadTop + newPadBottom) <= 0)
                    {
                        LoggerViewModel.Log("Pad bottom value would result in a non-positive height!", LogLevel.Error);
                        MessageBox.Show("Error. Pad bottom value would result in a non-positive height!", "Parsing error");
                    }
                    else
                    {
                        newHeight = (uint)((int)oldHeight + newPadTop + newPadBottom);
                        OnPropertyChanged(nameof(NewPadBottom));
                        OnPropertyChanged(nameof(NewHeight));
                    }
                }
            }
        }

        public ResizeViewModel(ProjectManager projectManager)
        {
            _projectManager     = projectManager;
            OldWidth            = _projectManager.Project.MapWidth.ToString();
            OldHeight           = _projectManager.Project.MapHeight.ToString();
            NewWidth            = OldWidth;
            NewHeight           = OldHeight;
            NewPadRight         = "0";
            NewPadLeft          = "0";
            NewPadTop           = "0";
            NewPadBottom        = "0";
        }

        public bool ResizeProject()
        {
            _projectManager.ResizeProject(newWidth, newHeight, newPadRight, newPadLeft, newPadTop, newPadBottom);
            return true;
        }
    }
}
