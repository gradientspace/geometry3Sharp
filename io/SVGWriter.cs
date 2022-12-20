using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Globalization;

namespace g3
{
	public class SVGWriter
	{
        public bool FlipY = true;


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
        public Style DefaultDGraphStyle;
        public Style DefaultCircleStyle;
        public Style DefaultArcStyle;
        public Style DefaultLineStyle;


        List<object> Objects;


		AxisAlignedBox2d Bounds;

		public int Precision = 3;
		public double BoundsPad = 10;


		public SVGWriter()
		{
			Objects = new List<object>();
			Bounds = AxisAlignedBox2d.Empty;

			DefaultPolygonStyle = Style.Outline("grey", 1);
			DefaultPolylineStyle = Style.Outline("cyan", 1);
			DefaultCircleStyle = Style.Filled("green", "black", 1);
            DefaultArcStyle = Style.Outline("magenta", 1);
            DefaultLineStyle = Style.Outline("black", 1);
            DefaultDGraphStyle = Style.Outline("blue", 1);
        }


        public void SetDefaultLineWidth(float width)
        {
            DefaultPolygonStyle.stroke_width = width;
            DefaultPolylineStyle.stroke_width = width;
            DefaultCircleStyle.stroke_width = width;
            DefaultArcStyle.stroke_width = width;
            DefaultLineStyle.stroke_width = width;
            DefaultDGraphStyle.stroke_width = width;
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


        public void AddBox(AxisAlignedBox2d box) {
            AddBox(box, DefaultPolygonStyle);
        }
        public void AddBox(AxisAlignedBox2d box, Style style)
        {
            Polygon2d poly = new Polygon2d();
            for (int k = 0; k < 4; ++k)
                poly.AppendVertex(box.GetCorner(k));
            AddPolygon(poly, style);
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


        public void AddGraph(DGraph2 graph)
        {
            Objects.Add(graph);
            Bounds.Contain(graph.GetBounds());
        }
        public void AddGraph(DGraph2 graph, Style style)
        {
            Objects.Add(graph);
            Styles[graph] = style;
            Bounds.Contain(graph.GetBounds());
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


        public void AddArc(Arc2d arc)
        {
            Objects.Add(arc);
            Bounds.Contain(arc.Bounds);
        }
        public void AddArc(Arc2d arc, Style style)
        {
            Objects.Add(arc);
            Styles[arc] = style;
            Bounds.Contain(arc.Bounds);
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


        public void AddComplex(PlanarComplex complex)
        {
            Objects.Add(complex);
            Bounds.Contain(complex.Bounds());
        }



		public IOWriteResult Write(string sFilename) {
            var current_culture = Thread.CurrentThread.CurrentCulture;

            try {
                // push invariant culture for write
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

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
                        else if (o is Arc2d)
                            write_arc(o as Arc2d, w);
                        else if (o is Segment2dBox)
                            write_line(o as Segment2dBox, w);
                        else if (o is DGraph2)
                            write_graph(o as DGraph2, w);
                        else if (o is PlanarComplex)
                            write_complex(o as PlanarComplex, w);
                        else
                            throw new Exception("SVGWriter.Write: unknown object type " + o.GetType().ToString());
                    }


                    w.WriteLine("</svg>");
                }

                // restore culture
                Thread.CurrentThread.CurrentCulture = current_culture;
                return IOWriteResult.Ok;

            } catch (Exception e) {
                Thread.CurrentThread.CurrentCulture = current_culture;
                return new IOWriteResult(IOCode.WriterError, "Unknown error : exception : " + e.Message);
            }
		}







        public static void QuickWrite(List<GeneralPolygon2d> polygons, string sPath, double line_width = 1)
        {
            SVGWriter writer = new SVGWriter();
            Style outer_cw = SVGWriter.Style.Outline("black", 2*(float)line_width);
            Style outer_ccw = SVGWriter.Style.Outline("green", 2 * (float)line_width);
            Style inner = SVGWriter.Style.Outline("red", (float)line_width);
            foreach (GeneralPolygon2d poly in polygons) {
                if ( poly.Outer.IsClockwise )
                    writer.AddPolygon(poly.Outer, outer_cw);
                else
                    writer.AddPolygon(poly.Outer, outer_ccw);
                foreach (var hole in poly.Holes)
                    writer.AddPolygon(hole, inner);
            }
            writer.Write(sPath);
        }


        public static void QuickWrite(DGraph2 graph, string sPath, double line_width = 1)
        {
            SVGWriter writer = new SVGWriter();
            Style style = SVGWriter.Style.Outline("black", (float)line_width);
            writer.AddGraph(graph, style);
            writer.Write(sPath);
        }

        public static void QuickWrite(List<GeneralPolygon2d> polygons1, string color1, float width1,
		                              List<GeneralPolygon2d> polygons2, string color2, float width2,
		                              string sPath)
		{
			SVGWriter writer = new SVGWriter();
			Style style1 = SVGWriter.Style.Outline(color1, width1);
			Style style1_holes = SVGWriter.Style.Outline(color1, width1/2);
			foreach (GeneralPolygon2d poly in polygons1) {
				writer.AddPolygon(poly.Outer, style1);
				foreach (var hole in poly.Holes)
					writer.AddPolygon(hole, style1_holes);
			}
			Style style2 = SVGWriter.Style.Outline(color2, width2);
			Style style2_holes = SVGWriter.Style.Outline(color2, width2 / 2);
			foreach (GeneralPolygon2d poly in polygons2) {
				writer.AddPolygon(poly.Outer, style2);
				foreach (var hole in poly.Holes)
					writer.AddPolygon(hole, style2_holes);
			}
			writer.Write(sPath);
		}








        protected virtual Vector2d MapPt(Vector2d v)
        {
            if (FlipY) {
                return new Vector2d(v.x, Bounds.Min.y + (Bounds.Max.y - v.y));
            } else
                return v;
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
                Vector2d v = MapPt(poly[i]);
				b.Append(Math.Round(v.x,Precision)); 
				b.Append(','); 
				b.Append(Math.Round(v.y, Precision));
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
                Vector2d v = MapPt(poly[i]);
                b.Append(Math.Round(v.x, Precision));
				b.Append(',');
				b.Append(Math.Round(v.y, Precision));
				if (i < poly.VertexCount - 1)
					b.Append(' ');
			}
			b.Append("\" ");
			append_style(b, poly, ref DefaultPolylineStyle);
			b.Append(" />");

			w.WriteLine(b);
		}



        void write_graph(DGraph2 graph, StreamWriter w)
        {
            string style = get_style(graph, ref DefaultDGraphStyle);

            StringBuilder b = new StringBuilder();
            foreach ( int eid in graph.EdgeIndices()) {
                Segment2d seg = graph.GetEdgeSegment(eid);
                b.Append("<line ");
                Vector2d p0 = MapPt(seg.P0), p1 = MapPt(seg.P1);
                append_property("x1", p0.x, b, true);
                append_property("y1", p0.y, b, true);
                append_property("x2", p1.x, b, true);
                append_property("y2", p1.y, b, true);
                b.Append(style);
                b.Append(" />");
                b.AppendLine();
            }
            w.WriteLine(b);
        }


        void write_circle(Circle2d circle, StreamWriter w)
		{
			StringBuilder b = new StringBuilder();
			b.Append("<circle ");
            Vector2d c = MapPt(circle.Center);
            append_property("cx", c.x, b, true);
			append_property("cy", c.y, b, true);
			append_property("r", circle.Radius, b, true);
			append_style(b, circle, ref DefaultCircleStyle);
			b.Append(" />");
			w.WriteLine(b);
		}


        void write_arc(Arc2d arc, StreamWriter w)
        {
            StringBuilder b = new StringBuilder();
            Vector2d vStart = MapPt(arc.P0);
            Vector2d vEnd = MapPt(arc.P1);
            b.Append("<path ");
            b.Append("d=\"");

            // move to start coordinates
            b.Append("M");
            b.Append(Math.Round(vStart.x, Precision));
            b.Append(",");
            b.Append(Math.Round(vStart.y, Precision));
            b.Append(" ");

            // start arc
            b.Append("A");

            // radii (write twice because this is actually elliptical arc)
            b.Append(Math.Round(arc.Radius, Precision));
            b.Append(",");
            b.Append(Math.Round(arc.Radius, Precision));
            b.Append(" ");

            b.Append("0 ");     // x-axis-rotation

            int large = (arc.AngleEndDeg - arc.AngleStartDeg) > 180 ? 1 : 0;
            int sweep = (arc.IsReversed) ? 1 : 0;
            b.Append(large);
            b.Append(",");
            b.Append(sweep);

            // end coordinates
            b.Append(Math.Round(vEnd.x, Precision));
            b.Append(",");
            b.Append(Math.Round(vEnd.y, Precision));

            b.Append("\" ");     // close path

            append_style(b, arc, ref DefaultArcStyle);


            b.Append(" />");
            w.WriteLine(b);
        }


        void write_line(Segment2dBox segbox, StreamWriter w)
		{
			Segment2d seg = (Segment2d)segbox;
			StringBuilder b = new StringBuilder();
			b.Append("<line ");
			Vector2d p0 = MapPt(seg.P0), p1 = MapPt(seg.P1);
			append_property("x1", p0.x, b, true);
			append_property("y1", p0.y, b, true);
			append_property("x2", p1.x, b, true);
			append_property("y2", p1.y, b, true);
			append_style(b, segbox, ref DefaultLineStyle);
			b.Append(" />");
			w.WriteLine(b);
		}




        void write_complex(PlanarComplex complex, StreamWriter w)
        {
            foreach ( var elem in complex.ElementsItr() ) {
                List<IParametricCurve2d> curves = CurveUtils2.Flatten(elem.source);
                foreach ( IParametricCurve2d c in curves ) {
                    if (c is Segment2d)
                        write_line(new Segment2dBox((Segment2d)c), w);
                    else if (c is Circle2d)
                        write_circle(c as Circle2d, w);
                    else if (c is Polygon2DCurve)
                        write_polygon((c as Polygon2DCurve).Polygon, w);
                    else if (c is PolyLine2DCurve)
                        write_polyline((c as PolyLine2DCurve).Polyline, w);
                    else if (c is Arc2d)
                        write_arc(c as Arc2d, w);
                }
            }
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
        string get_style(object o, ref Style defaultStyle) {
            Style style;
            if (Styles.TryGetValue(o, out style) == false)
                style = defaultStyle;
            return "style=\"" + style.ToString() + "\"";
        }



    }
}
