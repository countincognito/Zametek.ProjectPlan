using Zametek.Common.Project;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingManager
    {
        Common.Project.v0_1_0.ArrowGraphSettingsDto GetArrowGraphSettings();
        Common.Project.v0_1_0.ResourceSettingsDto GetResourceSettings();
    }
}
