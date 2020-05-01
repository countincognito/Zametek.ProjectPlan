using Microsoft.Win32;
using System.IO;
using System.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class FileDialogService
        : IFileDialogService
    {
        #region Private Method

        private bool OpenResult(
        string initialDirectory,
        FileDialogFileTypeFilter filter,
        FileDialog dlg
        )
        {
            dlg.InitialDirectory = initialDirectory;
            dlg.DefaultExt = filter.DefaultExtension;
            dlg.Filter = filter.ToFileDialogFilterString();

            bool? result = dlg.ShowDialog();
            FileInfo fileInfo = null;
            DirectoryInfo directoryInfo = null;
            if (!string.IsNullOrEmpty(dlg.FileName))
            {
                fileInfo = new FileInfo(dlg.FileName);
                directoryInfo = fileInfo.Directory;
            }
            Filename = fileInfo != null ? fileInfo.FullName : null;
            Directory = directoryInfo != null ? directoryInfo.FullName : null;
            return result.GetValueOrDefault();
        }

        #endregion

        #region IFileDialogService Members

        public string Filename
        {
            get;
            private set;
        }

        public string Directory
        {
            get;
            private set;
        }

        public bool ShowSaveDialog(
            string initialDirectory,
            FileDialogFileTypeFilter filter)
        {
            return OpenResult(initialDirectory, filter, new SaveFileDialog());
        }

        public bool ShowOpenDialog(
            string initialDirectory,
            FileDialogFileTypeFilter filter)
        {
            return OpenResult(initialDirectory, filter, new OpenFileDialog());
        }

        #endregion
    }
}
