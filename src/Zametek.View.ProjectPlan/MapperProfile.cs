using AutoMapper;
using Avalonia.Controls;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class MapperProfile
        : Profile
    {
        public MapperProfile()
        {
            CreateMap<IFileFilter, FileDialogFilter>().ReverseMap();
        }
    }
}
