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
            ScaleAroundPoint = 4,
            ToFrame = 5,
            FromFrame = 6
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
            public Frame3f Frame {
                get { return new Frame3f((Vector3f)RotateOrigin, Quaternion); }
            }
        }

        List<XForm> Operations;



        public TransformSequence()
        {
            Operations = new List<XForm>();
        }

        public TransformSequence(TransformSequence copy)
        {
            Operations = new List<XForm>(copy.Operations);
        }



        public void Append(TransformSequence sequence)
        {
            Operations.AddRange(sequence.Operations);
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
                type = XFormType.QuaternionRotateAroundPoint,
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

        public void AppendToFrame(Frame3f frame)
        {
            Quaternionf q = frame.Rotation; 
            Operations.Add(new XForm() {
                type = XFormType.ToFrame,
                data = new Vector3dTuple3(new Vector3d(q.x, q.y, q.z), new Vector3d(q.w, 0, 0), frame.Origin)
            });
        }

        public void AppendFromFrame(Frame3f frame)
        {
            Quaternionf q = frame.Rotation;
            Operations.Add(new XForm() {
                type = XFormType.FromFrame,
                data = new Vector3dTuple3(new Vector3d(q.x, q.y, q.z), new Vector3d(q.w, 0, 0), frame.Origin)
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

                    case XFormType.ToFrame:
                        p = Operations[i].Frame.ToFrameP(ref p);
                        break;

                    case XFormType.FromFrame:
                        p = Operations[i].Frame.FromFrameP(ref p);
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.TransformP: unhandled type!");
                }
            }

            return p;
        }




        /// <summary>
        /// Apply transforms to vector. Includes scaling.
        /// </summary>
        public Vector3d TransformV(Vector3d v)
        {
            int N = Operations.Count;
            for (int i = 0; i < N; ++i) {
                switch (Operations[i].type) {
                    case XFormType.Translation:
                        break;

                    case XFormType.QuaternionRotateAroundPoint:
                    case XFormType.QuaterionRotation:
                        v = Operations[i].Quaternion * v;
                        break;

                    case XFormType.ScaleAroundPoint:
                    case XFormType.Scale:
                        v *= Operations[i].Scale;
                        break;

                    case XFormType.ToFrame:
                        v = Operations[i].Frame.ToFrameV(ref v);
                        break;

                    case XFormType.FromFrame:
                        v = Operations[i].Frame.FromFrameV(ref v);
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.TransformV: unhandled type!");
                }
            }

            return v;
        }




        /// <summary>
        /// Apply transforms to point
        /// </summary>
        public Vector3f TransformP(Vector3f p) {
            return (Vector3f)TransformP((Vector3d)p);
        }


        /// <summary>
        /// construct inverse transformation sequence
        /// </summary>
        public TransformSequence MakeInverse()
        {
            TransformSequence reverse = new TransformSequence();
            int N = Operations.Count;
            for (int i = N-1; i >= 0; --i) {
                switch (Operations[i].type) {
                    case XFormType.Translation:
                        reverse.AppendTranslation(-Operations[i].Translation);
                        break;

                    case XFormType.QuaterionRotation:
                        reverse.AppendRotation(Operations[i].Quaternion.Inverse());
                        break;

                    case XFormType.QuaternionRotateAroundPoint:
                        reverse.AppendRotation(Operations[i].Quaternion.Inverse(), Operations[i].RotateOrigin);
                        break;

                    case XFormType.Scale:
                        reverse.AppendScale(1.0 / Operations[i].Scale);
                        break;

                    case XFormType.ScaleAroundPoint:
                        reverse.AppendScale(1.0 / Operations[i].Scale, Operations[i].RotateOrigin);
                        break;

                    case XFormType.ToFrame:
                        reverse.AppendFromFrame(Operations[i].Frame);
                        break;

                    case XFormType.FromFrame:
                        reverse.AppendToFrame(Operations[i].Frame);
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.MakeInverse: unhandled type!");
                }
            }
            return reverse;
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
