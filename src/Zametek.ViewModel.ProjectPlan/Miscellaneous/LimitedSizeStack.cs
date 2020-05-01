using System;
using System.Collections.Generic;

namespace Zametek.ViewModel.ProjectPlan
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Stack behaviour")]
    public class LimitedSizeStack<T>
        : LinkedList<T>
        where T : class
    {
        private readonly int m_MaxSize;

        public LimitedSizeStack(int maxSize)
        {
            if (maxSize < 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Resources.Message_ValueCannotBeLessThanOne, nameof(maxSize));
            }
            m_MaxSize = maxSize;
        }

        public void Push(T item)
        {
            AddFirst(item);

            if (Count > m_MaxSize)
            {
                RemoveLast();
            }
        }

        public T Pop()
        {
            LinkedListNode<T> item = First;

            if (item != null)
            {
                RemoveFirst();
            }

            return item?.Value;
        }
    }
}
