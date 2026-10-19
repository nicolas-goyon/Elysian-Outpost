// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

// The result of the occlusion optimization: all visible planes, organized by direction.
public class VisibleFaces
{
    public Dictionary<(Axis, AxisOrder), List<VisiblePlane>> PlanesByAxis { get; }
        = new Dictionary<(Axis, AxisOrder), List<VisiblePlane>>();
}
