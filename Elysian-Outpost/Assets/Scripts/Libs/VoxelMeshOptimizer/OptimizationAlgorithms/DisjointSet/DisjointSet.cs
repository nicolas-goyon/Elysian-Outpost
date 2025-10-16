namespace Libs.VoxelMeshOptimizer.OptimizationAlgorithms.DisjointSet
{

    public struct DisjointSet
    {
        private readonly int[] _parent; 

        private readonly int[] _size;
        
        private int _count; // number of components

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
            if (n < 0) throw new System.ArgumentOutOfRangeException(nameof(n), "must be non-negative");

            _count = n;
            _parent = new int[n];
            _size = new int[n];
            for (int i = 0; i < n; i++)
            {
                _parent[i] = i;
                _size[i] = 1;
            }
        }

        /**
            * Returns the number of sets.
            *
            * @return the number of sets (between {@code 1} and {@code n})
            */
        public int GetCount()
        {
            return _count;
        }

        public bool IsRoot(int p)
        {
            return _parent[p] == p;
        }


        /**
            * Returns the canonical element of the set containing element {@code p}.
            *
            * @param  p an element
            * @return the canonical element of the set containing {@code p}
            * @throws IllegalArgumentException unless {@code 0 <= p < n}
            */
        public readonly int Find(int p)
        {
            Validate(p);
            int root = p;
            while (root != _parent[root])
                root = _parent[root];
            while (p != root)
            {
                int newp = _parent[p];
                _parent[p] = root;
                p = newp;
            }

            return root;
        }

        // validate that p is a valid index
        private readonly void Validate(int p)
        {
            int n = _parent.Length;
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
            if (_size[rootP] < _size[rootQ])
            {
                _parent[rootP] = rootQ;
                _size[rootQ] += _size[rootP];
            }
            else
            {
                _parent[rootQ] = rootP;
                _size[rootP] += _size[rootQ];
            }

            _count--;
        }

    }
}
