using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CAIME
{
    /// <summary>
    /// Traces region borders and writes them as an SVG. One instance owns one export's state.
    /// </summary>
    public class SVGBordersExporter
    {
        private Dictionary<Tuple<int, int>, List<Tuple<int, ushort>>> SVGBorders;
        private List<Tuple<List<Tuple<int, ushort>>, bool>> FinalSVGBorders;

        public static bool Export(Project project)
        {
            return new SVGBordersExporter().Run(project);
        }

        private bool Run(Project project)
        {
            var dialog = new SaveFileDialog()
            {
                Filter = "Svg file (*.svg)|*.svg",
                Title = "Save borders as an svg file",
            };

            dialog.FileName = "borders.svg";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                LoggerViewModel.Log("Svg borders export was cancelled by user.", LogLevel.Info);
                return false;
            }

            if (string.IsNullOrEmpty(dialog.FileName))
            {
                LoggerViewModel.Log("Failed to export Svg borders due to invalid filename.", LogLevel.Error);
                return false;
            }

            var db = project.Database;
            var mapHexFile = project.MapHexFile;

            // IsBorder can be stale (e.g. the region layer changed since the last recompute) -
            // refresh it first so this always traces the borders that actually exist now,
            // rather than whatever happened to still be in memory. Mirrors BordersExporter.Export.
            BordersExporter.GenerateRegionEdges(mapHexFile);

            SVGBorders = new Dictionary<Tuple<int, int>, List<Tuple<int, ushort>>>();
            FindSVGBorderEdges(mapHexFile);
            FinalSVGBorders = new List<Tuple<List<Tuple<int, ushort>>, bool>>();
            OrderSVGBorderEdges(mapHexFile, db);
            Write(mapHexFile, dialog.FileName);

            LoggerViewModel.Log("Svg borders have been successfully exported.", LogLevel.Info);
            return true;
        }

        //Helper function
        public static bool AreHexEdgesAdjacent(MapHexFile mapHexFile, int hexIndex1, ushort dir1, int hexIndex2, ushort dir2)
        {
            if (hexIndex1 == hexIndex2)
            { //within same hex
                if (dir1 == dir2) //they are actually the same edge
                    return true;
                if ((dir1 + 1) % 6 == dir2)
                    return true;
                else if ((dir1 + 5) % 6 == dir2) 
                    return true;
                else
                    return false;
            }
            else if (mapHexFile.GetNeighbourIndex(hexIndex1, dir1) == hexIndex2)
            {  //direct neighbor
                if (dir1 == HexGridUtility.InverseDir(dir2)) //they are actually the same edge
                    return true;
                else if ((dir1 + 4) % 6 == dir2)
                    return true;
                else if ((dir1 + 2) % 6 == dir2)
                    return true;
                else
                    return false;
            }
            else if (mapHexFile.GetNeighbourIndex(hexIndex1, (ushort)((dir1+1) % 6)) == hexIndex2)
            {  //right neighbor
                if ((dir1 + 5) % 6 == dir2)
                    return true;
                else if ((dir1 + 4) % 6 == dir2)
                    return true;
                else
                    return false;
            }
            else if (mapHexFile.GetNeighbourIndex(hexIndex1, (ushort)((dir1+5) % 6)) == hexIndex2)
            {  //left neighbor
                if ((dir1 + 1) % 6 == dir2)
                    return true;
                else if ((dir1 + 2) % 6 == dir2)
                    return true;
                else
                    return false;
            }
            else //none of that
               return false;
        }

        public static bool IsRegionBorderMajor(MapHexFile mapHexFile, DatabaseViewModel db, int regIndex1, int regIndex2)
        {
            String region_name1 = mapHexFile.GetRegionName(regIndex1);
            var reg1 = db.CachedRegionsToProvinces.Find(record => record.Region == region_name1);
  
            String region_name2 = mapHexFile.GetRegionName(regIndex2);
            var reg2 = db.CachedRegionsToProvinces.Find(record => record.Region == region_name2);

            //Could probably just check if the regions are land/sea... but this deals with the db not being set up too
            if ((reg1 == null) && (reg2 == null)) //Both are sea regions
                return false;

            if ((reg1 == null) || (reg2 == null)) //Land region next to sea
                return true;

            return !(reg1.Province == reg2.Province);
        }

        // Every hex-edge can only ever be geometrically adjacent (share a vertex) to edges
        // belonging to itself, the hex across the edge, or the two hexes across its
        // neighbouring edges - a fixed, small set regardless of how big the border group is.
        // Enumerating just those candidates (instead of testing against every other edge in
        // the group) is what turns the O(n^2) stitching below into O(n). Public because
        // SVGRoadExporter's stitching reuses it rather than re-deriving the same geometry.
        public static IEnumerable<Tuple<int, ushort>> CandidateAdjacentEdges(MapHexFile mapHexFile, int hexIndex, ushort dir)
        {
            var hexes = new[]
            {
                hexIndex,
                mapHexFile.GetNeighbourIndex(hexIndex, dir),
                mapHexFile.GetNeighbourIndex(hexIndex, (ushort)((dir + 1) % 6)),
                mapHexFile.GetNeighbourIndex(hexIndex, (ushort)((dir + 5) % 6)),
            };

            foreach (var h in hexes)
            {
                if (h == -1)
                    continue;

                for (ushort d = 0; d < HexGridUtility.NEIGHBOURS_COUNT; d++)
                {
                    if (h == hexIndex && d == dir)
                        continue;

                    yield return new Tuple<int, ushort>(h, d);
                }
            }
        }

        //Orders the lists within the dictionary
        public void OrderSVGBorderEdges(MapHexFile mapHexFile, DatabaseViewModel db)
        {
            foreach (Tuple<int, int> key in SVGBorders.Keys)
            {
                List<Tuple<int, ushort>> group = SVGBorders[key];
                bool majorOrMinor = IsRegionBorderMajor(mapHexFile, db, key.Item1, key.Item2);

                // Which group members are actually adjacent to each edge (same AreHexEdgesAdjacent
                // definition as before, just checked against a handful of geometric candidates
                // instead of the whole group). Built once per group, reused as points get consumed.
                var remaining = new HashSet<Tuple<int, ushort>>(group);
                var adjacency = new Dictionary<Tuple<int, ushort>, List<Tuple<int, ushort>>>();
                foreach (var edge in group)
                {
                    var adjacent = new List<Tuple<int, ushort>>();
                    foreach (var candidate in CandidateAdjacentEdges(mapHexFile, edge.Item1, edge.Item2))
                    {
                        if (remaining.Contains(candidate) && AreHexEdgesAdjacent(mapHexFile, edge.Item1, edge.Item2, candidate.Item1, candidate.Item2))
                        {
                            adjacent.Add(candidate);
                        }
                    }
                    adjacency[edge] = adjacent;
                }

                //Do this whole process until every point has been consumed into some ordered list
                while (remaining.Count > 0)
                {
                    Tuple<int, ushort> endpoint = null;

                    //find an endpoint to begin, if there is one (always possible there's only loops)
                    foreach (var point in remaining)
                    {
                        var degree = adjacency[point].Count(a => remaining.Contains(a));

                        //this means we found an endpoint and can stop searching
                        if (degree <= 1)
                        {
                            endpoint = point;
                            break;
                        }
                    }

                    //We didn't find an endpoint and so everything is loops, so we can start anywhere
                    if (endpoint == null)
                    {
                        endpoint = remaining.First();
                    }

                    //Remove points from the remaining set and put them in a new list, based on adjacency with the latest point in the ordered list
                    List<Tuple<int, ushort>> ordered_list = new List<Tuple<int, ushort>>();

                    remaining.Remove(endpoint);
                    ordered_list.Add(endpoint);

                    while (true)
                    {
                        var last = ordered_list.Last();

                        Tuple<int, ushort> next = null;
                        foreach (var candidate in adjacency[last])
                        {
                            if (remaining.Contains(candidate))
                            {
                                next = candidate;
                                break;
                            }
                        }

                        if (next == null)
                            break;

                        remaining.Remove(next);
                        ordered_list.Add(next);
                    }

                    FinalSVGBorders.Add(new Tuple<List<Tuple<int, ushort>>, bool>(ordered_list, majorOrMinor));
                }
            }
        }

        //Puts together the dictionary
        public void FindSVGBorderEdges(MapHexFile mapHexFile)
        {
            // SVGBorders keeps the edges in discovery order, so the dedup runs off a parallel set
            // rather than a linear scan of the growing list.
            var seenEdges = new Dictionary<Tuple<int, int>, HashSet<Tuple<int, ushort>>>();

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; hexIndex++)
            {
                Hex hex = mapHexFile.HexData[hexIndex];
                if (!hex.IsBorder)
                    continue;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
                {
                    Hex nbr = mapHexFile.GetNeighbour(hex, dir);
                    if (nbr == null)
                        continue;

                    if (hex.RegionId == nbr.RegionId)
                        continue;

                    else if (hex.RegionId > nbr.RegionId)
                    {
                        var reg_tup = new Tuple<int, int>(nbr.RegionId, hex.RegionId);
                        var edge_tup = new Tuple<int, ushort>(nbr.Index, HexGridUtility.InverseDir(dir));

                        AddSVGBorderEdge(seenEdges, reg_tup, edge_tup);
                    }
                    else // else if (hex.RegionId < nbr.RegionId)
                    {
                        var reg_tup = new Tuple<int, int>(hex.RegionId, nbr.RegionId);
                        var edge_tup = new Tuple<int, ushort>(hex.Index, dir);

                        AddSVGBorderEdge(seenEdges, reg_tup, edge_tup);
                    }
                }
            }
        }
        
        private void AddSVGBorderEdge(
            Dictionary<Tuple<int, int>, HashSet<Tuple<int, ushort>>> seenEdges,
            Tuple<int, int> regionPair,
            Tuple<int, ushort> edge)
        {
            if (!seenEdges.TryGetValue(regionPair, out var seen))
            {
                seen = new HashSet<Tuple<int, ushort>>();
                seenEdges.Add(regionPair, seen);
                SVGBorders.Add(regionPair, new List<Tuple<int, ushort>>());
            }

            if (seen.Add(edge))
                SVGBorders[regionPair].Add(edge);
        }

        public static (float x, float y) GetHexCenter(int col, int row)
        {
            float HexWidth = 1.0f;
            float HexHeight = 1.0f;

            float centerX = col * HexWidth;
            float centerY = row * HexHeight;

            if (col % 2 == 1)
            {
                centerY += HexHeight / 2;
            }

            return (centerX, centerY);
        }

        public static (float x, float y) GetEdgeCenter(int col, int row, ushort dir)
        {
            float HexWidth = 1.0f;
            float HexHeight = 1.0f;

            (float centerX, float centerY) = GetHexCenter(col, row);

            // Calculate the coordinates of the center of the edge based on the direction
            switch (dir)
            {
                case 0:
                    return (centerX, centerY + HexHeight / 2);
                case 1:
                    return (centerX + HexWidth / 2, centerY + HexHeight / 4);
                case 2:
                    return (centerX + HexWidth / 2, centerY - HexHeight / 4);
                case 3:
                    return (centerX, centerY - HexHeight / 2);
                case 4:
                    return (centerX - HexWidth / 2, centerY - HexHeight / 4);
                case 5:
                    return (centerX - HexWidth / 2, centerY + HexHeight / 4);
                default:
                    throw new ArgumentException("Invalid direction");
            }
        }

        private void Write(MapHexFile mapHexFile, string path)
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

                foreach (var tup in FinalSVGBorders)
                {
                    List<Tuple<int, ushort>> point_list = tup.Item1;
                    bool isMajor = tup.Item2;

                    var first_x = 0.0f;
                    var first_y = 0.0f;
                    var last_x = 0.0f;
                    var last_y = 0.0f;

                    sw.Write("\t<path d = \"");
                    for (int i = 0; i < point_list.Count; i++)
                    {
                        Hex hex = mapHexFile.HexData[point_list[i].Item1];

                        (var x, var y) = GetEdgeCenter(hex.Q, hex.R, point_list[i].Item2);

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

                        if (i == point_list.Count - 1)
                        {
                            last_x = x;
                            last_y = y;
                        }

                        sw.Write((x + 0.5) * scale_width);
                        sw.Write(",");
                        sw.Write(svg_height - ((y + 0.5) * scale_height));
                        sw.Write(" ");
                    }

                    //If the two endpoints are close enough, just connect it up as a real loop
                    if (Math.Abs(last_x - first_x) <= 0.5 && Math.Abs(last_y - first_y) <= 0.5)
                    {
                        sw.Write("Z");
                    }

                    int stroke_width = isMajor ? 3 : 1;

                    sw.Write($"\" stroke=\"#291410\" stroke-width=\"{stroke_width}\" fill=\"none\" stroke-linejoin=\"round\" stroke-linecap=\"round\" />\n");
                }

                sw.WriteLine("</svg>");
            }
        }
    }
}
