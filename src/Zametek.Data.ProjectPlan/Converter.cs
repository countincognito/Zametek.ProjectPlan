using AutoMapper;

namespace Zametek.Data.ProjectPlan
{
    public static class Converter
    {
        private readonly static IMapper m_Mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_2_0.ProjectPlanModel projectPlan)
        {
            return m_Mapper.Map<v0_2_0.ProjectPlanModel, Common.ProjectPlan.ProjectPlanModel>(projectPlan);
        }

        public static Common.ProjectPlan.ProjectPlanModel Upgrade(v0_1_0.ProjectPlanModel projectPlan)
        {
            return Upgrade(v0_2_0.Converter.Upgrade(projectPlan));
        }

        public static v0_2_0.ProjectPlanModel Format(Common.ProjectPlan.ProjectPlanModel projectPlan)
        {
            return m_Mapper.Map<Common.ProjectPlan.ProjectPlanModel, v0_2_0.ProjectPlanModel>(projectPlan);
        }
    }
}
