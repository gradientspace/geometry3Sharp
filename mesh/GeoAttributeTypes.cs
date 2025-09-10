// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using static g3.IGeoAttribute;


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
        public EAttribType AttribType { get; private set; } = EAttribType.Unspecified;

        public BaseGeoAttribute(string name, DMesh3? parentMesh = null, EAttribType attribType = EAttribType.Unspecified) 
        { 
            Name = name;
            AttribType = attribType;
            if (parentMesh != null)
                Initialize(parentMesh.TriangleCount);
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
        public IntGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }

    public class Vector2fGeoAttribute : BaseGeoAttribute<Vector2f>
    {
        public Vector2fGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }

    public class Vector3fGeoAttribute : BaseGeoAttribute<Vector3f>
    {
        public Vector3fGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }


    
    // attributes that have their value interpolated during mesh refinement edits


    public class BaseLerpableGeoAttribute<T> : BaseGeoAttribute<T>, ILinearGeoAttribute where T : struct
    {
        public BaseLerpableGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }

        // could we write generic versions of these functions? 
        public virtual void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo) { throw new NotImplementedException(); }
        public virtual void UpdateOnSplit(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo) { throw new NotImplementedException(); }
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
    public class TriUVsGeoAttribute : BaseLerpableGeoAttribute<TriUVs>
    {
        public TriUVsGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }

        public override void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo) {
            TriUVs orig = GetValue(pokeInfo.orig_t0);
            Vector2f centerUV = orig.BaryPoint(pokeInfo.new_vid_barycoords);
            TriUVs newt0 = orig; newt0.C = centerUV;
            SetValue(pokeInfo.orig_t0, new(orig.A, orig.B, centerUV));
            InsertValue(pokeInfo.new_t1, new(orig.B, orig.C, centerUV));
            InsertValue(pokeInfo.new_t2, new(orig.C, orig.A, centerUV));
        }
        public override void UpdateOnSplit(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo) 
        {
            int f = splitInfo.vNew;

            TriUVs orig0 = GetValue(splitInfo.eOrigT0);
            Index3i curT0 = Mesh.GetTriangle(splitInfo.eOrigT0);
            Index3i newFBC = Mesh.GetTriangle(splitInfo.eNewT2);
            int fb_Index = curT0.IndexOf(f);
            int c = newFBC.c; int c_Index = curT0.IndexOf(c);
            int a_Index = Index3i.OtherIndex(fb_Index, c_Index);
            // (validate a/b order) // int b = newFBC.b; Util.gDevAssert(b == splitInfo.eOrigAB.b && curT0[a_Index] == splitInfo.eOrigAB.a);
            Vector2f a_Pos = orig0[a_Index], b_Pos = orig0[fb_Index], c_Pos = orig0[c_Index];
            Vector2f f_Pos = Vector2f.Lerp(a_Pos, b_Pos, (float)splitInfo.split_t);     // is this always right order?
            orig0[fb_Index] = f_Pos;
            SetValue(splitInfo.eOrigT0, orig0);
            InsertValue(splitInfo.eNewT2, new TriUVs(f_Pos, b_Pos, c_Pos)); // (f,b,c)

            // note this is exactly the same as above, only with T1/T3
            // have to recompute everything because tris may not be connected in UV!
            if (splitInfo.eOrigT1 >= 0) {       // tri /a/b/d becomes a/f/d
                TriUVs orig1 = GetValue(splitInfo.eOrigT1);
                Index3i curT1 = Mesh.GetTriangle(splitInfo.eOrigT1);
                Index3i NewFDB = Mesh.GetTriangle(splitInfo.eNewT3);
                fb_Index = curT1.IndexOf(f);
                int d = NewFDB.b; int d_index = curT1.IndexOf(d);
                a_Index = Index3i.OtherIndex(fb_Index, d_index);
                // (validate a/b order) // b = NewFDB.c; Util.gDevAssert(b == splitInfo.eOrigAB.b && curT1[a_Index] == splitInfo.eOrigAB.a);
                a_Pos = orig1[a_Index]; b_Pos = orig1[fb_Index]; Vector2f d_Pos = orig1[d_index];
                f_Pos = Vector2f.Lerp(a_Pos, b_Pos, (float)splitInfo.split_t);
                orig1[fb_Index] = f_Pos;
                SetValue(splitInfo.eOrigT1, orig1);
                InsertValue(splitInfo.eNewT3, new TriUVs(f_Pos, d_Pos, b_Pos)); // (f,d,b)
            }
        }


    }

    public struct TriNormals
    {
        public Vector3f A, B, C;
        public TriNormals() { }
        public TriNormals(Vector3f a, Vector3f b, Vector3f c) { A = a; B = b; C = c; }
        public Vector3f BaryPoint(Vector3d baryCoords) { return (Vector3f)(baryCoords.x * (Vector3d)A + baryCoords.y * (Vector3d)B + baryCoords.z * (Vector3d)C); }

    }
    public class TriNormalsGeoAttribute : BaseLerpableGeoAttribute<TriNormals>
    {
        public TriNormalsGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }

        public override void UpdateOnPoke(DMesh3.PokeTriangleInfo pokeInfo)
        {
            TriNormals orig = GetValue(pokeInfo.orig_t0);
            Vector3f centerUV = orig.BaryPoint(pokeInfo.new_vid_barycoords);
            TriNormals newt0 = orig; newt0.C = centerUV;
            SetValue(pokeInfo.orig_t0, new(orig.A, orig.B, centerUV));
            InsertValue(pokeInfo.new_t1, new(orig.B, orig.C, centerUV));
            InsertValue(pokeInfo.new_t2, new(orig.C, orig.A, centerUV));
        }
        public override void UpdateOnSplit(DMesh3 Mesh, DMesh3.EdgeSplitInfo splitInfo)
        {
            InsertValue_Copy(splitInfo.eNewT2, splitInfo.eOrigT0);
            if (splitInfo.eOrigT1 >= 0)
                InsertValue_Copy(splitInfo.eNewT3, splitInfo.eOrigT1);
        }
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
