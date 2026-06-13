namespace Zametek.Data.ProjectPlan
{
    public static class Converter
    {
        private readonly static VersionMapper m_Mapper = new();


        #region ProjectModels

        public static v0_6_1.ProjectModel Format(Common.ProjectPlan.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return m_Mapper.FromCurrentToV0_6_1(project);
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_6_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return m_Mapper.FromV0_6_1ToCurrent(project);
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(v0_6_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_6_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_5_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(v0_6_0.Converter.Upgrade(m_Mapper, localNow, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_4_4.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_5_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_4_3.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_4_4.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_4_2.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_4_3.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_4_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_4_2.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_4_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_4_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_3_2.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_4_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_3_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_3_2.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_3_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_3_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_2_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_3_0.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_2_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_2_1.Converter.Upgrade(m_Mapper, project));
        }

        public static Common.ProjectPlan.ProjectModel Upgrade(DateTimeOffset localNow, v0_1_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return Upgrade(localNow, v0_2_0.Converter.Upgrade(project));
        }

        #endregion

        #region AppSettingsModels

        public static v0_6_0.AppSettingsModel Format(Common.ProjectPlan.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return m_Mapper.FromCurrentToV0_6_0(appSettings);
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_6_0.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return m_Mapper.FromV0_6_0ToCurrent(appSettings);
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_4_4.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return Upgrade(v0_6_0.Converter.Upgrade(m_Mapper, appSettings));
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_4_1.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return Upgrade(v0_4_4.Converter.Upgrade(m_Mapper, appSettings));
        }

        public static Common.ProjectPlan.AppSettingsModel Upgrade(v0_3_0.AppSettingsModel appSettings)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            return Upgrade(v0_4_1.Converter.Upgrade(appSettings));
        }

        #endregion
    }
}
