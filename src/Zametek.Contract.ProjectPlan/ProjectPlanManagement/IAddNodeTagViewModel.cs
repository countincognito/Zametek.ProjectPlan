namespace Zametek.Contract.ProjectPlan
{
    public interface IAddNodeTagViewModel
    {
        string Tag { get; set; }

        void RunValidation();
    }
}
