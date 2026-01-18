namespace Zametek.ViewModel.ProjectPlan
{
    [Serializable]
    public class NodeActionModel
    {
        public HashSet<Guid> NodeIds { get; init; } = [];

        public NodeAction Action { get; private set; } = default;

        public void SetCut(IEnumerable<Guid> nodeIds)
        {
            Reset();
            foreach (Guid nodeId in nodeIds)
            {
                NodeIds.Add(nodeId);
            }
            Action = NodeAction.Cut;
        }

        public void SetCopy(IEnumerable<Guid> nodeIds)
        {
            Reset();
            foreach (Guid nodeId in nodeIds)
            {
                NodeIds.Add(nodeId);
            }
            Action = NodeAction.Copy;
        }

        public void Reset()
        {
            NodeIds.Clear();
            Action = default;
        }
    }
}
