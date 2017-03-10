using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{
    public class MeshConnectedComponents
    {
        public DMesh3 Mesh;

        public struct Component
        {
            public int[] Indices;
        }

        public List<Component> Components;



        public MeshConnectedComponents(DMesh3 mesh)
        {
            Mesh = mesh;
            Components = new List<Component>();
        }


        public int LargestByCount
        {
            get {
                int largest_i = 0;
                int largest_count = Components[largest_i].Indices.Length;
                for ( int i = 1; i < Components.Count; ++i ) {
                    if ( Components[i].Indices.Length > largest_count ) {
                        largest_count = Components[i].Indices.Length;
                        largest_i = i;
                    }
                }
                return largest_i;
            }
        }




        public void FindConnectedT()
        {
            Components = new List<Component>();

            int NT = Mesh.MaxTriangleID;

            // [TODO] could use Euler formula to determine if mesh is closed genus-0...

            // initial active set contains all triangles
            byte[] active = new byte[Mesh.MaxTriangleID];
            for ( int i = 0; i < NT; ++i ) {
                if (Mesh.IsTriangle(i)) {
                    active[i] = 0;
                } else
                    active[i] = 255;
            }

            // temporary buffers
            List<int> queue = new List<int>(NT / 10);
            List<int> cur_comp = new List<int>(NT / 10);

            // keep finding valid seed triangles and growing connected components
            // until we are done
            for ( int i = 0; i < NT; ++i ) {
                if (active[i] == 255)
                    continue;

                int seed_t = i;
                queue.Add(seed_t);
                active[seed_t] = 1;      // in queue

                while ( queue.Count > 0 ) {
                    int cur_t = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);

                    active[cur_t] = 2;   // tri has been processed
                    cur_comp.Add(cur_t);

                    Index3i nbrs = Mesh.GetTriNeighbourTris(cur_t);
                    for ( int j = 0; j < 3; ++j ) {
                        int nbr_t = nbrs[j];
                        if ( nbr_t != DMesh3.InvalidID && active[nbr_t] == 0 ) {
                            queue.Add(nbr_t);
                            active[nbr_t] = 1;           // in queue
                        }
                    }
                }


                Component comp = new Component() {
                    Indices = cur_comp.ToArray()
                };
                Components.Add(comp);

                // remove tris in this component from active set
                for ( int j = 0; j < comp.Indices.Length; ++j) {
                    active[comp.Indices[j]] = 255;
                }

                cur_comp.Clear();
                queue.Clear();
            }


        }




    }
}
