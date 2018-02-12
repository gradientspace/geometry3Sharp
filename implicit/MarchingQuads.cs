using System;
using System.Collections;


namespace g3
{
	/// <summary>
	/// 2D MarchingQuads polyline extraction from scalar field
    /// [TODO] this is very, very old code. Should at minimum rewrite using current
    /// vector classes/etc.
	/// </summary>
	public class MarchingQuads
	{
		DPolyLine2f m_stroke;

		AxisAlignedBox2f m_bounds;
		float m_fXShift;
		float m_fYShift;
		float m_fScale;

		int m_nCells;
		float m_fCellSize;

		static float s_fValueSentinel = 9999999.0f;
		
		float m_fIsoValue;

		static int LEFT = 0x1;
		static int TOP = 0x2;
		static int RIGHT = 0x4;
		static int BOTTOM = 0x8;
		static int ALL = 0xF;

		struct Cell {
			uint nPosition;    // 16 bits each for x and y  (max 16k per axis)
			public float fValue;     // value in top left corner
			public int nLeftVertex;  // vertex on left edge
			public int nTopVertex;   // vertex on top edge
			public bool bTouched;   // true if node has been seen

			public void Initialize( uint x, uint y ) {
				this.x = x; this.y = y;
				fValue = s_fValueSentinel;
				nLeftVertex = nTopVertex = -1;
				bTouched = false;
			}

			public uint x {
				get { return nPosition & 0xFFFF; }
				set { nPosition = (y << 16) | (value&0xFFFF); }
			}

			public uint y {
				get { return (nPosition >> 16) & 0xFFFF; }
				set { nPosition = ((value & 0xFFFF)<<16) | x; }
			}
		}

		Cell[][] m_cells;


		struct SeedPoint {
			public float x;
			public float y;
			public SeedPoint(float fX, float fY) {x = fX; y = fY; }
		}

		ArrayList m_seedPoints;

		ImplicitField2d m_field;

		ArrayList m_cellStack;

		bool[] m_bEdgeSigns;


		public MarchingQuads(int nSubdivisions, AxisAlignedBox2f bounds, float fIsoValue) {
			m_stroke = new DPolyLine2f();
			m_bounds = new AxisAlignedBox2f();
			
			m_nCells = nSubdivisions;
			SetBounds(bounds);

			m_cells = null;
			InitializeCells();

			m_seedPoints = new ArrayList();
			m_cellStack = new ArrayList();

			m_bEdgeSigns = new bool[4];

			m_fIsoValue = fIsoValue;
		}

		public int Subdivisions {
			get { return m_nCells; }
			set { m_nCells = value; SetBounds( m_bounds ); InitializeCells(); }
		}

		public AxisAlignedBox2f Bounds {
			get { return m_bounds; }
			set { SetBounds(value); }
		}

		public DPolyLine2f Stroke {
			get { return m_stroke; }
		}


		public 	AxisAlignedBox2f GetBounds() {
			return m_bounds;
		}


		public void AddSeedPoint( float x, float y ) {
			m_seedPoints.Add( new SeedPoint(x - m_fXShift, y - m_fYShift) );
		}

		public void ClearSeedPoints() {
			m_seedPoints.Clear();
		}

		public void ClearStroke() {
            m_stroke.Clear();
		}

		public void Polygonize( ImplicitField2d field ) {

			m_field = field;

			ResetCells();  // reset bTouched flags

			m_cellStack.Clear();

			// iterate over seed points
			for (int i = 0; i < m_seedPoints.Count; ++i) {

				SeedPoint p = (SeedPoint)m_seedPoints[i];
				int xi = (int)(p.x / m_fCellSize);
				int yi = (int)(p.y / m_fCellSize);

				bool bFoundSurface = false;
				while (!bFoundSurface && yi > 0 && yi < m_cells.Length-1 && xi > 0 && xi < m_cells[0].Length-1 ) {

					if ( m_cells[yi][xi].bTouched == false ) {
						bool bResult = ProcessCell(xi,yi);
						if (bResult == true)
							bFoundSurface = true;
					} else
						bFoundSurface = true;
					xi--;
				}

				while ( m_cellStack.Count != 0 ) {
					Cell cell = (Cell)m_cellStack[ m_cellStack.Count - 1];
					m_cellStack.RemoveAt( m_cellStack.Count - 1 );

					if ( m_cells[ (int)cell.y ][ (int)cell.x ].bTouched == false) {
						bool bResult = ProcessCell( (int)cell.x, (int)cell.y );
						if (bResult == false)
							bResult = true;
					}
				}
			}
			
		}


		void SubdivideStep(ref float fValue1, ref float fValue2, ref float fX1, ref float fY1, ref float fX2, ref float fY2, 
						bool bVerticalEdge) {

	        float fAlpha = 0.5f;

	        float fX = 0.0f, fY = 0.0f;
	        if (bVerticalEdge) {
		        fX = fX1;
		        fY = fAlpha*fY1 + (1.0f-fAlpha)*fY2;
	        } else {
		        fX = fAlpha*fX1	+ (1.0f-fAlpha)*fX2;
		        fY = fY1;
	        }

	        float fValue = (float)m_field.Value(fX, fY);
	        if (fValue < m_fIsoValue) {
		        fValue1 = fValue;
		        fX1 = fX;
		        fY1 = fY;
	        } else {
		        fValue2 = fValue;
		        fX2 = fX;
		        fY2 = fY;
	        }

		}

		int LerpAndAddStrokeVertex( float fValue1, float fValue2, int x1, int y1, int x2, int y2, bool bVerticalEdge ) {

			// swap if need be
			if (fValue1 > fValue2) {
				int nSwap = x1;
				x1 = x2;
				x2 = nSwap;

				nSwap = y1;
				y1 = y2;
				y2 = nSwap;

				float fSwap = fValue1;
				fValue1 = fValue2;
				fValue2 = fSwap;
			}

			float fRefValue1 = fValue1;
			float fRefValue2 = fValue2;
			float fX1 = (float)x1 * m_fCellSize + m_fXShift;
			float fY1 = (float)y1 * m_fCellSize + m_fYShift;
			float fX2 = (float)x2 * m_fCellSize + m_fXShift;
			float fY2 = (float)y2 * m_fCellSize + m_fYShift;

            for (int i = 0; i < 10; ++i)
                SubdivideStep(ref fRefValue1, ref fRefValue2, ref fX1, ref fY1, ref fX2, ref fY2, bVerticalEdge);

			if ( Math.Abs(fRefValue1) < Math.Abs(fRefValue2) ) {
				return m_stroke.AddVertex(fX1, fY1);
			} else {
				return m_stroke.AddVertex(fX2, fY2);
			}

		}


		int GetLeftEdgeVertex(int xi, int yi) {

			Cell cell = m_cells[yi][xi];
			if (cell.nLeftVertex != -1)
				return cell.nLeftVertex;
			m_cells[yi][xi].nLeftVertex = LerpAndAddStrokeVertex(cell.fValue, m_cells[yi+1][xi].fValue, 
				xi, yi, xi, yi+1, true);
			return m_cells[yi][xi].nLeftVertex;
		}

		int GetRightEdgeVertex(int xi, int yi) {

			Cell cell = m_cells[yi][xi+1];
			if (cell.nLeftVertex != -1)
				return cell.nLeftVertex;
			m_cells[yi][xi+1].nLeftVertex = LerpAndAddStrokeVertex( cell.fValue, m_cells[yi+1][xi+1].fValue, 
				xi+1, yi, xi+1, yi+1, true);
			return m_cells[yi][xi+1].nLeftVertex;
		}

		int GetTopEdgeVertex(int xi, int yi) {

			Cell cell = m_cells[yi][xi];
			if (cell.nTopVertex != -1)
				return cell.nTopVertex;
			m_cells[yi][xi].nTopVertex = LerpAndAddStrokeVertex(cell.fValue, m_cells[yi][xi+1].fValue, 
				xi, yi, xi+1, yi, false);
			return m_cells[yi][xi].nTopVertex;
		}

		int GetBottomEdgeVertex(int xi, int yi) {

			Cell cell = m_cells[yi+1][xi];
			if (cell.nTopVertex != -1)
				return cell.nTopVertex;
			m_cells[yi+1][xi].nTopVertex = LerpAndAddStrokeVertex(cell.fValue, m_cells[yi+1][xi+1].fValue, 
				xi, yi+1, xi+1, yi+1, false);
			return m_cells[yi+1][xi].nTopVertex;
		}

		bool ProcessCell( int xi, int yi ) {

			m_cells[yi][xi].bTouched = true;

			int nCase = 0;
			for (int i = 0; i < 4; ++i) {
				int nxi = xi + (i & 1);
				int nyi = yi + ((i >> 1) & 1);
				if (m_cells[nyi][nxi].fValue == s_fValueSentinel)
					m_cells[nyi][nxi].fValue = m_field.Value( (float)nxi * m_fCellSize + m_fXShift, (float)nyi*m_fCellSize + m_fYShift );
				m_bEdgeSigns[i] = (m_cells[nyi][nxi].fValue > m_fIsoValue);
				nCase |= (m_bEdgeSigns[i] == true ? 1 : 0)  << i;

			}

			if (nCase == 0 || nCase == 15)
				return false;		// nothing to do - inside or outside...


			// don't actually need to compute all of these...
			int nLeftV = 0, nRightV = 0, nTopV = 0, nBottomV = 0;
			if ( m_bEdgeSigns[0] != m_bEdgeSigns[2] )
				nLeftV = GetLeftEdgeVertex(xi,yi);
			if ( m_bEdgeSigns[1] != m_bEdgeSigns[3] )
				nRightV = GetRightEdgeVertex(xi,yi);
			if ( m_bEdgeSigns[0] != m_bEdgeSigns[1] )
				nTopV = GetTopEdgeVertex(xi,yi);
			if ( m_bEdgeSigns[2] != m_bEdgeSigns[3] )
				nBottomV = GetBottomEdgeVertex(xi,yi);

			// evaluate "middle" decider case...
			float fDecider = 0.0f;
			if (nCase == 6 || nCase == 9)
				fDecider = m_field.Value( (float)xi * m_fCellSize + m_fCellSize/2.0f + m_fXShift, 
					(float)yi*m_fCellSize + m_fCellSize/2.0f + m_fYShift );

			int nSidesToPush = 0;

			switch(nCase) {
				case 1:
				case 14:
					m_stroke.AddEdge(nLeftV, nTopV);
					nSidesToPush = (LEFT | TOP);
					break;
				case 2:
				case 13:
					m_stroke.AddEdge(nTopV, nRightV);
					nSidesToPush = (RIGHT | TOP);
					break;
				case 4:
				case 11:
					m_stroke.AddEdge(nBottomV, nLeftV);
					nSidesToPush = (LEFT | BOTTOM);
					break;
				case 7:
				case 8:
					m_stroke.AddEdge(nRightV, nBottomV);
					nSidesToPush = (RIGHT | BOTTOM);
					break;

				case 3:
				case 12:
					m_stroke.AddEdge(nRightV, nLeftV);
					nSidesToPush = (LEFT | RIGHT);
					break;
				case 5:
				case 10:
					m_stroke.AddEdge(nTopV, nBottomV);
					nSidesToPush = (BOTTOM | TOP);
					break;

				case 9:
					if (fDecider > m_fIsoValue) {
						m_stroke.AddEdge(nLeftV,nBottomV);
						m_stroke.AddEdge(nTopV,nRightV);
					} else {
						m_stroke.AddEdge(nLeftV, nTopV);
						m_stroke.AddEdge(nBottomV, nRightV);
					}
					nSidesToPush = ALL;
					break;

				case 6:
					if (fDecider > m_fIsoValue) {
						m_stroke.AddEdge(nLeftV, nTopV);
						m_stroke.AddEdge(nBottomV, nRightV);
					} else {
						m_stroke.AddEdge(nLeftV,nBottomV);
						m_stroke.AddEdge(nTopV,nRightV);
					}
					nSidesToPush = ALL;
					break;
			}
		

			// ?!??!?! WHY ARE TOP AND BOTTOM REVERSED ????!?!?!?! 
			// because the "Top" edge is the "y" edge, and the "Bottom" edge is the "y+1" edge.
			// So when we want to push the quad "below" the "Bottom" edge, that (y+1), and
			// the one "Above" the top edge is (y-1). Maybe rename?

			if ((nSidesToPush & LEFT) != 0 && xi-1 >= 0 && m_cells[yi][xi-1].bTouched == false)
				m_cellStack.Add( m_cells[yi][xi-1] );
			if ((nSidesToPush & RIGHT) != 0 && xi+1 < m_nCells && m_cells[yi][xi+1].bTouched == false)
				m_cellStack.Add( m_cells[yi][xi+1] );
			if ((nSidesToPush & BOTTOM) != 0 && yi+1 < m_nCells && m_cells[yi+1][xi].bTouched == false)
				m_cellStack.Add( m_cells[yi+1][xi] );
			if ((nSidesToPush & TOP) != 0 && yi-1 >= 0 && m_cells[yi-1][xi].bTouched == false)
				m_cellStack.Add( m_cells[yi-1][xi] );

			return true;

		}
		
		
		// private members

		void ResetCells() {
			for (uint y = 0; y < m_cells.Length; ++y) {
				for (uint x = 0; x < m_cells.Length; ++x) {
					m_cells[y][x].bTouched = false;
                    m_cells[y][x].nLeftVertex = m_cells[y][x].nTopVertex = -1;
				}
			}
		}

		void InitializeCells() {

			m_cells = new Cell[m_nCells + 1][];
			for (uint y = 0; y < m_cells.Length; ++y) {
				m_cells[y] = new Cell[m_nCells + 1];
				for (uint x = 0; x < m_cells.Length; ++x) {
					m_cells[y][x].Initialize(x,y);
				}
			}
		}

		void SetBounds( AxisAlignedBox2f bounds ) {
            m_bounds = bounds;

			m_fXShift = (bounds.Min.x < 0) ? bounds.Min.x : -bounds.Min.x;
			m_fYShift = (bounds.Min.y < 0) ? bounds.Min.y : -bounds.Min.y;

			m_fScale = (bounds.Width > bounds.Height) ? bounds.Width : bounds.Height;

			m_fCellSize = m_fScale / m_nCells;
		}


	}
}
