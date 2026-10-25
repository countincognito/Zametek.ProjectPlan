using System.Collections.ObjectModel;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ObservableUniqueCollection<T>
        : ObservableCollection<T>
    {
        private readonly HashSet<T> m_HashSet;

        public ObservableUniqueCollection()
            : this(EqualityComparer<T>.Default)
        { }

        public ObservableUniqueCollection(IEqualityComparer<T> equalityComparer)
        {
            m_HashSet = new HashSet<T>(equalityComparer);
        }

        public void Sort(IComparer<T> comparer)
        {
            List<T> sorted = [.. this];
            sorted.Sort(comparer);

            for (int i = 0; i < sorted.Count; i++)
            {
                Move(IndexOf(sorted[i]), i);
            }
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
