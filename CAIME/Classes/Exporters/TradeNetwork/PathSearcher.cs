using System;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// Traces paths through the painted trade network, level by level, offering every partial path
    /// to a set of candidates as it goes. One instance holds all the scratch state a search needs
    /// and is reused across sources; instances are not shared between threads.
    /// </summary>
    internal sealed class PathSearcher
    {
        private readonly TradeGrid _grid;

        private readonly bool[]    _visited;
        private int[]              _visitedLog = new int[256];
        private int                _visitedCount;

        // Partial paths are held as a tree: a node is one hex plus the node it was reached from.
        // Extending appends a child, forking appends a sibling, and neither copies the prefix.
        private int[]              _nodeHex    = new int[1024];
        private int[]              _nodeParent = new int[1024];
        private int[]              _nodeDepth  = new int[1024];
        private int                _nodeCount;

        private int[]              _frontier   = new int[256];
        private int                _frontierCount;

        public PathSearcher(TradeGrid grid)
        {
            _grid    = grid;
            _visited = new bool[grid.Count];
        }

        /// <summary>
        /// Searches from every endpoint of the source region that lies in <paramref name="domain"/>.
        /// The candidates are shared across the searches, so a later endpoint can improve on an
        /// earlier one's result.
        /// </summary>
        public void Resolve(RouteDomain domain, int[] sourceEndpoints, RouteCandidate[] candidates)
        {
            for (int i = 0; i < sourceEndpoints.Length; ++i)
            {
                int endpoint = sourceEndpoints[i];
                if (domain.Contains(_grid, endpoint))
                {
                    Search(domain, endpoint, candidates);
                }
            }
        }

        private void Search(RouteDomain domain, int startEndpoint, RouteCandidate[] candidates)
        {
            ClearVisited();
            _nodeCount     = 0;
            _frontierCount = 0;

            AppendToFrontier(AddNode(startEndpoint, -1, 1));

            while (_frontierCount > 0)
            {
                // Paths appended while this level runs belong to the next one.
                int levelCount = _frontierCount;
                int position   = 0;

                while (position < levelCount)
                {
                    int node = _frontier[position];
                    int tail = _nodeHex[node];

                    MarkVisited(tail);

                    bool drop = domain.Terminates(_grid, tail)
                             || !Offer(node, tail, candidates)
                             || !ExtendAndFork(position, node, tail);

                    if (drop)
                    {
                        RemoveFromFrontier(position);
                        --levelCount;
                    }
                    else
                    {
                        ++position;
                    }
                }
            }
        }

        /// <summary>
        /// Section 6.4. The walk stops at the first candidate that adopts the path, which is why a
        /// path can only ever be adopted once. Returns false when no candidate can be improved by
        /// this path or any longer one grown from it.
        /// </summary>
        private bool Offer(int node, int tail, RouteCandidate[] candidates)
        {
            int  length     = _nodeDepth[node];
            bool canImprove = false;

            for (int i = 0; i < candidates.Length; ++i)
            {
                var candidate = candidates[i];

                if (candidate.Path != null && candidate.Path.Length <= length)
                    continue;

                if (IsDestinationEndpoint(candidate, tail))
                {
                    candidate.Path = MaterialisePath(node, length);
                    return false;
                }

                canImprove = true;
            }

            return canImprove;
        }

        private static bool IsDestinationEndpoint(RouteCandidate candidate, int hex)
        {
            var endpoints = candidate.DestinationEndpoints;

            for (int i = 0; i < endpoints.Length; ++i)
            {
                if (endpoints[i] == hex)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Section 6.5. Extends the path along the first unvisited trade edge, then forks a sibling
        /// for every edge still left in the mask. Returns false when the path is exhausted and must
        /// leave the frontier.
        /// </summary>
        private bool ExtendAndFork(int position, int node, int tail)
        {
            int mask = _grid.TradeMask[tail];

            // A hex reached along a mask bit always has a mask of its own on a valid map. Dropping
            // the path instead of looping forever is the only sane answer if one ever does not.
            if (mask == 0)
                return false;

            int depth    = _nodeDepth[node] + 1;
            int extended = -1;

            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                int bit = 1 << direction;
                if ((mask & bit) == 0)
                    continue;

                int neighbour = _grid.Neighbour(tail, direction);
                mask &= ~bit;

                if (!_visited[neighbour])
                {
                    extended           = AddNode(neighbour, node, depth);
                    _frontier[position] = extended;
                    break;
                }

                if (mask == 0)
                    return false;
            }

            while (mask != 0)
            {
                int direction = BitScanForward(mask);
                mask &= mask - 1;

                int neighbour = _grid.Neighbour(tail, direction);
                if (!IsOnPath(extended, neighbour))
                {
                    AppendToFrontier(AddNode(neighbour, node, depth));
                }
            }

            return true;
        }

        private static int BitScanForward(int mask)
        {
            int direction = 0;
            while ((mask & 1) == 0)
            {
                mask >>= 1;
                ++direction;
            }

            return direction;
        }

        private bool IsOnPath(int node, int hex)
        {
            for (int walk = node; walk >= 0; walk = _nodeParent[walk])
            {
                if (_nodeHex[walk] == hex)
                    return true;
            }

            return false;
        }

        private int[] MaterialisePath(int node, int length)
        {
            var path = new int[length];

            for (int walk = node, slot = length - 1; walk >= 0; walk = _nodeParent[walk], --slot)
            {
                path[slot] = _nodeHex[walk];
            }

            return path;
        }

        private int AddNode(int hex, int parent, int depth)
        {
            if (_nodeCount == _nodeHex.Length)
            {
                int grown = _nodeHex.Length * 2;
                Array.Resize(ref _nodeHex, grown);
                Array.Resize(ref _nodeParent, grown);
                Array.Resize(ref _nodeDepth, grown);
            }

            _nodeHex[_nodeCount]    = hex;
            _nodeParent[_nodeCount] = parent;
            _nodeDepth[_nodeCount]  = depth;

            return _nodeCount++;
        }

        private void AppendToFrontier(int node)
        {
            if (_frontierCount == _frontier.Length)
            {
                Array.Resize(ref _frontier, _frontier.Length * 2);
            }

            _frontier[_frontierCount++] = node;
        }

        private void RemoveFromFrontier(int position)
        {
            Array.Copy(_frontier, position + 1, _frontier, position, _frontierCount - position - 1);
            --_frontierCount;
        }

        private void MarkVisited(int hex)
        {
            if (_visited[hex])
                return;

            _visited[hex] = true;

            if (_visitedCount == _visitedLog.Length)
            {
                Array.Resize(ref _visitedLog, _visitedLog.Length * 2);
            }

            _visitedLog[_visitedCount++] = hex;
        }

        private void ClearVisited()
        {
            for (int i = 0; i < _visitedCount; ++i)
            {
                _visited[_visitedLog[i]] = false;
            }

            _visitedCount = 0;
        }
    }
}
