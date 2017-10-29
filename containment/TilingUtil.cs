using System;
using System.Collections.Generic;

namespace g3
{
	public static class TilingUtil
	{


		/// <summary>
		/// Regular-grid tiling of element inside bounds, with spacing between elements
		/// Returns list of translations to element.
		/// Always allows at least one row and column, even if element overflows bounds in that dimension.
		/// </summary>
		public static List<Vector2d> BoundedRegularTiling2(AxisAlignedBox2d element, AxisAlignedBox2d bounds,
														   double spacing)
		{
			Vector2d oshift = -element.Min;
			double w = element.Width; double h = element.Height;

			int nx = Math.Max(1, (int)(bounds.Width / w));
			double spacew = (nx - 1) * spacing;
			while (nx > 1 && bounds.Width - (w * nx + spacew) < 0)
				nx--;

			int ny = Math.Max(1, (int)(bounds.Height / h));
			double spaceh = (ny - 1) * spacing;
			while (ny > 1 && bounds.Height - (w * ny + spaceh) < 0)
				ny--;

			List<Vector2d> translations = new List<Vector2d>();
			for (int yi = 0; yi < ny; ++yi) {
				double dy = yi * h + yi * spacing;
				for (int xi = 0; xi < nx; ++xi) {
					double dx = xi * w + xi * spacing;
					translations.Add(new Vector2d(dx, dy) + oshift + bounds.Min);
				}
			}

			return translations;
		}


	}
}
