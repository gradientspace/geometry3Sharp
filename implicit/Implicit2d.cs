using System;
using System.Collections.Generic;

namespace g3
{
	/// <summary>
	/// Summary description for ImplicitField2D.
	/// </summary>
	public interface ImplicitField2d
	{
		float Value( float fX, float fY );

		void Gradient( float fX, float fY, ref float fGX, ref float fGY );

		AxisAlignedBox2f Bounds { get; }
	}

	public interface ImplicitOperator2d : ImplicitField2d 
	{
		void AddChild( ImplicitField2d field );
	}




	public class ImplicitPoint2d : ImplicitField2d 
	{
        Vector2f m_vCenter;
		private float m_radius;

		public ImplicitPoint2d( float x, float y ) {
            m_vCenter = new Vector2f(x, y);
			m_radius = 1;
		}
		public ImplicitPoint2d( float x, float y, float radius ) {
            m_vCenter = new Vector2f(x, y);
            m_radius = radius;
		}

		public float Value( float fX, float fY ) {

			float tx = (fX - m_vCenter.x);
			float ty = (fY - m_vCenter.y);
			float fDist2 = tx*tx + ty*ty;
			fDist2 /= (m_radius*m_radius);
			fDist2 = 1.0f - fDist2;
			if ( fDist2 < 0.0f)
				return 0.0f;
			else
				return fDist2 * fDist2 * fDist2;
		}

		public AxisAlignedBox2f Bounds {
            get { 
                return new AxisAlignedBox2f(LowX, LowY, HighX, HighY);
            }
		}

		public void Gradient( float fX, float fY, ref float fGX, ref float fGY ) {
			float tx = (fX - m_vCenter.x);
			float ty = (fY - m_vCenter.y);
			float fDist2 = (tx*tx + ty*ty);
			float fTmp = 1.0f - fDist2;
			if ( fTmp < 0.0f) {
				fGX = fGY = 0;
			} else {
				float fSqrt = (float)Math.Sqrt(fDist2);
				float fGradMag = -6.0f * fSqrt * fTmp*fTmp;
				fGradMag /= fSqrt;
				fGX = tx * fGradMag;
				fGY = ty * fGradMag;
			}
		}

		public bool InBounds( float x, float y ) {
			return (x >= LowX && x <= HighX && x >= LowY && x <= HighY);
		}

		public float LowX {
			get { return m_vCenter.x - radius; }
		}

		public float LowY {
			get { return m_vCenter.y - radius; }
		}

		public float HighX {
			get { return m_vCenter.x + radius; }
		}

		public float HighY {
			get { return m_vCenter.y + radius; }
		}

		public float radius {
			get { return m_radius; }
			set { m_radius = value; }
		}

		public float x {
			get { return m_vCenter.x; }
			set { m_vCenter.x = value; }
		}

		public float y {
			get { return m_vCenter.y; }
			set { m_vCenter.y = value; }
		}

        public Vector2f Center {
            get { return m_vCenter; }
            set { m_vCenter = value; }
        }
	}



}
