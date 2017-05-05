using System;
using System.Collections.Generic;


namespace g3
{
	/// <summary>
	/// Summary description for PolyLine.
	/// </summary>
	public class DPolyLine2f
	{
		public struct Edge {
			public int v1;
			public int v2;

			public Edge( int vertex1, int vertex2 ) {v1 = vertex1; v2 = vertex2;}
		}

		public struct Vertex {
			public int index;
			public float x;
			public float y;

			public Vertex(float fX, float fY, int nIndex) {x = fX; y = fY; index = nIndex; }
		}

        List<Vertex> m_vertices;
        List<Edge> m_edges;

		public DPolyLine2f()
		{
			m_vertices = new List<Vertex>();
			m_edges = new List<Edge>();
		}

		public DPolyLine2f( DPolyLine2f copy ) {
			m_vertices = new List<Vertex>( copy.m_vertices );
            m_edges = new List<Edge>( copy.m_edges );
		}

        public List<Edge> Edges {
            get { return m_edges; }
        }
        public List<Vertex> Vertices {
            get { return m_vertices; }
        }

		public int VertexCount {
			get { return m_vertices.Count; }
		}
		public int EdgeCount {
			get { return m_edges.Count; }
		}


        public void Clear() {
            m_vertices.Clear();
            m_edges.Clear();
        }


		public Vertex GetVertex(int i) {
			return m_vertices[i];
		}


		public int AddVertex( float fX, float fY) {
            int nIndex = m_vertices.Count;
            m_vertices.Add( new Vertex(fX, fY, nIndex) );
            return nIndex;
		}

		public int AddEdge( int v1, int v2 ) {
            int nIndex = m_edges.Count;
            m_edges.Add( new Edge(v1, v2) );
            return nIndex;
		}


/*
		public PolyLine Simplify(float fThreshold, int nMaxSkip) {

			// get rid of normals
			m_normals = null;

			// square threshold
			fThreshold *= fThreshold;

			// iterate through vertices
			PolyLine simpStroke = new PolyLine();

			int nLastGoodVertex = 0;
			Vertex vLastGood = m_vertices[nLastGoodVertex];
			Vertex vNext;

			simpStroke.AddVertex( vLastGood.x, vLastGood.y );

			while (nLastGoodVertex < m_nNextVertex - 2) {

				int nNextVert = nLastGoodVertex + 1;
				vNext = m_vertices[nNextVert];
				float fDistSqr = (vNext.x - vLastGood.x)*(vNext.x - vLastGood.x) + (vNext.y - vLastGood.y)*(vNext.y - vLastGood.y);
				int si = 0;
				while (fDistSqr < fThreshold && nNextVert < m_nNextVertex-2 && si < nMaxSkip) {
					nNextVert++;
					vNext = m_vertices[nNextVert];
					fDistSqr = (vNext.x - vLastGood.x)*(vNext.x - vLastGood.x) + (vNext.y - vLastGood.y)*(vNext.y - vLastGood.y);
					si++;
				}

				// add new vertex and edge
				if (fDistSqr > fThreshold || si >= nMaxSkip) {
					simpStroke.AddVertex( vNext.x, vNext.y );
					simpStroke.AddEdge( simpStroke.VertexCount-2, simpStroke.VertexCount-1 );
				} 

				nLastGoodVertex = nNextVert;
				vLastGood = vNext;
			}
			
			// always add last vertex
			simpStroke.AddVertex( m_vertices[m_nNextVertex-1].x, m_vertices[m_nNextVertex-1].y );
			simpStroke.AddEdge( simpStroke.VertexCount-2, simpStroke.VertexCount-1 );

			return simpStroke;
		}
*/

		// note: this function assumes that the PolyLine is a closed loop with
		// vertex and edges in random order, and tries to order them coherently...
		// (it is not very good...)
		public bool OrderVertices() {

			// need a new array of vertices and edges
            List<Vertex> newVertices = new List<Vertex>( m_vertices.Count );
            List<Edge> newEdges = new List<Edge>( m_edges.Count );
			int[] tmpEdges = new int[2];  // temporary

			// start at a random vertex, add to newVertices
			int nCurVertex = 0;
			int newVi = 0;
			int newEi = 0;
			newVertices[newVi++] = m_vertices[nCurVertex];

			// loop until all vertices are done
			int nLastEdge = -1;
			while (newVi != m_vertices.Count) {

				// find the two edges connected to this vertex. If there are
				// more than two, we are in trouble..
				int ei = 0;
				for (int i = 0; i < m_edges.Count; ++i) {
					if ( m_edges[i].v1 == nCurVertex || m_edges[i].v2 == nCurVertex ) {
						if (ei > 1) 
							return false;
						tmpEdges[ei++] = i;
					}
				}

				// we should have two now
				if (ei != 2) 
					return false;

				// one of them has to be the edge we touched last time, unless we
				// have something bad...
				int nWhichEdge = 0;
				if (nLastEdge == -1)
					nWhichEdge = 0;
				else if (tmpEdges[0] == nLastEdge)
					nWhichEdge = tmpEdges[1];
				else if (tmpEdges[1] == nLastEdge)
					nWhichEdge = tmpEdges[0];
				else
					return false;		// failure!

				// extract the other vertex from this edge
				int nNextVertex = ( m_edges[nWhichEdge].v1 == nCurVertex ) ? m_edges[nWhichEdge].v2 : m_edges[nWhichEdge].v1;

				// add this vertex
				newVertices[newVi++] = m_vertices[nNextVertex];

                // add an edge
                newEdges[newEi++] = new Edge(nCurVertex, nNextVertex);

				// update control variables
				nCurVertex = nNextVertex;
				nLastEdge = nWhichEdge;
			}

            // [RMS TODO] should we close the loop here ??
            newEdges[newEi++] = new Edge(nCurVertex, 0);

			// replace edges and vertices of this stroke, and clear normals
			m_edges = newEdges;
			m_vertices = newVertices;

			return true;

		}

	}


}
