namespace Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet
{

    public struct DisjointSet
    {
        private int[] parent; // parent[i] = parent of i

        private int[] size; // size[i] = number of sites in tree rooted at i
        // Note: not necessarily correct if i is not a root node
        
        private int count; // number of components

        /**
            * Initializes an empty union-find data structure with
            * {@code n} elements {@code 0} through {@code n-1}.
            * Initially, each element is in its own set.
            *
            * @param  n the number of elements
            * @throws IllegalArgumentException if {@code n < 0}
            */
        public DisjointSet(int n)
        {
            if (n < 0) throw new System.ArgumentOutOfRangeException("n should be over or equal to 0");

            count = n;
            parent = new int[n];
            size = new int[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                size[i] = 1;
            }
        }

        /**
            * Returns the number of sets.
            *
            * @return the number of sets (between {@code 1} and {@code n})
            */
        public int GetCount()
        {
            return count;
        }

        public bool IsRoot(int p)
        {
            return parent[p] == p;
        }


        /**
            * Returns the canonical element of the set containing element {@code p}.
            *
            * @param  p an element
            * @return the canonical element of the set containing {@code p}
            * @throws IllegalArgumentException unless {@code 0 <= p < n}
            */
        public int Find(int p)
        {
            Validate(p);
            int root = p;
            while (root != parent[root])
                root = parent[root];
            while (p != root)
            {
                int newp = parent[p];
                parent[p] = root;
                p = newp;
            }

            return root;
        }

        // validate that p is a valid index
        private void Validate(int p)
        {
            int n = parent.Length;
            if (p < 0 || p >= n)
            {
                throw new System.IndexOutOfRangeException("index " + p + " is not between 0 and " + (n - 1));
            }
        }

        /**
            * Merges the set containing element {@code p} with the set
            * containing element {@code q}.
            *
            * @param  p one element
            * @param  q the other element
            * @throws IllegalArgumentException unless
            *         both {@code 0 <= p < n} and {@code 0 <= q < n}
            */
        public void Union(int p, int q)
        {
            int rootP = Find(p);
            int rootQ = Find(q);
            if (rootP == rootQ) return;

            // make smaller root point to larger one
            if (size[rootP] < size[rootQ])
            {
                parent[rootP] = rootQ;
                size[rootQ] += size[rootP];
            }
            else
            {
                parent[rootQ] = rootP;
                size[rootP] += size[rootQ];
            }

            count--;
        }

    }
}
