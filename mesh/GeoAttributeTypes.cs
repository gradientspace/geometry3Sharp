// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;


#nullable enable

namespace g3
{
    /// <summary>
    /// base type for fixed-count attributes, eg a per-triangle attribute with one Element per triangle
    /// </summary>
    public interface IGeoAttribute
    {
        public enum EAttribType
        {
            Unspecified = 0,
            Triangle = 1,
            Vertex = 2
        }


        string Name { get; }
        EAttribType AttribType { get; }

        Type ElementType { get; }
        int NumElements { get; }
        
        void Initialize(int Count);
        void InitializeToCopy(int NewCount, IGeoAttribute SourceAttrib, Func<int,int> IndexMapF);
        void TrimToLength(int NewCount);

        void InsertValue_Copy(int Index, int CopyIndex = -1);
    }


    public interface ILinearGeoAttribute
    {
        void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo);
        void UpdateOnSplit(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo);
        void UpdateOnReverseTriOrientation(int TriangleID);
    }


    /// <summary>
    /// typed extensions to IGeoAttribute. Type must be a struct.
    /// </summary>
    public interface IGeoAttribute<T> : IGeoAttribute where T : struct
    {
        // todo: T-valued arguments for Initialize/UpdateCount complicate a lot of things...maybe could do without?

        T DefaultValue { get; }

        void Initialize(int Count, T InitialValue = default)
        {
            UpdateCount(Count, true, InitialValue);
        }

        void UpdateCount(int NewCount, bool bInitializeNew = false, T InitValue = default);
        void InsertValue(int Index, T NewValue);
        void SetValue(int Index, T NewValue);
        T GetValue(int Index);
    }

    /// <summary>
    /// base type for a DVector-backed geo attribute
    /// </summary>
    public abstract class BaseGeoAttribute<T> : IGeoAttribute<T> where T : struct
    {
        public DVector<T> data = new DVector<T>();

        public Type ElementType { get { return typeof(T); } }
        public int NumElements { get { return data.size; } }
        public T DefaultValue { get { return default(T); } }

        public string Name { get; private set; } = "UNNAMED";
        public IGeoAttribute.EAttribType AttribType { get; private set; } = IGeoAttribute.EAttribType.Unspecified;

        protected virtual int get_element_count(DMesh3 parentMesh)
        {
            if (AttribType == IGeoAttribute.EAttribType.Triangle)
                return parentMesh.MaxTriangleID;
            else if (AttribType == IGeoAttribute.EAttribType.Vertex)
                return parentMesh.MaxVertexID;
            else
                return 0;
        }

        public BaseGeoAttribute(string name, DMesh3? parentMesh = null, IGeoAttribute.EAttribType attribType = IGeoAttribute.EAttribType.Unspecified) 
        { 
            Name = name;
            AttribType = attribType;
            if (parentMesh != null)
                Initialize(get_element_count(parentMesh));
        }
        public BaseGeoAttribute(string name, T InitialValue, DMesh3? parentMesh = null, IGeoAttribute.EAttribType attribType = IGeoAttribute.EAttribType.Unspecified)
        {
            Name = name;
            AttribType = attribType;
            if (parentMesh != null)
                Initialize(get_element_count(parentMesh), InitialValue);
        }

        public void Initialize(int Count)
        {
            UpdateCount(Count, false);
        }
        public void Initialize(int Count, T InitialValue = default)
        {
            UpdateCount(Count, true, InitialValue);
        }
        public void InitializeToCopy(int NewCount, IGeoAttribute SourceAttrib, Func<int, int>? MapNewToOldF)
        {
            if (SourceAttrib is BaseGeoAttribute<T> TypedSource) 
            {
                if (MapNewToOldF == null) {
                    Util.gDevAssert(NewCount == SourceAttrib.NumElements);
                    data.copy(TypedSource.data);
                    return;
                }

                int SourceCount = TypedSource.NumElements;
                if (SourceCount == NewCount) {
                    data.copy(TypedSource.data);
                } else {
                    UpdateCount(NewCount, false);
                    for (int i = 0; i < NewCount; ++i)
                        SetValue(i, TypedSource.GetValue(MapNewToOldF(i)));
                }
            } else
                throw new NotImplementedException();
        }

        public void TrimToLength(int NewCount)
        {
            Util.gDevAssert(NewCount <= NumElements);
            data.resize(NewCount);
        }

        public void UpdateCount(int NewCount, bool bInitializeNew = false, T InitValue = default)
        {
            int prev_size = data.size;
            data.resize(NewCount);
            if (bInitializeNew) {
                while (prev_size != data.size) {
                    data[prev_size] = InitValue;
                    prev_size++;
                }
            }
        }
        public void InsertValue(int Index, T NewValue)
        {
            data.insert(NewValue, Index);
        }
        public void InsertValue_Copy(int Index, int CopyIndex = -1)
        {
            if (CopyIndex >= 0 && CopyIndex < data.Length)
                data.insert(data[CopyIndex], Index);
            else
                data.insert(DefaultValue, Index);
        }
        public void SetValue(int Index, T NewValue)
        {
            data[Index] = NewValue;
        }
        public T GetValue(int Index)
        {
            return data[Index];
        }
    }


    public class IntGeoAttribute : BaseGeoAttribute<int>
    {
        public IntGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }

    public class Vector2fGeoAttribute : BaseGeoAttribute<Vector2f>
    {
        public Vector2fGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }

    public class Vector3fGeoAttribute : BaseGeoAttribute<Vector3f>
    {
        public Vector3fGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }




    public abstract class BaseBaryVertGeoAttribute<T> : BaseGeoAttribute<T>, ILinearGeoAttribute where T : struct
    {
        public BaseBaryVertGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }
        public BaseBaryVertGeoAttribute(string Name, T initialValue, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, initialValue, parentMesh, attribType) { }

        public virtual void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo) {
            UpdateOnPoke_Impl(pokeInfo);
        }
        public virtual void UpdateOnSplit(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo) {
            UpdateOnSplit_Impl(Mesh, splitInfo);
        }
        public virtual void UpdateOnReverseTriOrientation(int TriangleID) { }

        public abstract T GetBaryValue(ref T A, ref T B, ref T C, Vector3d baryCoords);
        public abstract T GetInterpValue(ref T A, ref T B, double interp_t);

        public void UpdateOnPoke_Impl(DMesh3.PokeTriangleInfo pokeInfo)
        {
            T a = GetValue(pokeInfo.orig_tri.a);
            T b = GetValue(pokeInfo.orig_tri.b);
            T c = GetValue(pokeInfo.orig_tri.c);
            T newValue = GetBaryValue(ref a, ref b, ref c, pokeInfo.new_vid_barycoords);
            InsertValue(pokeInfo.new_vid, newValue);
        }
        public void UpdateOnSplit_Impl(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo)
        {
            T a = GetValue(splitInfo.eOrigAB.a);
            T b = GetValue(splitInfo.eOrigAB.b);
            T newValue = GetInterpValue(ref a, ref b, splitInfo.split_t);
            InsertValue(splitInfo.vNew, newValue);
        }
    }


    public class VertexColorsGeoAttribute : BaseBaryVertGeoAttribute<Vector3f>
    {
        public VertexColorsGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }
        public VertexColorsGeoAttribute(string Name, Vector3f initialValue, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, initialValue, parentMesh, attribType) { }

        public override Vector3f GetInterpValue(ref Vector3f A, ref Vector3f B, double interp_t) {
            return (Vector3f)Vector3d.Lerp(A, B, interp_t);
        }
        public override Vector3f GetBaryValue(ref Vector3f A, ref Vector3f B, ref Vector3f C, Vector3d baryCoords) {
            return (Vector3f)(baryCoords.x * (Vector3d)A + baryCoords.y * (Vector3d)B + baryCoords.z * (Vector3d)C); 
        }
        public Vector3f GetBaryValue(int ai, int bi, int ci, Vector3d baryCoords) {
            return (Vector3f)(baryCoords.x * (Vector3d)GetValue(ai) + baryCoords.y * (Vector3d)GetValue(bi) + baryCoords.z * (Vector3d)GetValue(ci));
        }
        public DVector<float> ToBuffer()
        {
            DVector<float> buffer = new DVector<float>(); buffer.resize(NumElements*3);
            for ( int i = 0; i < NumElements; ++i ) {
                Vector3f v = GetValue(i);
                buffer[3*i] = v.x; buffer[3*i+1] = v.y; buffer[3*i+2] = v.z;
            }
            return buffer;
        }
        public void SetFromBuffer(DVector<float> buffer)
        {
            int N = buffer.Length/3;
            UpdateCount(N);
            for (int i = 0; i < N; ++i)
                SetValue(i, new Vector3f(buffer[3*i], buffer[3*i+1], buffer[3*i+2]));
        }
    }


    // attributes that have their value interpolated during mesh refinement edits

    public abstract class BaseBaryTriGeoAttribute<T, T2> : BaseGeoAttribute<T>, ILinearGeoAttribute where T : struct where T2 : struct
    {
        public BaseBaryTriGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }

        // could we write generic versions of these functions? 
        public virtual void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo) {
            UpdateOnPoke_Impl(pokeInfo);
        }
        public virtual void UpdateOnSplit(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo) {
            //throw new NotImplementedException(); 
            UpdateOnSplit_Impl(Mesh, splitInfo);
        }
        public virtual void UpdateOnReverseTriOrientation(int TriangleID)
        {
            UpdateOnReverseTriOrientation_Impl(TriangleID);
        }

        // wrappers we need to allow generic code below to interact with T and T2 types
        public abstract T MakeNewValue(T2 a, T2 b, T2 c);
        public abstract T2 GetBaryValue(ref T tuple, Vector3d baryCoords);
        public abstract T2 MakeLerpValue(T2 a, T2 b, float alpha);
        public abstract T2 GetValueElem(ref T tuple, int index);
        public abstract void SetValueElem(ref T tuple, int index, T2 newValue);

        // generic implementations of tri update functions
        // this avoids having to duplicate the code in each subclass
        // but it will have some overhead...might be better to actually copy-paste (should only be 3 cases - vector2f/3f/4f)

        public virtual void UpdateOnReverseTriOrientation_Impl(int TriangleID)
        {
            // set_triangle(tID, t[1], t[0], t[2]);
            T curVals = GetValue(TriangleID);
            T newVals = MakeNewValue( GetValueElem(ref curVals, 1), GetValueElem(ref curVals, 0), GetValueElem(ref curVals, 2) );
            SetValue(TriangleID, newVals);
        }

        public void UpdateOnPoke_Impl(DMesh3.PokeTriangleInfo pokeInfo)
        {
            T orig = GetValue(pokeInfo.orig_t0);
            T2 centerUV = GetBaryValue(ref orig, pokeInfo.new_vid_barycoords);
            T newt0 = orig;
            SetValueElem(ref newt0, 2, centerUV);
            T2 A = GetValueElem(ref orig, 0);
            T2 B = GetValueElem(ref orig, 1);
            T2 C = GetValueElem(ref orig, 2);
            SetValue(pokeInfo.orig_t0, MakeNewValue( A, B, centerUV) );
            InsertValue(pokeInfo.new_t1, MakeNewValue(B, C, centerUV));
            InsertValue(pokeInfo.new_t2, MakeNewValue(C, A, centerUV));
        }

        public void UpdateOnSplit_Impl(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo)
        {
            int f = splitInfo.vNew;

            T orig0 = GetValue(splitInfo.eOrigT0);
            Index3i curT0 = Mesh.GetTriangle(splitInfo.eOrigT0);
            Index3i newFBC = Mesh.GetTriangle(splitInfo.eNewT2);
            int fb_Index = curT0.IndexOf(f);
            int c = newFBC.c; int c_Index = curT0.IndexOf(c);
            int a_Index = Index3i.OtherIndex(fb_Index, c_Index);
            // (validate a/b order) // int b = newFBC.b; Util.gDevAssert(b == splitInfo.eOrigAB.b && curT0[a_Index] == splitInfo.eOrigAB.a);
            T2 a_Pos = GetValueElem(ref orig0, a_Index);
            T2 b_Pos = GetValueElem(ref orig0, fb_Index);
            T2 c_Pos = GetValueElem(ref orig0, c_Index);
            T2 f_Pos = MakeLerpValue(a_Pos, b_Pos, (float)splitInfo.split_t);     // is this always right order?
            SetValueElem(ref orig0, fb_Index, f_Pos);
            SetValue(splitInfo.eOrigT0, orig0);
            InsertValue(splitInfo.eNewT2, MakeNewValue(f_Pos, b_Pos, c_Pos)); // (f,b,c)

            // note this is exactly the same as above, only with T1/T3 and some slight index permutations...
            // (have to recompute a/b because tris may not be connected in UV!)
            if (splitInfo.eOrigT1 >= 0) {       // tri /a/b/d becomes a/f/d
                T orig1 = GetValue(splitInfo.eOrigT1);
                Index3i curT1 = Mesh.GetTriangle(splitInfo.eOrigT1);
                Index3i NewFDB = Mesh.GetTriangle(splitInfo.eNewT3);
                fb_Index = curT1.IndexOf(f);
                int d = NewFDB.b; int d_index = curT1.IndexOf(d);
                a_Index = Index3i.OtherIndex(fb_Index, d_index);
                // (validate a/b order) // b = NewFDB.c; Util.gDevAssert(b == splitInfo.eOrigAB.b && curT1[a_Index] == splitInfo.eOrigAB.a);
                a_Pos = GetValueElem(ref orig1, a_Index);
                b_Pos = GetValueElem(ref orig1, fb_Index);
                T2 d_Pos = GetValueElem(ref orig1, d_index);
                f_Pos = MakeLerpValue(a_Pos, b_Pos, (float)splitInfo.split_t);
                SetValueElem(ref orig1, fb_Index, f_Pos);
                SetValue(splitInfo.eOrigT1, orig1);
                InsertValue(splitInfo.eNewT3, MakeNewValue(f_Pos, d_Pos, b_Pos)); // (f,d,b)
            }
        }

    }


    public struct TriUVs
    {
        public Vector2f A, B, C;
        public TriUVs() { }
        public TriUVs(Vector2f a, Vector2f b, Vector2f c) { A = a; B = b; C = c; }
        public Vector2f this[int key] {
            get { return (key == 0) ? A : (key == 1) ? B : C; }
            set { if (key == 0) A = value; else if (key == 1) B = value; else C = value; }
        }
        public Vector2f BaryPoint(Vector3d baryCoords) { return (Vector2f)(baryCoords.x * (Vector2d)A + baryCoords.y * (Vector2d)B + baryCoords.z * (Vector2d)C); }
    }
    public class TriUVsGeoAttribute : BaseBaryTriGeoAttribute<TriUVs, Vector2f>
    {
        public TriUVsGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }

        //public override void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo) {
        //    TriUVs orig = GetValue(pokeInfo.orig_t0);
        //    Vector2f centerUV = orig.BaryPoint(pokeInfo.new_vid_barycoords);
        //    TriUVs newt0 = orig; newt0.C = centerUV;
        //    SetValue(pokeInfo.orig_t0, new(orig.A, orig.B, centerUV));
        //    InsertValue(pokeInfo.new_t1, new(orig.B, orig.C, centerUV));
        //    InsertValue(pokeInfo.new_t2, new(orig.C, orig.A, centerUV));
        //}

        public override Vector2f GetBaryValue(ref TriUVs tuple, Vector3d baryCoords) { return tuple.BaryPoint(baryCoords); }
        public override TriUVs MakeNewValue(Vector2f a, Vector2f b, Vector2f c) { return new TriUVs(a, b, c); }
        public override Vector2f MakeLerpValue(Vector2f a, Vector2f b, float alpha) { return Vector2f.Lerp(a, b, alpha); }
        public override Vector2f GetValueElem(ref TriUVs tuple, int index) { return tuple[index]; }
        public override void SetValueElem(ref TriUVs tuple, int index, Vector2f newValue) { tuple[index] = newValue; }
    }

    public struct TriNormals
    {
        public Vector3f A, B, C;
        public TriNormals() { }
        public TriNormals(Vector3f a, Vector3f b, Vector3f c) { A = a; B = b; C = c; }
        public Vector3f this[int key] {
            get { return (key == 0) ? A : (key == 1) ? B : C; }
            set { if (key == 0) A = value; else if (key == 1) B = value; else C = value; }
        }
        public Vector3f BaryPoint(Vector3d baryCoords) { return (Vector3f)(baryCoords.x * (Vector3d)A + baryCoords.y * (Vector3d)B + baryCoords.z * (Vector3d)C); }
    }
    public class TriNormalsGeoAttribute : BaseBaryTriGeoAttribute<TriNormals, Vector3f>
    {
        public TriNormalsGeoAttribute(string Name, DMesh3? parentMesh, IGeoAttribute.EAttribType attribType) : base(Name, parentMesh, attribType) { }

        public override Vector3f GetBaryValue(ref TriNormals tuple, Vector3d baryCoords) { return tuple.BaryPoint(baryCoords); }
        public override TriNormals MakeNewValue(Vector3f a, Vector3f b, Vector3f c) { return new TriNormals(a, b, c); }
        public override Vector3f MakeLerpValue(Vector3f a, Vector3f b, float alpha) { return Vector3f.Lerp(a, b, alpha); }
        public override Vector3f GetValueElem(ref TriNormals tuple, int index) { return tuple[index]; }
        public override void SetValueElem(ref TriNormals tuple, int index, Vector3f newValue) { tuple[index] = newValue; }

        public override void UpdateOnReverseTriOrientation_Impl(int TriangleID)
        {
            // set_triangle(tID, t[1], t[0], t[2]);
            TriNormals normals = GetValue(TriangleID);
            normals = new TriNormals(-normals[1], -normals[0], -normals[2]);
            SetValue(TriangleID, normals);
        }

        //public override void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo)
        //{
        //    TriNormals orig = GetValue(pokeInfo.orig_t0);
        //    Vector3f centerUV = orig.BaryPoint(pokeInfo.new_vid_barycoords);
        //    TriNormals newt0 = orig; newt0.C = centerUV;
        //    SetValue(pokeInfo.orig_t0, new(orig.A, orig.B, centerUV));
        //    InsertValue(pokeInfo.new_t1, new(orig.B, orig.C, centerUV));
        //    InsertValue(pokeInfo.new_t2, new(orig.C, orig.A, centerUV));
        //}

    }


#if SOME_SYMBOL
    // attribute concept w/ single-value optimization....possibly delete later?

    public class DIntAttribute
    {
        DVector<int>? data = null;
        int NumElements = 0;
        int ConstantValue = 0;
        bool bInflated = false;

        private void inflate(bool bInitialize)
        {
            if (bInflated == false) {
                data = new DVector<int>();
                data.resize(NumElements);
                if (bInitialize) {
                    for (int i = 0; i < NumElements; i++)
                        data[i] = ConstantValue;
                }
                bInflated = true;
            }
        }

        public void Initialize(int Count, int DefaultValue = 0)
        {
            data = null;
            NumElements = Count;
            ConstantValue = DefaultValue;
            bInflated = false;
        }

        public void UpdateCount(int NewCount, bool bInitializeNew = false, int InitValue = 0)
        {
            if (bInflated == false) 
            {
                if (bInitializeNew && InitValue != ConstantValue)
                    inflate(true);
            }

            int prev_size = data!.size;
            data.resize(NewCount);
            if (bInitializeNew) {
                while (prev_size != data.size) {
                    data[prev_size] = InitValue;
                    prev_size++;
                }
            }
        }

        public void InsertValue(int Index, int NewValue)
        {
            if (bInflated == false) {
                if (NewValue == ConstantValue) {
                    NumElements = Math.Max(NumElements, Index+1);
                    return;
                } else
                    inflate(true);
            }
            data!.insert(NewValue, Index);
        }

        public void SetValue(int Index, int NewValue)
        {
            if (bInflated == false) {
                if (NewValue == ConstantValue)
                    return;
                inflate(true);  // else 
            }
            data![Index] = NewValue;
        }

        public int GetValue(int Index)
        {
            if (bInflated == false)
                return ConstantValue;
            return data![Index];
        }
    }
#endif



}
