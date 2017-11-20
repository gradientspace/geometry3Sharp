using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // radially-symmetric 3D arrow
    public class Radial3DArrowGenerator : VerticalGeneralizedCylinderGenerator
    {
        public float StickRadius = 0.5f;
        public float StickLength = 1.0f;
        public float HeadBaseRadius = 1.0f;
        public float TipRadius = 0.0f;
        public float HeadLength = 0.5f;

        override public MeshGenerator Generate()
        {
            Sections = new CircularSection[4];
            Sections[0] = new CircularSection(StickRadius, 0.0f);
            Sections[1] = new CircularSection(StickRadius, StickLength);
            Sections[2] = new CircularSection(HeadBaseRadius, StickLength);
            Sections[3] = new CircularSection(TipRadius, StickLength+HeadLength);

            Capped = true;
            NoSharedVertices = true;
            base.Generate();

            return this;
        }

    }






}
