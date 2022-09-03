using System.ComponentModel;

namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public enum GraphCompilationErrorCode
    {
        [Description(@"Missing dependencies")]
        C0010,
        [Description(@"Circular dependencies")]
        C0020,
        [Description(@"Invalid constraints")]
        C0030,
        [Description(@"All resources are marked as explicit targets, but not all activities have targeted resources")]
        C0040
    }
}
