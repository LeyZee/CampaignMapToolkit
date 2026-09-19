using Microsoft.Win32;

namespace CAIME
{
    public class BackImageTool : OneTimeTool
    {
        private readonly ToolbarViewModel toolbarVM;

        public BackImageTool(ToolbarViewModel toolbarVM) : base(ToolType.BackImg, System.Windows.Input.Key.I)
        {
            this.toolbarVM = toolbarVM;
        }

        public override void ProcessClick(ViewportViewModel viewportVM)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.bmp, *.tiff, *.png) | *.jpg; *.jpeg; *.bmp; *.tiff; *.png",
                RestoreDirectory = true,
            };

            var openRes = ofd.ShowDialog();
            if (openRes.HasValue && openRes.Value == true)
            {
                viewportVM.SetBackgroundImage(ofd.FileName);
            }

            var fallbackTool = toolbarVM.GetActiveTool();
            if (fallbackTool.Type != this.Type)
            {
                toolbarVM.SetActiveTool(fallbackTool.Type);
            }
        }
    }
}
