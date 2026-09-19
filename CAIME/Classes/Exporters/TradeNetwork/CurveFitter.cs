using System;
using System.Collections.Generic;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// Turns one piece of a route into the curves that draw it, splitting a span in half wherever a
    /// single curve cannot stay close enough to the hexes it is meant to follow.
    /// </summary>
    internal sealed class CurveFitter
    {
        private const float DeviationThreshold = 100.0f;

        private readonly TradeGrid   _grid;
        private readonly List<Curve> _curves = new List<Curve>();

        public CurveFitter(TradeGrid grid)
        {
            _grid = grid;
        }

        public Curve[] Fit(int[] path, RoutePiece piece)
        {
            _curves.Clear();

            if (piece.IsCorner)
            {
                FitCorner(path, piece);
            }
            else if (piece.StartIndex == piece.EndIndex)
            {
                FitSingleHexSpan(path[piece.StartIndex]);
            }
            else
            {
                FitSpan(path, piece.StartIndex, piece.EndIndex);
            }

            return _curves.ToArray();
        }

        private void FitCorner(int[] path, RoutePiece piece)
        {
            int middle = piece.StartIndex + 1;

            var start = HexGeometry.EdgeFacing(_grid, path[middle], path[piece.StartIndex], HexGeometry.CornerControlLength);
            var end   = HexGeometry.EdgeFacing(_grid, path[middle], path[piece.EndIndex], HexGeometry.CornerControlLength);

            _curves.Add(Build(start, end));
        }

        /// <summary>
        /// A span of one hex is drawn straight through the hex, from one of its trade edges to the
        /// other, with both control points parked on the midpoint between them.
        /// </summary>
        private void FitSingleHexSpan(int hex)
        {
            var  entry = default(HexPoint);
            var  exit  = default(HexPoint);
            int  found = 0;

            for (int direction = 0; direction < TradeGrid.DirectionCount && found < 2; ++direction)
            {
                int neighbour = _grid.Neighbour(hex, direction);
                if (neighbour < 0 || !_grid.TradeEdgesMeet(hex, neighbour))
                    continue;

                if (found == 0)
                {
                    entry = HexGeometry.EdgePoint(_grid, hex, direction);
                }
                else
                {
                    exit = HexGeometry.EdgePoint(_grid, hex, direction);
                }

                ++found;
            }

            if (found < 2)
            {
                throw new InvalidOperationException(
                    $"Hex {hex} carries a single-hex trade route span but has fewer than two trade edges.");
            }

            short controlX = unchecked((short)((exit.X - entry.X) * 50));
            short controlY = unchecked((short)((exit.Y - entry.Y) * 50));

            _curves.Add(new Curve((ushort)entry.X, (ushort)entry.Y, controlX, controlY,
                                  unchecked((short)-controlX), unchecked((short)-controlY),
                                  (ushort)exit.X, (ushort)exit.Y));
        }

        private void FitSpan(int[] path, int first, int last)
        {
            var start = HexGeometry.OutboundEdge(_grid, path[first], path[first + 1], HexGeometry.DefaultControlLength);
            var end   = HexGeometry.OutboundEdge(_grid, path[last], path[last - 1], HexGeometry.DefaultControlLength);

            FitRange(path, first, last, start, end);
        }

        /// <summary>
        /// Section 7.4. Solves for the control lengths that pull the curve's midpoint onto the
        /// middle of the range, then either keeps the result or splits the range at the hex the
        /// curve strays furthest from.
        /// </summary>
        private void FitRange(int[] path, int first, int last, CurveEndpoint start, CurveEndpoint end)
        {
            int count = last - first + 1;

            if (count <= 2)
            {
                _curves.Add(Build(start, end));
                return;
            }

            var  target = TargetMidpoint(path, first, count);
            var  trial  = Build(start, end);

            float midpointX = (trial.StartX + 3.0f * trial.FirstControlX + 3.0f * trial.SecondControlX + trial.EndX) / 8.0f;
            float midpointY = (trial.StartY + 3.0f * trial.FirstControlY + 3.0f * trial.SecondControlY + trial.EndY) / 8.0f;

            float startDirectionX = trial.StartControlX / 100.0f;
            float startDirectionY = trial.StartControlY / 100.0f;
            float endDirectionX   = trial.EndControlX / 100.0f;
            float endDirectionY   = trial.EndControlY / 100.0f;

            float differenceX = target.X - midpointX;
            float differenceY = target.Y - midpointY;

            float numerator   = startDirectionY * differenceX - startDirectionX * differenceY;
            float denominator = startDirectionY * endDirectionX - startDirectionX * endDirectionY;

            if (denominator != 0.0f)
            {
                float alongEnd = numerator / denominator;
                float alongStart;

                if (startDirectionX != 0.0f)
                {
                    alongStart = (differenceX - endDirectionX * alongEnd) / startDirectionX;
                }
                else if (startDirectionY != 0.0f)
                {
                    alongStart = (differenceY - endDirectionY * alongEnd) / startDirectionY;
                }
                else
                {
                    // Neither component can carry the solution, so the trial curve stands as it is.
                    FinalCheck(path, first, last, trial, start, end);
                    return;
                }

                if (alongStart < 0.0f)
                    alongStart = 0.0f;

                if (alongEnd < 0.0f)
                    alongEnd = 0.0f;

                alongStart *= 10.0f;
                alongEnd   *= 10.0f;

                float chord = Chord(trial);

                start.ControlLength = ClampToChord(alongStart * 8.0f / 3.0f + 10.5f, chord, start.Length);
                end.ControlLength   = ClampToChord(alongEnd * 8.0f / 3.0f + 10.5f, chord, end.Length);

                var fitted = Build(start, end);

                int   worstHex;
                float deviation = WorstDeviation(path, first, last, fitted, out worstHex);

                // Accepting on "close enough" here and splitting on "not close enough" in the final
                // check leaves an exact deviation of the threshold accepted in one and split in the
                // other. Both comparisons are required as they stand.
                if (deviation <= DeviationThreshold)
                {
                    _curves.Add(fitted);
                }
                else
                {
                    Split(path, first, last, worstHex, start, end);
                }

                return;
            }

            float straightChord = Chord(trial);

            start.ControlLength = AtLeastMinimum(straightChord * 30.0f / start.Length);
            end.ControlLength   = AtLeastMinimum(straightChord * 30.0f / end.Length);

            FinalCheck(path, first, last, Build(start, end), start, end);
        }

        private void FinalCheck(int[] path, int first, int last, Curve trial, CurveEndpoint start, CurveEndpoint end)
        {
            int   worstHex;
            float deviation = WorstDeviation(path, first, last, trial, out worstHex);

            if (deviation >= DeviationThreshold)
            {
                Split(path, first, last, worstHex, start, end);
            }
            else
            {
                _curves.Add(trial);
            }
        }

        /// <summary>
        /// Section 7.6. Both halves meet at the worst hex, sharing one tangent so the join stays
        /// smooth, and both are fitted from scratch with default control lengths.
        /// </summary>
        private void Split(int[] path, int first, int last, int worstHex, CurveEndpoint start, CurveEndpoint end)
        {
            start.ControlLength = HexGeometry.DefaultControlLength;
            end.ControlLength   = HexGeometry.DefaultControlLength;

            int middle = first;
            while (path[middle] != worstHex)
            {
                ++middle;
            }

            var back    = HexGeometry.EdgeFacing(_grid, path[middle], path[middle - 1], HexGeometry.DefaultControlLength);
            var forward = HexGeometry.EdgeFacing(_grid, path[middle], path[middle + 1], HexGeometry.DefaultControlLength);

            int tangentX = ((forward.ControlX - back.ControlX) + 10) / 20;
            int tangentY = ((forward.ControlY - back.ControlY) + 10) / 20;

            var centre = _grid.Centre(path[middle]);

            var join            = new CurveEndpoint();
            join.X              = centre.X;
            join.Y              = centre.Y;
            join.DirectionX     = tangentX;
            join.DirectionY     = tangentY;
            join.ControlLength  = HexGeometry.DefaultControlLength;

            var resume           = new CurveEndpoint();
            resume.X             = centre.X;
            resume.Y             = centre.Y;
            resume.DirectionX    = -tangentX;
            resume.DirectionY    = -tangentY;
            resume.ControlLength = HexGeometry.DefaultControlLength;

            FitRange(path, first, middle, start, join);
            FitRange(path, middle, last, resume, end);
        }

        private HexPoint TargetMidpoint(int[] path, int first, int count)
        {
            if ((count & 1) != 0)
                return _grid.Centre(path[first + count / 2]);

            var before = _grid.Centre(path[first + count / 2 - 1]);
            var after  = _grid.Centre(path[first + count / 2]);

            return new HexPoint((before.X + after.X) / 2, (before.Y + after.Y) / 2);
        }

        /// <summary>
        /// Section 7.5. Walks the range's interior hexes and keeps the one furthest from the curve,
        /// replacing the incumbent only on a strictly greater distance.
        /// </summary>
        private float WorstDeviation(int[] path, int first, int last, Curve curve, out int worstHex)
        {
            worstHex = path[first + 1];
            float worst = 0.0f;

            for (int i = first + 1; i < last; ++i)
            {
                float deviation = Deviation(path[i], curve);
                if (deviation > worst)
                {
                    worst    = deviation;
                    worstHex = path[i];
                }
            }

            return worst;
        }

        private float Deviation(int hex, Curve curve)
        {
            var point = _grid.Centre(hex);

            int chordX = curve.EndX - curve.StartX;
            int chordY = curve.EndY - curve.StartY;

            // The dot products are 32-bit integers that wrap rather than saturate, and the parameter
            // is deliberately not clamped to the curve's own span.
            int numerator   = unchecked(point.X * chordX + point.Y * chordY - (curve.StartX * chordX + curve.StartY * chordY));
            int denominator = unchecked(chordX * chordX + chordY * chordY);

            if (denominator == 0)
            {
                throw new InvalidOperationException("A trade-route curve has coincident anchors and cannot be measured.");
            }

            float t = (float)numerator / denominator;

            float curveX, curveY;
            curve.Evaluate(t, out curveX, out curveY);

            float offsetX = point.X - curveX;
            float offsetY = point.Y - curveY;

            return offsetX * offsetX + offsetY * offsetY;
        }

        private static Curve Build(CurveEndpoint start, CurveEndpoint end)
        {
            return new Curve((ushort)start.X, (ushort)start.Y, start.ControlX, start.ControlY,
                             end.ControlX, end.ControlY, (ushort)end.X, (ushort)end.Y);
        }

        private static float Chord(Curve curve)
        {
            int dx = curve.EndX - curve.StartX;
            int dy = curve.EndY - curve.StartY;

            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static uint ClampToChord(float value, float chord, float directionLength)
        {
            float lowest  = chord * 20.0f / directionLength;
            float highest = chord * 40.0f / directionLength;

            if (value < lowest)
                value = lowest;

            if (value > highest)
                value = highest;

            return (uint)value;
        }

        private static uint AtLeastMinimum(float value)
        {
            return (uint)(value < 10.5f ? 10.5f : value);
        }
    }
}
