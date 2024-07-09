using AutoMapper;
using Avalonia.Platform.Storage;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class MapperProfile
        : Profile
    {
        public MapperProfile()
        {
            CreateMap<IFileFilter, FilePickerFileType>().ReverseMap();
        }
    }
}
