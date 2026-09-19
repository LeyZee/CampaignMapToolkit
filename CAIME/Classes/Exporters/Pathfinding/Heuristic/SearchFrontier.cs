using System;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// The cheapest-first queue a search pulls from, ordered by the whole label an entry carried when it
    /// was added. Entries with byte-identical priorities do occur, so the order they come back in is part
    /// of the result: this is a binary min-heap, sifting a removed root's replacement into the smaller
    /// child and preferring the left child when the two are equal.
    /// </summary>
    internal sealed class SearchFrontier
    {
        private uint[] _priorities;
        private int[]  _hexes;
        private int    _count;

        public SearchFrontier(int initialCapacity)
        {
            _priorities = new uint[Math.Max(initialCapacity, 1)];
            _hexes      = new int[_priorities.Length];
        }

        public void Clear()
        {
            _count = 0;
        }

        public void Add(uint priority, int hexIndex)
        {
            if (_count == _priorities.Length)
                Grow();

            int child = _count++;

            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (_priorities[parent] <= priority)
                    break;

                _priorities[child] = _priorities[parent];
                _hexes[child]      = _hexes[parent];
                child              = parent;
            }

            _priorities[child] = priority;
            _hexes[child]      = hexIndex;
        }

        public bool TryTake(out int hexIndex)
        {
            if (_count == 0)
            {
                hexIndex = -1;
                return false;
            }

            hexIndex = _hexes[0];

            uint lastPriority = _priorities[--_count];
            int  lastHexIndex = _hexes[_count];

            if (_count > 0)
                SiftDown(lastPriority, lastHexIndex);

            return true;
        }

        private void SiftDown(uint priority, int hexIndex)
        {
            int parent = 0;

            while (true)
            {
                int left = parent * 2 + 1;
                if (left >= _count)
                    break;

                int right = left + 1;
                int child = right < _count && _priorities[right] < _priorities[left] ? right : left;

                if (_priorities[child] >= priority)
                    break;

                _priorities[parent] = _priorities[child];
                _hexes[parent]      = _hexes[child];
                parent              = child;
            }

            _priorities[parent] = priority;
            _hexes[parent]      = hexIndex;
        }

        private void Grow()
        {
            Array.Resize(ref _priorities, _priorities.Length * 2);
            Array.Resize(ref _hexes, _hexes.Length * 2);
        }
    }
}
