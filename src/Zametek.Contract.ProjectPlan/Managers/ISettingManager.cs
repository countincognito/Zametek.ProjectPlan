using Zametek.Common.Project;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingManager
    {
        ArrowGraphSettingsDto GetArrowGraphSettings();
        ResourceSettingsDto GetResourceSettings();
    }
}
