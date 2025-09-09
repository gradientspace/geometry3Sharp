using g3;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace g3
{
    public class DMesh3Attributes
    {
        DMesh3? parentMesh = null;
        public DMesh3? ParentMesh => parentMesh;

        public DMesh3Attributes(DMesh3 parentMesh)
        {
            this.parentMesh = parentMesh;
        }

        protected IntGeoAttribute? materialID { get; private set; } = null;
        public bool HasMaterialID => (materialID != null);
        public void EnableMaterialID() {
            if (materialID == null)
                materialID = new_tri_attrib<IntGeoAttribute>();
        }
        public IntGeoAttribute MaterialID { get { if (!HasMaterialID) EnableMaterialID(); return materialID!; } }




        public virtual IEnumerable<IGeoAttribute> TriAttributes()
        {
            if (materialID != null)
                yield return materialID;
        }


        public virtual void EnableMatching(DMesh3Attributes Other)
        {
            if (Other.HasMaterialID)
                EnableMaterialID();
        }

        public virtual void Copy(DMesh3Attributes other, int[]? mapV, int[]? mapT)
        {
            Func<int, int>? MapV = (mapV != null) ? (a) => { return mapV[a]; } : null;
            Func<int, int>? MapT = (mapT != null) ? (a) => { return mapT[a]; } : null;

            if (other.HasMaterialID)
                this.MaterialID.InitializeToCopy(ParentMesh!.TriangleCount, other.MaterialID, MapT);
        }

        public virtual void TrimTo(int MaxVertID, int MaxTriID)
        {
            foreach (IGeoAttribute TriAttrib in TriAttributes())
                TriAttrib.TrimTo(MaxTriID);
        }

        public virtual void OnRemoveTriangle(int tid)
        {
            // don't actually have to do anything for remove? just leave value?
        }

        public virtual void OnSplitEdge(in DMesh3.EdgeSplitInfo splitInfo)
        {
            foreach (IGeoAttribute attrib in TriAttributes()) {
                attrib.InsertValue_Copy(splitInfo.eNewT2, splitInfo.eOrigT0);
                if (splitInfo.eOrigT1 >= 0)
                    attrib.InsertValue_Copy(splitInfo.eNewT3, splitInfo.eOrigT1);
            }
        }

        public virtual void OnFlipEdge(in DMesh3.EdgeFlipInfo flipInfo)
        {
            // ?? could try to update some tri attributes...
        }

        public virtual void OnCollapseEdge(in DMesh3.EdgeCollapseInfo collapseInfo)
        {
            // ?? possibly want to upate some tri attributes...
        }

        public virtual void OnMergeEdges(in DMesh3.MergeEdgesInfo mergeInfo)
        {
            // ??
        }

        public virtual void OnPokeTriangle(in DMesh3.PokeTriangleInfo pokeInfo)
        {
            foreach (IGeoAttribute attrib in TriAttributes()) {
                attrib.InsertValue_Copy(pokeInfo.new_t1, pokeInfo.orig_t0);
                attrib.InsertValue_Copy(pokeInfo.new_t2, pokeInfo.orig_t0);
            }
        }

        protected T new_tri_attrib<T>() where T : IGeoAttribute, new()
        {
            T attrib = new();
            if (parentMesh != null)
                attrib.Initialize(parentMesh!.TriangleCount);
            return attrib;
        }

        public void CheckValidity(FailMode eFailMode = FailMode.Throw)
        {
            bool is_ok = true;
            Action<bool> CheckOrFailF = (b) => { is_ok = is_ok && b; };
            if (eFailMode == FailMode.DebugAssert) {
                CheckOrFailF = (b) => { Debug.Assert(b); is_ok = is_ok && b; };
            } else if (eFailMode == FailMode.gDevAssert) {
                CheckOrFailF = (b) => { Util.gDevAssert(b); is_ok = is_ok && b; };
            } else if (eFailMode == FailMode.Throw) {
                CheckOrFailF = (b) => { if (b == false) throw new Exception("DMesh3Attributes.CheckValidity: check failed"); };
            }

            CheckOrFailF(parentMesh != null);

            int TriCount = parentMesh!.MaxTriangleID;
            if (materialID != null)
                CheckOrFailF(materialID.NumElements == TriCount);
        }

    }

    public interface IGeoAttribute
    {
        Type ElementType { get; }
        int NumElements { get; }
        
        void Initialize(int Count);
        void InitializeToCopy(int NewCount, IGeoAttribute SourceAttrib, Func<int,int> IndexMapF);
        void TrimTo(int NewCount);

        void InsertValue_Copy(int Index, int CopyIndex = -1);
    }

    public interface IGeoAttribute<T> : IGeoAttribute where T : struct
    {
        void Initialize(int Count, T InitialValue = default)
        {
            UpdateCount(Count, true, InitialValue);
        }

        void UpdateCount(int NewCount, bool bInitializeNew = false, T InitValue = default);
        void InsertValue(int Index, T NewValue);
        void SetValue(int Index, T NewValue);
        T GetValue(int Index);

        T DefaultValue { get; }
    }


    public class BaseGeoAttribute<T> : IGeoAttribute<T> where T : struct
    {
        public DVector<T> data = new DVector<T>();

        public Type ElementType { get { return typeof(T); } }
        public int NumElements { get { return data.size; } }
        public T DefaultValue { get { return default(T); } }

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

        public void TrimTo(int NewCount)
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
