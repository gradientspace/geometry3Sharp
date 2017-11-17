using System;
using System.Collections.Generic;
using System.IO;

namespace g3
{
    public interface ITransform2
    {
        Vector2d TransformP(Vector2d p);
        Vector2d TransformN(Vector2d n);
        double TransformScalar(double s);
    }



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
    public class TransformSequence2 : ITransform2
    {
        enum XFormType
        {
            Translation = 0,
            Rotation = 1,
            RotateAroundPoint = 2,
            Scale = 3,
            ScaleAroundPoint = 4,

            NestedITransform2 = 10
        }

        struct XForm
        {
            public XFormType type;
            public Vector2dTuple2 data;
            public object xform;

            // may need to update these to handle other types...
            public Vector2d Translation {
                get { return data.V0; }
            }
            public Vector2d Scale {
                get { return data.V0; }
            }
            public Matrix2d Rotation {
                get { return new Matrix2d(data.V0.x); }
            }
            public Vector2d RotateOrigin {
                get { return data.V1; }
            }

            public bool ScaleIsUniform {
                get { return data.V0.EpsilonEqual(data.V1, MathUtil.Epsilonf); }
            }

            public ITransform2 NestedITransform2 {
                get { return xform as ITransform2; }
            }
        }

        List<XForm> Operations;



        public TransformSequence2()
        {
            Operations = new List<XForm>();
        }



        public TransformSequence2 Translation(Vector2d dv)
        {
            Operations.Add(new XForm() {
                type = XFormType.Translation,
                data = new Vector2dTuple2(dv, Vector2d.Zero)
            });
            return this;
        }
        public TransformSequence2 Translation(double dx, double dy)
        {
            Operations.Add(new XForm() {
                type = XFormType.Translation,
                data = new Vector2dTuple2(new Vector2d(dx,dy), Vector2d.Zero)
            });
            return this;
        }

        public TransformSequence2 RotationRad(double angle)
        {
            Operations.Add(new XForm() {
                type = XFormType.Rotation,
                data = new Vector2dTuple2(new Vector2d(angle, 0), Vector2d.Zero)
            });
            return this;
        }
        public TransformSequence2 RotationDeg(double angle) {
            return RotationRad(angle * MathUtil.Deg2Rad);
        }


        public TransformSequence2 RotationRad(double angle, Vector2d aroundPt)
        {
            Operations.Add(new XForm() {
                type = XFormType.RotateAroundPoint,
                data = new Vector2dTuple2(new Vector2d(angle, 0), aroundPt)
            });
            return this;
        }
        public TransformSequence2 RotationDeg(double angle, Vector2d aroundPt) {
            return RotationRad(angle * MathUtil.Deg2Rad, aroundPt);
        }

        public TransformSequence2 Scale(Vector2d s)
        {
            Operations.Add(new XForm() {
                type = XFormType.Scale,
                data = new Vector2dTuple2(s, Vector2d.Zero)
            });
            return this;
        }

        public TransformSequence2 Scale(Vector2d s, Vector2d aroundPt)
        {
            Operations.Add(new XForm() {
                type = XFormType.ScaleAroundPoint,
                data = new Vector2dTuple2(s, aroundPt)
            });
            return this;
        }

        public TransformSequence2 Append(ITransform2 t2)
        {
            Operations.Add(new XForm() {
                type = XFormType.NestedITransform2,
                xform = t2
            });
            return this;
        }



        /// <summary>
        /// Apply transforms to point
        /// </summary>
        public Vector2d TransformP(Vector2d p)
        {
            int N = Operations.Count;
            for ( int i = 0; i < N; ++i ) {
                switch ( Operations[i].type ) {
                    case XFormType.Translation:
                        p += Operations[i].Translation;
                        break;

                    case XFormType.Rotation:
                        p = Operations[i].Rotation * p;
                        break;

                    case XFormType.RotateAroundPoint:
                        p -= Operations[i].RotateOrigin;
                        p = Operations[i].Rotation * p;
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

                    case XFormType.NestedITransform2:
                        p = Operations[i].NestedITransform2.TransformP(p);
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.TransformP: unhandled type!");
                }
            }

            return p;
        }



        /// <summary>
        /// Apply transforms to normalized vector
        /// </summary>
        public Vector2d TransformN(Vector2d n)
        {
            int N = Operations.Count;
            for (int i = 0; i < N; ++i) {
                switch (Operations[i].type) {
                    case XFormType.Translation:
                        break;

                    case XFormType.Rotation:
                        n= Operations[i].Rotation * n;
                        break;

                    case XFormType.RotateAroundPoint:
                        n = Operations[i].Rotation * n;
                        break;

                    case XFormType.Scale:
                        Util.gDevAssert(Operations[i].ScaleIsUniform);
                        n *= Operations[i].Scale;
                        break;

                    case XFormType.ScaleAroundPoint:
                        Util.gDevAssert(Operations[i].ScaleIsUniform);
                        n *= Operations[i].Scale;
                        break;

                    case XFormType.NestedITransform2:
                        n = Operations[i].NestedITransform2.TransformN(n);
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.TransformN: unhandled type!");
                }
            }

            return n;
        }





        /// <summary>
        /// Apply transforms to scalar dimension
        /// </summary>
        public double TransformScalar(double s)
        {
            int N = Operations.Count;
            for (int i = 0; i < N; ++i) {
                switch (Operations[i].type) {
                    case XFormType.Translation:
                        break;

                    case XFormType.Rotation:
                        break;

                    case XFormType.RotateAroundPoint:
                        break;

                    case XFormType.Scale:
                        Util.gDevAssert(Operations[i].ScaleIsUniform);
                        s *= Operations[i].Scale.x;
                        break;

                    case XFormType.ScaleAroundPoint:
                        Util.gDevAssert(Operations[i].ScaleIsUniform);
                        s *= Operations[i].Scale.x;
                        break;

                    case XFormType.NestedITransform2:
                        s = Operations[i].NestedITransform2.TransformScalar(s);
                        break;

                    default:
                        throw new NotImplementedException("TransformSequence.TransformScalar: unhandled type!");
                }
            }

            return s;
        }





        // [RMS] need to store nested sequence?
        //public void Store(BinaryWriter writer)
        //{
        //    writer.Write((int)1);      // version number

        //    writer.Write((int)Operations.Count);
        //    for ( int i = 0; i < Operations.Count; ++i ) {
        //        writer.Write((int)Operations[i].type);
        //        gSerialization.Store(Operations[i].data.V0, writer);
        //        gSerialization.Store(Operations[i].data.V1, writer);
        //    }
        //}

        //public void Restore(BinaryReader reader)
        //{
        //    int version = reader.ReadInt32();
        //    if (version != 1)
        //        throw new Exception("TransformSequence.Restore: unknown version number!");

        //    int N = reader.ReadInt32();
        //    Operations = new List<XForm>();
        //    for ( int i = 0; i < N; ++i ) {
        //        int type = reader.ReadInt32();
        //        XForm x = new XForm() { type = (XFormType)type };
        //        gSerialization.Restore(ref x.data.V0, reader);
        //        gSerialization.Restore(ref x.data.V1, reader);
        //        Operations.Add(x);
        //    }
        //}



    }
}
