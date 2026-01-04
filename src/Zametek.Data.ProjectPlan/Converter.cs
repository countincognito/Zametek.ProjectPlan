using AutoMapper;

namespace Zametek.Data.ProjectPlan
{
    public static class Converter
    {
        private readonly static IMapper m_Mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();

        #region ProjectModels

        public static v0_6_0.ProjectModel Format(Common.ProjectPlan.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return m_Mapper.Map<Common.ProjectPlan.ProjectModel, v0_6_0.ProjectModel>(project);
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_6_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return m_Mapper.Map<v0_6_0.ProjectModel, Common.ProjectPlan.ProjectModel>(project);
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_5_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_6_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_4_4.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_5_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_4_3.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_4_4.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_4_2.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_4_3.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_4_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_4_2.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_4_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_4_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_3_2.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_4_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_3_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_3_2.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_3_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_3_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_2_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_3_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_2_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_2_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_1_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_2_0.Converter.Upgrade(project));
        }

        #endregion

        #region AppSettingsModels

        public static v0_4_4.AppSettingsModel Format(Common.ProjectPlan.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return m_Mapper.Map<Common.ProjectPlan.AppSettingsModel, v0_4_4.AppSettingsModel>(appSettings);
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_4_4.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return m_Mapper.Map<v0_4_4.AppSettingsModel, Common.ProjectPlan.AppSettingsModel>(appSettings);
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_4_1.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return m_Mapper.Map<v0_4_1.AppSettingsModel, Common.ProjectPlan.AppSettingsModel>(appSettings);
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_3_0.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return Upgrade(v0_4_1.Converter.Upgrade(appSettings));
        }

        #endregion
    }
}
