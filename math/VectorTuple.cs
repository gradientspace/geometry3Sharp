using System;

namespace g3
{
    // These are convenience classes used in place of local stack arrays
    // (which C# does not support, but is common in C++ code)


    public struct Vector3dTuple2
    {
        public Vector3d V0, V1;

        public Vector3dTuple2(Vector3d v0, Vector3d v1)
        {
            V0 = v0; V1 = v1;
        }

        public Vector3d this[int key] {
            get { return (key == 0) ? V0 : V1; }
            set { if (key == 0) V0 = value; else V1 = value; }
        }
    }


    public struct Vector3dTuple3
    {
        public Vector3d V0, V1, V2;

        public Vector3dTuple3(Vector3d v0, Vector3d v1, Vector3d v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public Vector3d this[int key]
        {
            get { return (key == 0) ? V0 : (key == 1) ? V1 : V2; }
            set { if (key == 0) V0 = value; else if (key == 1) V1 = value; else V2 = value; }
        }
    }



    public struct Vector3fTuple3
    {
        public Vector3f V0, V1, V2;

        public Vector3fTuple3(Vector3f v0, Vector3f v1, Vector3f v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public Vector3f this[int key]
        {
            get { return (key == 0) ? V0 : (key == 1) ? V1 : V2; }
            set { if (key == 0) V0 = value; else if (key == 1) V1 = value; else V2 = value; }
        }
    }




    public struct Vector2dTuple2
    {
        public Vector2d V0, V1;

        public Vector2dTuple2(Vector2d v0, Vector2d v1)
        {
            V0 = v0; V1 = v1;
        }

        public Vector2d this[int key]
        {
            get { return (key == 0) ? V0 : V1; }
            set { if (key == 0) V0 = value; else V1 = value; }
        }
    }


    public struct Vector2dTuple3
    {
        public Vector2d V0, V1, V2;

        public Vector2dTuple3(Vector2d v0, Vector2d v1, Vector2d v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public Vector2d this[int key]
        {
            get { return (key == 0) ? V0 : (key == 1) ? V1 : V2; }
            set { if (key == 0) V0 = value; else if (key == 1) V1 = value; else V2 = value; }
        }
    }


    public struct Vector2dTuple4
    {
        public Vector2d V0, V1, V2, V3;

        public Vector2dTuple4(Vector2d v0, Vector2d v1, Vector2d v2, Vector2d v3)
        {
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
        }

        public Vector2d this[int key] {
            get {
                return (key > 1) ?
                  ((key == 2) ? V2 : V3) :
                  ((key == 1) ? V1 : V0);
            }
            set {
                if ( key > 1 ) {
                    if (key == 2) V2 = value;
                    else V3 = value;
                } else {
                    if (key == 1) V0 = value;
                    else V1 = value;
                }
            }
        }
    }


}
