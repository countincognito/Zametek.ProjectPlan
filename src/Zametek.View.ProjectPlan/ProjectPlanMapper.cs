using Avalonia.Platform.Storage;
using Riok.Mapperly.Abstractions;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public partial class ProjectPlanMapper
    {
        public partial FileFilter ToFileFilter(FilePickerFileType src);

        public partial FilePickerFileType ToFilePickerFileType(FileFilter src);
    }
}
