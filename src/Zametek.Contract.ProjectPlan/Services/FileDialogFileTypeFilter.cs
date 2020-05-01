using System.Collections.Generic;
using System.Linq;

namespace Zametek.Contract.ProjectPlan
{
    public class FileDialogFileTypeFilter
    {
        private readonly List<FileTypeFilter> _filters = new List<FileTypeFilter>();

        public IReadOnlyList<IFileTypeFilter> Filters => _filters;

        public string DefaultExtension => _filters.First()?.FileExtension ?? "*.*";

        /// <summary>
        /// Creates a <see cref="FileDialogFileTypeFilter"/> given pairs of strings as 
        /// parameters. 
        /// <para>
        /// The first and every other subsequent string should be a 
        /// <em>File Type Display Name</em>. The second and every other subsequent
        /// string should be a <em>File Extension</em>.
        /// </para>
        /// <para>If no values or invalid values are provided, then the 
        /// <b>All files (*.*)|*.*</b> filter will be applied.
        /// </para>
        /// <para>Valid <em>File Extensions</em>s are "ext", ".ext" and "*.ext".</para>
        /// For example:
        /// <code>
        /// var dialogFilter = FileDialogFileTypeFilter.Create("Text Files", ".txt",
        /// "MS Project", ".mpp");
        /// <para />
        /// var filter = dialogFilter.ToFileDialogFilterString();
        /// <para />
        /// // filter will be "Text Files (*.txt)|*.txt|MS Project (*.mpp)|*.mpp";
        /// </code>
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public FileDialogFileTypeFilter(params string[] values)
        {
            if (values?.Length == 0 || values.Length % 2 != 0)
            {
                _filters.Add(new FileTypeFilter("All Files", "*.*"));
            }
            else
            {
                for (var i = 0; i < values.Length - 1; i += 2)
                {
                    _filters.Add(new FileTypeFilter(values[i], values[i + 1]));
                }
            }
        }

        /// <summary>
        /// Converts the <see cref="FileDialogFileTypeFilter"/> into a valid filter 
        /// for a <see cref="Microsoft.Win32.FileDialog"/>.
        /// <para />
        /// For example: "Ext Files (*.ext)|*.ext|Ext2 Files (*.ex2)|*.ex2"
        /// </summary>
        /// <returns></returns>
        public string ToFileDialogFilterString()
        {
            return string.Join('|', _filters.Select(_ => _.ToString()));
        }
    }
}
