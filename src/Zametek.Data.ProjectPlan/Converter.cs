using AutoMapper;

namespace Zametek.Data.ProjectPlan
{
    public static class Converter
    {
        private readonly static IMapper m_Mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();

        public static v0_4_0.ProjectPlanModel Format(Common.ProjectPlan.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return m_Mapper.Map<Common.ProjectPlan.ProjectPlanModel, v0_4_0.ProjectPlanModel>(projectPlan);
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_4_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return m_Mapper.Map<v0_4_0.ProjectPlanModel, Common.ProjectPlan.ProjectPlanModel>(projectPlan);
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_3_2.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return Upgrade(v0_4_0.Converter.Upgrade(m_Mapper, projectPlan));
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_3_1.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return Upgrade(v0_3_2.Converter.Upgrade(m_Mapper, projectPlan));
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_3_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return Upgrade(v0_3_1.Converter.Upgrade(m_Mapper, projectPlan));
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_2_1.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return Upgrade(v0_3_0.Converter.Upgrade(m_Mapper, projectPlan));
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_2_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return Upgrade(v0_2_1.Converter.Upgrade(m_Mapper, projectPlan));
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_1_0.ProjectPlanModel projectPlan)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            return Upgrade(v0_2_0.Converter.Upgrade(projectPlan));
        }
    }
}
