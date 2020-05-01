using System.Text;

namespace Zametek.Contract.ProjectPlan
{
    internal class FileTypeFilter : IFileTypeFilter
    {
        public FileTypeFilter(string fileType, string fileExtension)
        {
            FileType = fileType;
            FileExtension = CleanUpExtension(fileExtension);
        }

        private static string CleanUpExtension(string fileExtension)
        {
            var sb = new StringBuilder(fileExtension);

            if (sb[0] != '*')
            {
                sb.Insert(0, '*');
            }
            if (sb[1] != '.')
            {
                sb.Insert(1, '.');
            }

            return sb.ToString();
        }

        public string FileType { get; }
        public string FileExtension { get; }

        public override string ToString()
        {
            return $"{FileType} ({FileExtension})|{FileExtension}";
        }
    }
}
