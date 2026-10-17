// This file is a changed version of the union-find code of algs4:
// https://github.com/kevin-wayne/algs4
// The comments come from WeightedQuickUnionUF.java. The Find method is like
// the method of WeightedQuickUnionPathCompressionUF.java (algs4 book site).
// The Elysian Outpost contributors translated the code from Java to C# in 2025.
// algs4 uses the GNU General Public License, version 3 or later. Thus this
// file uses the same license.
//
// Copyright 2002-2025, Robert Sedgewick and Kevin Wayne.
//
// This file is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This file is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this file (Elysian-Outpost/LICENSE-GPL-3.0.txt).
// If not, see <https://www.gnu.org/licenses/>.
//
// SPDX-License-Identifier: GPL-3.0-or-later

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
