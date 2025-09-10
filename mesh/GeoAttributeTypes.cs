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

    public struct TriUVs
    {
        public Vector2f A, B, C;
    }
    public class TriUVsGeoAttribute : BaseGeoAttribute<TriUVs>
    {
        public TriUVsGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }
    }

    public struct TriNormals
    {
        public Vector3f A, B, C;
    }
    public class TriNormalsGeoAttribute : BaseGeoAttribute<TriNormals>
    {
        public TriNormalsGeoAttribute(string Name, DMesh3? parentMesh, EAttribType attribType) : base(Name, parentMesh, attribType) { }
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
