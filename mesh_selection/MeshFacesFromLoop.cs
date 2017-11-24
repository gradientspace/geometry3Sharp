using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace g3
{
    /// <summary>
    /// Find mesh triangles enclosed by a curve embedded in the mesh
    /// If a seed triangle in the enclosed region is not provided, then the
    /// smaller of the two largest connected-components is chosen as the "inside".
    /// </summary>
    public class MeshFacesFromLoop
    {
        public DMesh3 Mesh;

        int[] InitialLoopT;

        List<int> PathT;
        List<int> InteriorT;

        public MeshFacesFromLoop(DMesh3 Mesh, DCurve3 SpaceCurve, ISpatial Spatial)
        {
            this.Mesh = Mesh;

            int N = SpaceCurve.VertexCount;
            InitialLoopT = new int[N];
            for (int i = 0; i < N; ++i)
                InitialLoopT[i] = Spatial.FindNearestTriangle(SpaceCurve[i]);

            find_path();
            find_interior_from_tris();
        }


        public MeshFacesFromLoop(DMesh3 Mesh, DCurve3 SpaceCurve, ISpatial Spatial, int tSeed)
        {
            this.Mesh = Mesh;

            int N = SpaceCurve.VertexCount;
            InitialLoopT = new int[N];
            for (int i = 0; i < N; ++i)
                InitialLoopT[i] = Spatial.FindNearestTriangle(SpaceCurve[i]);

            find_path();
            find_interior_from_seed(tSeed);
        }

        /// <summary>
        /// returns new array containing selected triangles 
        /// </summary>
        public int[] ToArray()
        {
            return InteriorT.ToArray();
        }

        public MeshFaceSelection ToSelection()
        {
            MeshFaceSelection selection = new MeshFaceSelection(Mesh);
            //selection.Select(PathT);
            selection.Select(InteriorT);
            return selection;
        }

        public IList<int> PathTriangles {
            get { return PathT; }
        }
        public IList<int> InteriorTriangles {
            get { return InteriorT; }
        }





        // assumes PathT is contains set of triangles
        // that are fully connected, ie a flood-fill cannot escape!
        void find_interior_from_tris()
        {
            MeshFaceSelection pathNbrs = new MeshFaceSelection(Mesh);
            pathNbrs.Select(PathT);
            pathNbrs.ExpandToOneRingNeighbours();
            pathNbrs.Deselect(PathT);

            MeshConnectedComponents connected = new MeshConnectedComponents(Mesh);
            connected.FilterSet = pathNbrs;
            connected.FindConnectedT();
            int N = connected.Count;

            if (N < 2)
                throw new Exception("MeshFacesFromLoop.find_interior: only found one connected component!");

            // only consider 2 largest. somehow we are sometimes getting additional
            // "outside" components, and if we do growing from there, it covers whole mesh??
            connected.SortByCount(false);
            N = 2;

            MeshFaceSelection[] selections = new MeshFaceSelection[N];
            bool[] done = new bool[N];
            for (int i = 0; i < N; ++i) {
                selections[i] = new MeshFaceSelection(Mesh);
                selections[i].Select(connected.Components[i].Indices);
                done[i] = false;
            }

            HashSet<int> border_tris = new HashSet<int>(PathT);
            Func<int, bool> borderF = (tid) => { return border_tris.Contains(tid) == false; };

            // 'largest' flood fill is expensive...if we had a sense of tooth size we could reduce cost?
            for (int i = 0; i < N; ++i) {
                selections[i].FloodFill(connected.Components[i].Indices, borderF);
            }
            Array.Sort(selections, (a, b) => { return a.Count.CompareTo(b.Count); });
            InteriorT = new List<int>(selections[0]);
        }




        // assumes PathT contains set of triangles
        // that are fully connected, ie a flood-fill cannot escape!
        void find_interior_from_seed(int tSeed)
        {
            MeshFaceSelection selection = new MeshFaceSelection(Mesh);
            selection.Select(PathT);
            selection.FloodFill(tSeed);

            InteriorT = new List<int>(selection);
        }



            // [RMS] this is nicer but it is also incredibly slow because ExpandToOneRingNbrs is O(N)
            //   (maybe have ExpandToOneRingNeighbours that can take previous ring??)

            //int nRemaining = N;
            //while ( nRemaining > 1) {
            //    nRemaining = 0;
            //    for ( int i = 0; i < N; ++i ) {
            //        if (done[i])
            //            continue;
            //        int prev = selections[i].Count;
            //        selections[i].ExpandToOneRingNeighbours(borderF);
            //        if (selections[i].Count == prev)
            //            done[i] = true;
            //        else
            //            nRemaining++;
            //    }
            //}

            //InteriorT = new List<int>(PathT);
            //for ( int i = 0; i < N; ++i ) {
            //    if (done[i])
            //        InteriorT.AddRange(selections[i]);
            //}



            // [TODO] handle cases with > 2 components...right now only second-largest component
            //   will be included, any smaller will be ignored...

            // [RMS] this code doesn't work because using triangle count is not a reliable way
            // to decide which set is on the inside!

            //connected.SortByCount(false);
            //int[] inner = connected.Components[1].Indices;
            //HashSet<int> border_tris = new HashSet<int>(PathT);
            //HashSet<int> inner_tris = new HashSet<int>(inner);

            //MeshConnectedComponents connected2 = new MeshConnectedComponents(Mesh);
            //connected2.FilterF = (tid) => { return border_tris.Contains(tid) == false; };
            //connected2.SeedFilterF = (tid) => { return inner_tris.Contains(tid); };
            //connected2.FindConnectedT();

            //InteriorT = new List<int>();
            //foreach (var component in connected2.Components)
            //    InteriorT.AddRange(component.Indices);






        void find_path()
        {
            PathT = new List<int>();
            PathT.Add(InitialLoopT[0]);

            for ( int i = 1; i <= InitialLoopT.Length; ++i ) {
                int prevT = PathT[PathT.Count - 1];
                int nextT = InitialLoopT[i % InitialLoopT.Length];

                // if in same tri, we can skip this vtx
                if (nextT == prevT)
                    continue;

                // if tri is nbr of prev tri, then path is connected
                Index3i nbrT = Mesh.GetTriNeighbourTris(prevT);
                if ( nbrT.a == nextT || nbrT.b == nextT || nbrT.c == nextT ) {
                    PathT.Add(nextT);
                    continue;
                }

                // otherwise we have to find a path
                List<int> path = find_path(prevT, nextT);
                PathT.AddRange(path);
                PathT.Add(nextT);
            }

            if (PathT[PathT.Count - 1] == PathT[0])
                PathT.RemoveAt(PathT.Count - 1);
        }




        struct TriWithParent
        {
            public int tID;
            public int parentID;
        }
        List<TriWithParent> sequence = new List<TriWithParent>(32);
        HashSet<int> used = new HashSet<int>();
        List<int> buffer = new List<int>(32);


        void push_onto_sequence(int parentID)
        {
            // [TODO] use distances here? if already in set, could check and replace w/ shortest distance?
            //   - could store index in TriWithParent to make finding faster...

            Index3i nbrs = Mesh.GetTriNeighbourTris(parentID);
            for ( int j = 0; j < 3; ++j ) {
                if (used.Contains(nbrs[j]))
                    continue;
                sequence.Add(new TriWithParent() { tID = nbrs[j], parentID = parentID });
                used.Add(nbrs[j]);
            }
        }


        // returns set of triangles connecting triangle t1 to t2.
        // set does *not* contain t1 or t2
        List<int> find_path(int t1, int t2)
        {
            // TODO: A* style that searches towards t2...

            buffer.Clear();
            sequence.Clear();

            used.Clear();
            used.Add(t1);


            push_onto_sequence(t1);
            int si = 0;
            int foundi = -1;

            while ( foundi == -1 ) { 
                TriWithParent t = sequence[si];
                Index3i nbrs = Mesh.GetTriNeighbourTris(t.tID);
                if (nbrs.a == t2 || nbrs.b == t2 || nbrs.c == t2)
                    foundi = si;
                else 
                    push_onto_sequence(t.tID);
                si++;
            }
            if (foundi == -1)
                throw new Exception("MeshFacesFromLoop.find_path : could not find path!!");

            // walk backwards along path
            TriWithParent tCur = sequence[foundi];
            buffer.Add(tCur.tID);
            while ( tCur.parentID != t1 ) {
                // find parent
                tCur = sequence.Find((x) => { return x.tID == tCur.parentID; });
                buffer.Add(tCur.tID);
            }

            buffer.Reverse();
            return buffer;
        }


    }
}
