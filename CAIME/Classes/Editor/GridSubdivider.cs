using System;
using SharpDX;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Geometry;

namespace CAIME
{
    /// <summary>
    /// Helper class that splits the grid on sections (for optimisation purposes).
    /// </summary>
    public class GridSubdivider
    {
        /// <summary>
        /// Number of grid subdivisions by X.
        /// </summary>
        private int numGridSubdivisionsX;
        /// <summary>
        /// Number of grid subdivisions by Y.
        /// </summary>
        private int numGridSubdivisionsY;

        public GridSubdivider()
        {}

        /// <summary>
        /// Subdivides hex grid on sections.
        /// </summary>
        /// <param name="hexGrid">Grid to subdivide</param>
        /// <param name="gridSections">Returned grid sections</param>
        public void SplitGrid(int width, int height, HexLayout hexLayout, out MeshGeometryModel3D[] gridSections, out MeshBuilder[] meshBuilders)
        {
            int bestSubdivX = 1;
            int bestSubdivY = 1;

            // Find the highest number of X and Y subdivisions without remainder (from 1 to 10 subdsivisions in each direction).
            for (int i = 1; i <= 10; ++i)
            {
                if (width % i == 0)
                {
                    bestSubdivX = i;
                }

                if (height % i == 0)
                {
                    bestSubdivY = i;
                }    
            }

            numGridSubdivisionsX = bestSubdivX;
            numGridSubdivisionsY = bestSubdivY;

            gridSections = new MeshGeometryModel3D[numGridSubdivisionsX * numGridSubdivisionsY];
            meshBuilders = new MeshBuilder[gridSections.Length];

            for (int index = 0; index < gridSections.Length; ++index)
            {
                meshBuilders[index] = new MeshBuilder(generateNormals: false, generateTexCoords: false);
            }

            for (int hexIndex = 0; hexIndex < width * height; ++hexIndex)
            {
                HexGridUtility.CoordsFromIndex(hexIndex, width, out int row, out int col);

                int subdivIndex = FindSectionIndex(hexIndex, width, height);
                var polygon     = hexLayout.GetPolygonCorners(col, row);

                meshBuilders[subdivIndex].AddPolygon(polygon);
            }
        }

        /// <summary>
        /// Get grid subdivision index from cell index
        /// </summary>
        /// <param name="hexIndex">Cell index</param>
        /// <param name="gridWidth">Grid width</param>
        /// <param name="gridHeight">Grid height</param>
        /// <returns>Integer that represent grid subdivision index aka grid chunk index in <see cref="gridSections"/> list</returns>
        public int FindSectionIndex(int hexIndex, int width, int height)
        {
            /// The purpose of this function is to find a grid section index in which the specified cell index is located
            /// Step 1. Find cell X and Y coords:
            ///     cell_x_coord = cell_index / grid_width
            ///     cell_y_coord = cell_index & grid_width
            ///     
            /// Step 2. Calculate section width and height:
            ///     section_width  = grid_width  / grid_sections_count
            ///     section_height = grid_height / grid_sections_count
            ///     
            /// Step 3. Calculate section X and Y coords:
            ///     section_x_coord = cell_x_coord / section_height
            ///     section_y_coord = cell_y_coord / section_width
            ///     
            /// Step 4. Calculate section index:
            ///     section_index = section_x_coord * grid_sections_count + section_y_coord

            int sw = width  / numGridSubdivisionsX;                                             // Section width
            int sh = height / numGridSubdivisionsY;                                             // Section height
            
            HexGridUtility.CoordsFromIndex(hexIndex, width, out int x, out int y);              // Cell X and Y coords

            int sx = x / sh;                                                                    // Section X coord
            int sy = y / sw;                                                                    // Section Y coord
            int sectionIdx = HexGridUtility.IndexFromCoords(sx, sy, numGridSubdivisionsX);      // Section index

            return sectionIdx;
        }

        /// <summary>
        /// Get cell index local to it's grid subdivision
        /// </summary>
        /// <param name="cellIndex">Global grid cell index</param>
        /// <param name="subdivIndex">Grid subdivision index</param>
        /// <param name="gridWidth">Full grid width</param>
        /// <param name="gridHeight">Full grid height</param>
        /// <returns>Integer that represent local cell index (local to grid subdivision)</returns>
        public int GlobalIndexToSectionIndex(int cellIndex, int subdivIndex, int gridWidth, int gridHeight)
        {
            /// The purpose of this function is to return a local cell index
            /// Local cell index is a cell index in a grid section coordinate system
            /// Global cell index is a cell index in an unsectioned grid coordinate system
            /// Grid cells are enumerated from bottom left to top right as an 1d array <see cref="MapHexFile.HexData"/>
            /// Below is the formula to convert a global cell index to a local cell index:
            /// 
            /// local_cell_index    = local_cell_x_coord * grid_section_width + local_cell_y_coord
            /// local_cell_x_coord  = base_cell_index / full_grid_width
            /// local_cell_y_coord  = base_cell_index % full_grid_width
            /// base_cell_index     = global_cell_index - section_x_coord * grid_section_height * full_grid_width + section_y_coord * grid_section_width
            /// section_x_coord     = grid_section_index / num_grid_sections
            /// section_y_coord     = grid_section_index % num_grid_sections
            /// grid_section_height = full_grid_height / num_grid_sections
            /// grid_section_width  = full_grid_width / num_grid_sections
            /// full_grid_width     = <see cref="gridWidth"/>
            /// full_grid_height    = <see cref="gridHeight"/>
            /// grid_section_index  = <see cref="subdivIndex"/>
            /// global_cell_index   = <see cref="cellIndex"/>
            /// num_grid_sections   = <see cref="numGridSubdivisions"/>
            /// 
            /// Example of a global coordinate system:
            /// 
            /// 40 41 42 43 | 44 45 46 47
            /// 32 33 34 35 | 36 37 38 39
            /// 24 25 26 27 | 28 29 30 31
            /// ------------|------------
            /// 16 17 18 19 | 20 21 22 23
            /// 08 09 10 11 | 12 13 14 15
            /// 00 01 02 03 | 04 05 06 07
            /// 
            /// This is a 8x6 grid subdivided on 4 sections
            /// Values in the example above are global cell indices
            /// Step 1 would be to convert a global cell index to a base cell index
            /// Base cell indices are effectively indices of a first section (with coordinates 0,0)
            /// 
            /// Global coordinate system to base coordinate system:
            /// 
            /// 16 17 18 19 | 16 17 18 19
            /// 08 09 10 11 | 08 09 10 11
            /// 00 01 02 03 | 00 01 02 03
            /// ------------|------------
            /// 16 17 18 19 | 16 17 18 19
            /// 08 09 10 11 | 08 09 10 11
            /// 00 01 02 03 | 00 01 02 03
            /// 
            /// Step 2 would be to calculate the local cell index from base cell index (see formulae above)
            /// 
            /// Base coordinate system to local coordinate system:
            /// 
            /// 08 09 10 11 | 08 09 10 11
            /// 04 05 06 07 | 04 05 06 07
            /// 00 01 02 03 | 00 01 02 03
            /// ------------|------------
            /// 08 09 10 11 | 08 09 10 11
            /// 04 05 06 07 | 04 05 06 07
            /// 00 01 02 03 | 00 01 02 03

            int sw = gridWidth  / numGridSubdivisionsX;                                                     // Section width
            int sh = gridHeight / numGridSubdivisionsY;                                                     // Section height

            HexGridUtility.CoordsFromIndex(subdivIndex, numGridSubdivisionsX, out int sx, out int sy);      // Section X and Y coords
            int baseIndex = cellIndex - (sx * sh * gridWidth + sy * sw);                                    // Step 1. Global index to base index
            HexGridUtility.CoordsFromIndex(baseIndex, gridWidth, out int x, out int y);                     // Base index X and Y coords
            int localIndex = HexGridUtility.IndexFromCoords(x, y, sw);                                      // Step 2. Base index to local index
            return localIndex;
        }
    }
}
