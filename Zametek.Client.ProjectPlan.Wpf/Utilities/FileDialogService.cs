using System.IO;
using System.Windows.Forms;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class FileDialogService
        : IFileDialogService
    {
        #region Private Method

        private DialogResult OpenResult(
            string initialDirectory,
            string associatedFileType,
            string associatedFileExtension,
            FileDialog dlg)
        {
            dlg.InitialDirectory = initialDirectory;
            dlg.DefaultExt = associatedFileExtension;
            dlg.Filter = string.Format("{0} | *{1}", associatedFileType, associatedFileExtension);
            DialogResult result = dlg.ShowDialog();
            FileInfo fileInfo = null;
            DirectoryInfo directoryInfo = null;
            if (!string.IsNullOrEmpty(dlg.FileName))
            {
                fileInfo = new FileInfo(dlg.FileName);
                directoryInfo = fileInfo.Directory;
            }
            Filename = fileInfo != null ? fileInfo.FullName : null;
            Directory = directoryInfo != null ? directoryInfo.FullName : null;
            return result;
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

        public DialogResult ShowSaveDialog(
            string initialDirectory,
            string associatedFileType,
            string associatedFileExtension)
        {
            using (var dlg = new SaveFileDialog())
            {
                return OpenResult(initialDirectory, associatedFileType, associatedFileExtension, dlg);
            }
        }

        public DialogResult ShowOpenDialog(
            string initialDirectory,
            string associatedFileType,
            string associatedFileExtension)
        {
            using (var dlg = new OpenFileDialog())
            {
                return OpenResult(initialDirectory, associatedFileType, associatedFileExtension, dlg);
            }
        }

        #endregion
    }
}
