using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.Manager.ProjectPlan
{
    public class SettingManager
        : ISettingManager
    {
        #region Fields

        private readonly ISettingResourceAccess m_SettingResourceAccess;

        #endregion

        #region Ctors

        public SettingManager(ISettingResourceAccess settingResourceAccess)
        {
            m_SettingResourceAccess = settingResourceAccess ?? throw new ArgumentNullException(nameof(settingResourceAccess));
        }

        #endregion

        #region ISettingManager Members

        public Common.Project.v0_1_0.ArrowGraphSettingsDto GetArrowGraphSettings()
        {
            return m_SettingResourceAccess.GetArrowGraphSettings();
        }

        public Common.Project.v0_1_0.ResourceSettingsDto GetResourceSettings()
        {
            return m_SettingResourceAccess.GetResourceSettings();
        }

        #endregion
    }
}
