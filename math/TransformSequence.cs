using System;
using System.Collections.Generic;
using System.IO;

namespace g3
{
    /// <summary>
    /// TransformSequence stores an ordered list of basic transformations.
    /// This can be useful if you need to construct some modifications and want
    /// to use the same set later. For example, if you have a hierarchy of objects
    /// with relative transformations and want to "save" the nested transform sequence
    /// without having to hold references to the original objects.
    /// 
    /// Use the Append() functions to add different transform types, and the TransformX()
    /// to apply the sequence
    /// </summary>
    public class TransformSequence
    {
        enum XFormType
        {
            Translation = 0,
            QuaterionRotation = 1,
            QuaternionRotateAroundPoint = 2,
            Scale = 3,
            ScaleAroundPoint = 4
        }

        struct XForm
        {
            public XFormType type;
            public Vector3dTuple3 data;

            // may need to update these to handle other types...
            public Vector3d Translation {
                get { return data.V0; }
            }
            public Vector3d Scale {
                get { return data.V0; }
            }
            public Quaternionf Quaternion {
                get { return new Quaternionf((float)data.V0.x, (float)data.V0.y, (float)data.V0.z, (float)data.V1.x); }
            }
            public Vector3d RotateOrigin {
                get { return data.V2; }
            }
        }

        List<XForm> Operations;



        public TransformSequence()
        {
            Operations = new List<XForm>();
        }



        public void AppendTranslation(Vector3d dv)
        {
            Operations.Add(new XForm() {
                type = XFormType.Translation,
                data = new Vector3dTuple3(dv, Vector3d.Zero, Vector3d.Zero)
            });
        }
        public void AppendTranslation(double dx, double dy, double dz)
        {
            Operations.Add(new XForm() {
                type = XFormType.Translation,
                data = new Vector3dTuple3(new Vector3d(dx,dy,dz), Vector3d.Zero, Vector3d.Zero)
            });
        }

        public void AppendRotation(Quaternionf q)
        {
            Operations.Add(new XForm() {
                type = XFormType.QuaterionRotation,
                data = new Vector3dTuple3(new Vector3d(q.x, q.y, q.z), new Vector3d(q.w, 0, 0), Vector3d.Zero)
            });
        }

        public void AppendRotation(Quaternionf q, Vector3d aroundPt)
        {
            Operations.Add(new XForm() {
                type = XFormType.QuaterionRotation,
                data = new Vector3dTuple3(new Vector3d(q.x, q.y, q.z), new Vector3d(q.w, 0, 0), aroundPt)
            });
        }

        public void AppendScale(Vector3d s)
        {
            Operations.Add(new XForm() {
                type = XFormType.Scale,
                data = new Vector3dTuple3(s, Vector3d.Zero, Vector3d.Zero)
            });
        }

        public void AppendScale(Vector3d s, Vector3d aroundPt)
        {
            Operations.Add(new XForm() {
                type = XFormType.ScaleAroundPoint,
                data = new Vector3dTuple3(s, Vector3d.Zero, aroundPt)
            });
        }



        /// <summary>
        /// Apply transforms to point
        /// </summary>
        public Vector3d TransformP(Vector3d p)
        {
            int N = Operations.Count;
            for ( int i = 0; i < N; ++i ) {
                switch ( Operations[i].type ) {
                    case XFormType.Translation:
                        p += Operations[i].Translation;
                        break;

                    case XFormType.QuaterionRotation:
                        p = Operations[i].Quaternion * p;
                        break;

                    case XFormType.QuaternionRotateAroundPoint:
                        p -= Operations[i].RotateOrigin;
                        p = Operations[i].Quaternion * p;
                        p += Operations[i].RotateOrigin;
                        break;

                    case XFormType.Scale:
                        p *= Operations[i].Scale;
                        break;

                    case XFormType.ScaleAroundPoint:
                        p -= Operations[i].RotateOrigin;
                        p *= Operations[i].Scale;
                        p += Operations[i].RotateOrigin;
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.TransformP: unhandled type!");
                }
            }

            return p;
        }






        public void Store(BinaryWriter writer)
        {
            writer.Write((int)1);      // version number

            writer.Write((int)Operations.Count);
            for ( int i = 0; i < Operations.Count; ++i ) {
                writer.Write((int)Operations[i].type);
                gSerialization.Store(Operations[i].data.V0, writer);
                gSerialization.Store(Operations[i].data.V1, writer);
                gSerialization.Store(Operations[i].data.V2, writer);
            }
        }

        public void Restore(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            if (version != 1)
                throw new Exception("TransformSequence.Restore: unknown version number!");

            int N = reader.ReadInt32();
            Operations = new List<XForm>();
            for ( int i = 0; i < N; ++i ) {
                int type = reader.ReadInt32();
                XForm x = new XForm() { type = (XFormType)type };
                gSerialization.Restore(ref x.data.V0, reader);
                gSerialization.Restore(ref x.data.V1, reader);
                gSerialization.Restore(ref x.data.V2, reader);
                Operations.Add(x);
            }
        }



    }
}
