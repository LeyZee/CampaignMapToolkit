using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CAIME
{
    /// <summary>
    /// Debug/visual aid, not consumed by the game: traces the road network into a standalone
    /// .svg file. A wired road connection links two hex *centers* (unlike a region border,
    /// which runs along a hex *edge*), so this walks the road graph hex-to-hex - closer in
    /// spirit to BordersExporter's (.pbd) hex-center walk than to SVGBordersExporter's
    /// edge-midpoint stitching.
    /// </summary>
    public class SVGRoadExporter
    {
        public static List<Tuple<int, int>> SVGRoads;
        public static List<List<int>> FinalSVGRoads;

        public static bool Export(Project project)
        {
            var dialog = new SaveFileDialog()
            {
                Filter = "Svg file (*.svg)|*.svg",
                Title = "Save roads as an svg file",
            };

            dialog.FileName = "roads.svg";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                LoggerViewModel.Log("Svg roads export was cancelled by user.", LogLevel.Info);
                return false;
            }

            if (string.IsNullOrEmpty(dialog.FileName))
            {
                LoggerViewModel.Log("Failed to export Svg roads due to invalid filename.", LogLevel.Error);
                return false;
            }

            var mapHexFile = project.MapHexFile;

            SVGRoads = new List<Tuple<int, int>>();
            FindSVGRoadEdges(mapHexFile);
            FinalSVGRoads = new List<List<int>>();
            OrderSVGRoadEdges();
            Write(mapHexFile, dialog.FileName);

            LoggerViewModel.Log("Svg roads have been successfully exported.", LogLevel.Info);
            return true;
        }

        //Puts together the flat list of wired hex-to-hex road connections (one entry per
        //physical connection, not two - a wired edge sets reciprocal mask bits on both hexes).
        public static void FindSVGRoadEdges(MapHexFile mapHexFile)
        {
            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; hexIndex++)
            {
                Hex hex = mapHexFile.HexData[hexIndex];
                if (!hex.IsRoad)
                    continue;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
                {
                    if ((hex.RoadEdgeMask & (1 << dir)) == 0)
                        continue;

                    int nbrIndex = mapHexFile.GetNeighbourIndex(hexIndex, dir);
                    if (nbrIndex == -1)
                        continue;

                    if (hexIndex < nbrIndex)
                    {
                        SVGRoads.Add(new Tuple<int, int>(hexIndex, nbrIndex));
                    }
                }
            }
        }

        //Decomposes the road graph (nodes = road hexes, edges = wired connections) into
        //ordered hex-index chains, each edge used exactly once. Starts from a dead-end/gateway
        //(degree <= 1 among unused edges) where one exists so a simple through-road traces as
        //a single chain rather than being split arbitrarily; junctions just end up shared
        //between two or more chains that meet at the same hex, which renders identically.
        public static void OrderSVGRoadEdges()
        {
            var adjacency = new Dictionary<int, List<int>>();
            void AddAdjacency(int a, int b)
            {
                if (!adjacency.TryGetValue(a, out var list))
                {
                    list = new List<int>();
                    adjacency[a] = list;
                }
                list.Add(b);
            }

            foreach (var edge in SVGRoads)
            {
                AddAdjacency(edge.Item1, edge.Item2);
                AddAdjacency(edge.Item2, edge.Item1);
            }

            // Tracks how many still-unused parallel connections remain between two hexes
            // (normally at most one, but this stays correct even if that were ever untrue).
            var remaining = new Dictionary<Tuple<int, int>, int>();
            foreach (var edge in SVGRoads)
            {
                var key = new Tuple<int, int>(edge.Item1, edge.Item2);
                remaining[key] = remaining.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            Tuple<int, int> EdgeKey(int a, int b) => a < b ? new Tuple<int, int>(a, b) : new Tuple<int, int>(b, a);

            int UnusedDegree(int hexIndex) => adjacency[hexIndex].Count(n => remaining[EdgeKey(hexIndex, n)] > 0);

            var nodesWithEdges = new HashSet<int>(adjacency.Keys.Where(n => UnusedDegree(n) > 0));

            while (nodesWithEdges.Count > 0)
            {
                // -1 as "not found" sentinel, since 0 is a legitimate hex index and FirstOrDefault
                // can't distinguish "found index 0" from "found nothing" for an int sequence.
                int start = -1;
                foreach (var n in nodesWithEdges)
                {
                    if (UnusedDegree(n) <= 1)
                    {
                        start = n;
                        break;
                    }
                }
                if (start == -1)
                {
                    start = nodesWithEdges.First();
                }

                var chain = new List<int> { start };
                var current = start;

                while (true)
                {
                    int next = -1;
                    foreach (var n in adjacency[current])
                    {
                        if (remaining[EdgeKey(current, n)] > 0)
                        {
                            next = n;
                            break;
                        }
                    }
                    if (next == -1)
                        break;

                    remaining[EdgeKey(current, next)]--;
                    chain.Add(next);
                    current = next;
                }

                foreach (var hexIndex in chain)
                {
                    if (UnusedDegree(hexIndex) == 0)
                        nodesWithEdges.Remove(hexIndex);
                }

                FinalSVGRoads.Add(chain);
            }
        }

        public static void Write(MapHexFile mapHexFile, string path)
        {
            File.Delete(path);

            using (StreamWriter sw = new StreamWriter(File.OpenWrite(path)))
            {
                var scale_width = 4.0;
                var scale_height = 8 / Math.Sqrt(3);
                var svg_width = Math.Floor(mapHexFile.MapWidth * scale_width);
                var svg_height = Math.Floor(mapHexFile.MapHeight * scale_height);

                sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");
                sw.WriteLine("<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\" >");
                sw.WriteLine($"<svg width = \"{svg_width}\" height = \"{svg_height}\" viewBox = \"0 0 {svg_width} {svg_height}\" >");

                foreach (var chain in FinalSVGRoads)
                {
                    var first_x = 0.0f;
                    var first_y = 0.0f;
                    var last_x = 0.0f;
                    var last_y = 0.0f;

                    sw.Write("\t<path d = \"");
                    for (int i = 0; i < chain.Count; i++)
                    {
                        Hex hex = mapHexFile.HexData[chain[i]];

                        (var x, var y) = SVGBordersExporter.GetHexCenter(hex.Q, hex.R);

                        if (i == 0)
                        {
                            sw.Write("M");
                            first_x = x;
                            first_y = y;
                        }
                        else
                        {
                            sw.Write("L");
                        }

                        if (i == chain.Count - 1)
                        {
                            last_x = x;
                            last_y = y;
                        }

                        sw.Write((x + 0.5) * scale_width);
                        sw.Write(",");
                        sw.Write(svg_height - ((y + 0.5) * scale_height));
                        sw.Write(" ");
                    }

                    //If the two endpoints are the same hex, it's a closed loop with no junctions
                    if (chain.Count > 2 && Math.Abs(last_x - first_x) <= 0.001 && Math.Abs(last_y - first_y) <= 0.001)
                    {
                        sw.Write("Z");
                    }

                    sw.Write("\" stroke=\"black\" stroke-width=\"2\" fill=\"none\" stroke-linejoin=\"round\" stroke-linecap=\"round\" />\n");
                }

                sw.WriteLine("</svg>");
            }
        }
    }
}
