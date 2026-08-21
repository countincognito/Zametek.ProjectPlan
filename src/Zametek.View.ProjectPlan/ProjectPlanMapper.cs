using Avalonia.Platform.Storage;
using Riok.Mapperly.Abstractions;
using System.Collections.Generic;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // Deep cloning is on for the same reason as the view model's own mapper: the
    // pattern lists are List<string> on both sides, so without it the file type
    // handed to the storage provider would be backed by the filter's own list.
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None, UseDeepCloning = true)]
    public partial class ProjectPlanMapper
    {
        // A file picker file type may describe itself by MIME type or by uniform
        // type identifier alone, in which case it carries no patterns at all. That
        // is a file type with nothing to match on, not a mapping failure, so it maps
        // to an empty list rather than throwing.
        public static List<string> FromNullableToDefault(IReadOnlyList<string>? src)
            => src is null ? [] : [.. src];

        public partial FileFilter ToFileFilter(FilePickerFileType src);

        public partial FilePickerFileType ToFilePickerFileType(FileFilter src);
    }
}
