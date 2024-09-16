using System.Collections.ObjectModel;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TrackerObservableCollection<T>
        : ObservableCollection<T>
    {
        private readonly HashSet<T> m_HashSet;

        public TrackerObservableCollection()
            : this(EqualityComparer<T>.Default)
        { }

        public TrackerObservableCollection(IEqualityComparer<T> equalityComparer)
        {
            m_HashSet = new HashSet<T>(equalityComparer);
        }

        protected override void InsertItem(int index, T item)
        {
            if (m_HashSet.Add(item))
            {
                base.InsertItem(index, item);
            }
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            m_HashSet.Clear();
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            m_HashSet.Remove(item);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, T item)
        {
            if (m_HashSet.Add(item))
            {
                var oldItem = this[index];
                m_HashSet.Remove(oldItem);
                base.SetItem(index, item);
            }
        }
    }
}
