using UnityEngine;

namespace VoxelMaster
{
    public abstract class BaseGeneration : MonoBehaviour
    {
        public virtual short Generation(int x, int y, int z)
        {
            return -1;
        }
    }
}