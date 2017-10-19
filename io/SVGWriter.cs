using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace g3
{
	public class SVGWriter
	{
		public struct Style {
			public string fill;
			public string stroke;
			public float stroke_width;

			public static readonly Style Default = new Style() { fill = "none", stroke = "black", stroke_width = 1 };

			public static Style Filled(string fillCol, string strokeCol = "", float strokeWidth = 0) {
				return new Style() { fill = fillCol, stroke = strokeCol, stroke_width = strokeWidth };
			}
			public static Style Outline(string strokeCol, float strokeWidth) {
				return new Style() { fill = "none", stroke = strokeCol, stroke_width = strokeWidth };
			}

			public override string ToString() {
				StringBuilder b = new StringBuilder();
				if (fill.Length > 0) { b.Append("fill:"); b.Append(fill); b.Append(';'); }
				if (stroke.Length > 0) { b.Append("stroke:"); b.Append(stroke); b.Append(';'); }
				if (stroke_width > 0) { b.Append("stroke-width:"); b.Append(stroke_width); b.Append(";"); }
				return b.ToString();
			}
		}


		Dictionary<object, Style> Styles = new Dictionary<object, Style>();

		public Style DefaultPolygonStyle;
		public Style DefaultPolylineStyle;
		public Style DefaultCircleStyle;
		public Style DefaultLineStyle;


		List<object> Objects;


		AxisAlignedBox2d Bounds;

		public int Precision = 3;
		public double BoundsPad = 10;

		public SVGWriter()
		{
			Objects = new List<object>();
			Bounds = AxisAlignedBox2d.Empty;

			DefaultPolygonStyle = Style.Outline("black", 1);
			DefaultPolylineStyle = Style.Outline("black", 1);
			DefaultCircleStyle = Style.Filled("green", "black", 1);
		}


		public void AddPolygon(Polygon2d poly) {
			Objects.Add(poly);
			Bounds.Contain(poly.Bounds);
		}
		public void AddPolygon(Polygon2d poly, Style style) {
			Objects.Add(poly);
			Styles[poly] = style;
			Bounds.Contain(poly.Bounds);
		}


		public void AddPolyline(PolyLine2d poly) {
			Objects.Add(poly);
			Bounds.Contain(poly.Bounds);
		}
		public void AddPolyline(PolyLine2d poly, Style style) {
			Objects.Add(poly);
			Styles[poly] = style;
			Bounds.Contain(poly.Bounds);
		}


		public void AddCircle(Circle2d circle) {
			Objects.Add(circle);
			Bounds.Contain(circle.Bounds);
		}
		public void AddCircle(Circle2d circle, Style style) {
			Objects.Add(circle);
			Styles[circle] = style;
			Bounds.Contain(circle.Bounds);
		}


		public void AddLine(Segment2d segment) {
			Objects.Add(new Segment2dBox(segment));
			Bounds.Contain(segment.P0); Bounds.Contain(segment.P1);
		}
		public void AddLine(Segment2d segment, Style style) {
			Segment2dBox segbox = new Segment2dBox(segment);
			Objects.Add(segbox);
			Styles[segbox] = style;
			Bounds.Contain(segment.P0); Bounds.Contain(segment.P1);
		}



		public IOWriteResult Write(string sFilename) {
			using (StreamWriter w = new StreamWriter(sFilename)) {
				if (w.BaseStream == null)
					return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");


				write_header_1_1(w);

				foreach (var o in Objects) {
					if (o is Polygon2d)
						write_polygon(o as Polygon2d, w);
					else if (o is PolyLine2d)
						write_polyline(o as PolyLine2d, w);
					else if (o is Circle2d)
						write_circle(o as Circle2d, w);
					else if (o is Segment2dBox)
						write_line(o as Segment2dBox, w);					
					else
						throw new Exception("SVGWriter.Write: unknown object type " + o.GetType().ToString());
				}


				w.WriteLine("</svg>");
			}

			return IOWriteResult.Ok;
		}



		void write_header_1_1(StreamWriter w) {
			StringBuilder b = new StringBuilder();

			b.Append("<svg ");
			b.Append("version=\"1.1\" " );
			b.Append("xmlns=\"http://www.w3.org/2000/svg\" ");
			b.Append("xmlns:xlink=\"http://www.w3.org/1999/xlink\" ");
			b.Append("x=\"0px\" y=\"0px\" ");
			b.Append(string.Format("viewBox=\"{0} {1} {2} {3}\" ",
			                       Math.Round(Bounds.Min.x-BoundsPad,Precision),
			                       Math.Round(Bounds.Min.y-BoundsPad,Precision),
			                       Math.Round(Bounds.Width+2*BoundsPad,Precision),
			                       Math.Round(Bounds.Height+2*BoundsPad,Precision)));
			b.Append('>');

			w.WriteLine(b);
		}



		void write_polygon(Polygon2d poly, StreamWriter w) 
		{
			StringBuilder b = new StringBuilder();
			b.Append("<polygon points=\"");
			for (int i = 0; i < poly.VertexCount; ++i ) {
				b.Append(Math.Round(poly[i].x,Precision)); 
				b.Append(','); 
				b.Append(Math.Round(poly[i].y, Precision));
				if (i < poly.VertexCount - 1)
					b.Append(' ');
			}
			b.Append("\" ");
			append_style(b, poly, ref DefaultPolygonStyle);
			b.Append(" />");

			w.WriteLine(b);
		}



		void write_polyline(PolyLine2d poly, StreamWriter w)
		{
			StringBuilder b = new StringBuilder();
			b.Append("<polyline points=\"");
			for (int i = 0; i < poly.VertexCount; ++i) {
				b.Append(Math.Round(poly[i].x, Precision));
				b.Append(',');
				b.Append(Math.Round(poly[i].y, Precision));
				if (i < poly.VertexCount - 1)
					b.Append(' ');
			}
			b.Append("\" ");
			append_style(b, poly, ref DefaultPolylineStyle);
			b.Append(" />");

			w.WriteLine(b);
		}



		void write_circle(Circle2d circle, StreamWriter w)
		{
			StringBuilder b = new StringBuilder();
			b.Append("<circle ");
			append_property("cx", circle.Center.x, b, true);
			append_property("cy", circle.Center.y, b, true);
			append_property("r", circle.Radius, b, true);
			append_style(b, circle, ref DefaultCircleStyle);
			b.Append(" />");
			w.WriteLine(b);
		}


		void write_line(Segment2dBox segbox, StreamWriter w)
		{
			Segment2d seg = (Segment2d)segbox;
			StringBuilder b = new StringBuilder();
			b.Append("<line ");
			Vector2d p0 = seg.P0, p1 = seg.P1;
			append_property("x1", p0.x, b, true);
			append_property("y1", p0.y, b, true);
			append_property("x2", p1.x, b, true);
			append_property("y2", p1.y, b, true);
			append_style(b, segbox, ref DefaultLineStyle);
			b.Append(" />");
			w.WriteLine(b);
		}


		void append_property(string name, double val, StringBuilder b, bool trailSpace = true) {
			b.Append(name); b.Append("=\"");
			b.Append(Math.Round(val, Precision));
			if ( trailSpace )
				b.Append("\" ");
			else
				b.Append("\"");
		}


		void append_style(StringBuilder b, object o, ref Style defaultStyle) {
			Style style;
			if (Styles.TryGetValue(o, out style) == false)
				style = defaultStyle;
			b.Append("style=\"");
			b.Append(style.ToString());
			b.Append("\"");			
		}



	}
}
