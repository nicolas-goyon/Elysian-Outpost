
using System.Text;
using Base;

namespace Libs.VoxelMeshOptimizer.OcclusionAlgorithms.Common
{

    /// <summary>
    /// Represents a 2D plane (slice) of visible voxels, defined by a specific iteration order and orientation.
    /// 
    /// Terminology:
    /// - MajorAxis: First axis iterated in the nested loops, also known as the "slice" axis (outermost loop).
    /// - MiddleAxis: Second axis iterated (middle loop).
    /// - MinorAxis: Third axis iterated (innermost loop).
    /// - SliceIndex is the index along the MinorAxis dimension. (Depth)
    /// - Voxels is the 2D array of (width x height) or (depth x height), etc., 
    ///   depending on which axes form the plane.
    /// </summary>
    public class VisiblePlane
    {
        /// <summary>
        /// The outermost loop's iteration axis.
        /// </summary>
        public Axis MajorAxis { get; }

        public AxisOrder MajorAxisOrder { get; }

        /// <summary>
        /// The middle loop's iteration axis.
        /// </summary>
        public Axis MiddleAxis { get; }

        public AxisOrder MiddleAxisOrder { get; }

        /// <summary>
        /// The innermost loop's iteration axis, considered as the slicing axis.
        /// </summary>
        public Axis MinorAxis { get; }

        public AxisOrder MinorAxisOrder { get; }

        /// <summary>
        /// The index along the MinorAxis for this plane. In other terms, the depth inside the chunk along the major axis.
        /// For instance, if MinorAxis = FrontToBack, then this might be z=0,1,2...
        /// </summary>
        public uint SliceIndex { get; }

        /// <summary>
        /// 2D array of voxels contained within this visible plane.
        /// The exact dimensions depend on the axes forming the plane. For instance:
        /// - If MinorAxis is Z, the plane dimensions are Voxels[x, y].
        /// </summary>
        public Voxel[,]
            Voxels
        {
            get;
            set;
        } // FIXME : The set is not good, and the current access and modification of voxels is not protected through methods nor clarified.

        /// <summary>
        /// Initializes a new instance of the VisiblePlane class.
        /// </summary>
        /// <param name="majorAxis">Axis used in the outermost loop.</param>
        /// <param name="middleAxis">Axis used in the middle loop.</param>
        /// <param name="minorAxis">Axis used in the innermost loop.</param>
        /// <param name="sliceIndex">Position along the slicing (MinorAxis) axis.</param>
        /// <param name="width">Width of the voxel array along the MajorAxis.</param>
        /// <param name="height">Height of the voxel array along the MiddleAxis.</param>
        public VisiblePlane(
            Axis majorAxis, AxisOrder majorAxisOrder,
            Axis middleAxis, AxisOrder middleAxisOrder,
            Axis minorAxis, AxisOrder minorAxisOrder,
            uint sliceIndex,
            uint width,
            uint height)
        {
            MajorAxis = majorAxis;
            MajorAxisOrder = majorAxisOrder;

            MiddleAxis = middleAxis;
            MiddleAxisOrder = middleAxisOrder;

            MinorAxis = minorAxis;
            MinorAxisOrder = minorAxisOrder;


            SliceIndex = sliceIndex;
            Voxels = new Voxel[width, height];

        }

        /// <summary>
        /// Checks whether the plane is empty, i.e., contains no visible voxels.
        /// </summary>
        public bool IsPlaneEmpty
        {
            get
            {
                int w = Voxels.GetLength(0);
                int h = Voxels.GetLength(1);
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (Voxels[x, y] != null && Voxels[x, y]!.IsSolid) return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Returns a concise string representation of the VisiblePlane object.
        /// Example:
        /// "Plane(Major=+X, Middle=+Y, Minor=+Z, SliceIndex=2)"
        /// </summary>
        public override string ToString()
        {
            string majorSign = MajorAxisOrder == AxisOrder.ASCENDING ? "+" : "-";
            string middleSign = MiddleAxisOrder == AxisOrder.ASCENDING ? "+" : "-";
            string minorSign = MinorAxisOrder == AxisOrder.ASCENDING ? "+" : "-";

            return
                $"Plane(Major={majorSign}{MajorAxis}, Middle={middleSign}{MiddleAxis}, Minor={minorSign}{MinorAxis}, SliceIndex={SliceIndex})";
        }




        /// <summary>
        /// Produces a detailed multi-line description of the plane, including axes, slice index,
        /// and voxel IDs in a human-readable 2D grid.
        /// </summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();

            // Indique les signes +/- selon AxisOrder
            string majorSign = MajorAxisOrder == AxisOrder.ASCENDING ? "+" : "-";
            string middleSign = MiddleAxisOrder == AxisOrder.ASCENDING ? "+" : "-";
            string minorSign = MinorAxisOrder == AxisOrder.ASCENDING ? "+" : "-";

            // Header avec ordre ascendant/descendant
            sb.AppendLine(ToString());
            sb.AppendLine("Voxels (each cell shows 'ID' or '.' if null):");

            int width = Voxels.GetLength(0);
            int height = Voxels.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                sb.Append($"Row {y}: ");
                for (int x = 0; x < width; x++)
                {
                    Voxel v = Voxels[x, y];
                    sb.Append(v is null ? ". " : $"{v.ID} ");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }



        /// <summary>
        /// Converts the VisiblePlane's voxel array to a 2D int array,
        /// mapping each non-null voxel to its ID and null voxels to -1.
        /// </summary>
        /// <returns>A 2D integer array with voxel IDs or -1 for empty spaces.</returns>
        public int[,] ConvertToPixelArray()
        {
            int width = Voxels.GetLength(0);
            int height = Voxels.GetLength(1);
            int[,] pixelArray = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Voxel voxel = Voxels[x, y];
                    pixelArray[x, y] =
                        (voxel != null)
                            ? voxel.ID
                            : -1; // TODO : Get a look at this later on, we may need to throw errors.
                }
            }

            return pixelArray;
        }


    }
}