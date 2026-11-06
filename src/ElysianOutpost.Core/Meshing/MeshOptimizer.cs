// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

public interface MeshOptimizer
{
    public Mesh Optimize(Chunk chunk);
}
