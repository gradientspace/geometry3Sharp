// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

#nullable enable

namespace g3
{

    public class AttribList<T> : IEnumerable<T> 
        where T : class
    {
        public AttribList(
            Func<uint, T> newAttribFunc,
            Action<T> destroyAttribFunc)
        {
            createNewAttribFunc = newAttribFunc;
            this.destroyAttribFunc = destroyAttribFunc;
        }

        protected Func<uint, T> createNewAttribFunc;
        protected Action<T> destroyAttribFunc;
        protected List<T> channels = new List<T>();

        public uint NumChannels => (uint)channels.Count;
        public virtual void SetNumChannels(uint NewNumChannels)
        {
            while ( channels.Count > NewNumChannels ) {
                destroyAttribFunc( channels[channels.Count-1] );
                channels.RemoveAt( channels.Count-1 );
            }
            while (channels.Count < NewNumChannels) {
                T newChannel = createNewAttribFunc((uint)channels.Count);
                channels.Add(newChannel);
            }
        }

        public bool HasChannel(uint i) { return i >= 0 && i < NumChannels; }

        public T GetChannel(uint i) {
            if (HasChannel(i) == false)
                SetNumChannels(i+1);
            return channels[(int)i]; 
        }
        public T this[uint key] => GetChannel(key);



        public IEnumerator<T> GetEnumerator() {
            return channels.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return channels.GetEnumerator();
        }
    }


    public static class AttributeNames
    {
        public const string MaterialID = "MaterialID";
        public const string TriNormals = "TriNormals;";

        public const string TriUVsPrefix = "TriUV;";
        public static string TriUV(uint Index) { return $"{TriUVsPrefix}{Index}"; }
    }

    public class DMesh3Attributes
    {
        DMesh3? parentMesh = null;
        public DMesh3? ParentMesh => parentMesh;
        List<IGeoAttribute> allAttributes = new List<IGeoAttribute>();

        public DMesh3Attributes(DMesh3 parentMesh)
        {
            this.parentMesh = parentMesh;
        }

        protected IntGeoAttribute? materialID { get; private set; } = null;
        public bool HasMaterialID => (materialID != null);
        public void EnableMaterialID() {
            if (materialID == null)
                materialID = register_attribute(new IntGeoAttribute(AttributeNames.MaterialID, parentMesh, IGeoAttribute.EAttribType.Triangle));
        }
        public void DisableMaterialID() {
            materialID = unregister_attribute(materialID);
        }
        public IntGeoAttribute MaterialID { get { if (!HasMaterialID) EnableMaterialID(); return materialID!; } }


        protected TriNormalsGeoAttribute? triNormals { get; private set; } = null;
        public bool HasTriNormals => (triNormals != null);
        public void EnableTriNormals() {
            if (triNormals == null)
                triNormals = register_attribute(new TriNormalsGeoAttribute(AttributeNames.TriNormals, parentMesh, IGeoAttribute.EAttribType.Triangle));
        }
        public void DisableTriNormals() {
            triNormals = unregister_attribute(triNormals);
        }
        public TriNormalsGeoAttribute TriNormals { get { if (!HasTriNormals) EnableTriNormals(); return triNormals!; } }



        protected AttribList<TriUVsGeoAttribute>? triangleUVs { get; private set; } = null;
        public bool HasTriUVs => (triangleUVs != null);
        public int NumTriUVChannels => (triangleUVs == null) ? 0 : (int)triangleUVs.NumChannels;
        public void EnableTriUVs(uint NumChannels = 1)
        {
            if (triangleUVs == null)
                triangleUVs = new AttribList<TriUVsGeoAttribute>( (uint idx) => { 
                        return register_attribute(new TriUVsGeoAttribute(AttributeNames.TriUV(idx), parentMesh, IGeoAttribute.EAttribType.Triangle)); 
                    },
                    (TriUVsGeoAttribute attrib) => { unregister_attribute(attrib); }  
                );
            if (triangleUVs.NumChannels < NumChannels)
                triangleUVs.SetNumChannels(NumChannels);
        }
        public void DisableTriUVs()
        {
            if (triangleUVs != null)
                SetNumUVChannels(0);
            triangleUVs = null;
        }
        public void SetNumUVChannels(uint NewNumChannels)
        {
            if (triangleUVs == null && NewNumChannels == 0) { return; }
            if (triangleUVs == null) EnableTriUVs(NewNumChannels);
            else triangleUVs.SetNumChannels(NewNumChannels);
        }
        public bool HasTriUVChannel(uint channelIndex) => (triangleUVs != null) && (triangleUVs.HasChannel(channelIndex));
        public TriUVsGeoAttribute TriUVChannel(uint channelIndex)
        {
            if (triangleUVs == null) EnableTriUVs(channelIndex);
            return triangleUVs![channelIndex];
        }



        protected virtual T register_attribute<T>(T geoAttribute) where T : class, IGeoAttribute
        {
            allAttributes.Add(geoAttribute);
            return geoAttribute;
        }
        protected virtual T? unregister_attribute<T>(T? attribute) where T : class, IGeoAttribute
        {
            if (attribute != null)
                allAttributes.Remove(attribute);
            return null;
        }


        public virtual IEnumerable<IGeoAttribute> Attributes()
        {
            return allAttributes;
        }


        public virtual IEnumerable<IGeoAttribute> TriAttributes()
        {
            foreach (IGeoAttribute geoAttribute in allAttributes)
                if (geoAttribute.AttribType == IGeoAttribute.EAttribType.Triangle)
                    yield return geoAttribute;
        }

        public virtual IGeoAttribute? FindByName(string Name)
        {
            foreach (IGeoAttribute geoAttribute in allAttributes)
                if (string.Compare(geoAttribute.Name, Name, StringComparison.OrdinalIgnoreCase) == 0)
                    return geoAttribute;
            return null;
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
            if (other.HasTriNormals)
                this.TriNormals.InitializeToCopy(ParentMesh!.TriangleCount, other.TriNormals, MapT);
            if (other.HasTriUVs) {
                this.EnableTriUVs();
                for (uint i = 0; i < other.NumTriUVChannels; ++i)
                    this.TriUVChannel(i).InitializeToCopy(ParentMesh!.TriangleCount, other.TriUVChannel(i), MapT);
            }
        }

        public virtual void TrimTo(int MaxVertID, int MaxTriID)
        {
            foreach (IGeoAttribute TriAttrib in TriAttributes())
                TriAttrib.TrimToLength(MaxTriID);
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
                if (attrib is ILinearGeoAttribute linearAttrib) {
                    linearAttrib.UpdateOnPoke(pokeInfo);
                } else {
                    attrib.InsertValue_Copy(pokeInfo.new_t1, pokeInfo.orig_t0);
                    attrib.InsertValue_Copy(pokeInfo.new_t2, pokeInfo.orig_t0);
                }
            }
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
            if (materialID != null) {
                CheckOrFailF(materialID.NumElements == TriCount);
                CheckOrFailF(FindByName(AttributeNames.MaterialID) == materialID);
            } else
                CheckOrFailF(FindByName(AttributeNames.MaterialID) == null);

            if (triNormals != null) {
                CheckOrFailF(triNormals.NumElements == TriCount);
                CheckOrFailF(FindByName(AttributeNames.TriNormals) == triNormals);
            } else
                CheckOrFailF(FindByName(AttributeNames.TriNormals) == null);

            if (triangleUVs != null) {
                for (uint i = 0; i < triangleUVs.NumChannels; ++i) {
                    CheckOrFailF(triangleUVs.HasChannel(i) == true);
                    CheckOrFailF(triangleUVs.GetChannel(i).NumElements == TriCount);
                    CheckOrFailF(FindByName(AttributeNames.TriUV(i)) == triangleUVs.GetChannel(i));
                }
                for (uint i = triangleUVs.NumChannels; i < 8; ++i)
                    CheckOrFailF(FindByName(AttributeNames.TriUV(i)) == null);
            } else {
                for (uint i = 0; i < 8; ++i)
                    CheckOrFailF(FindByName(AttributeNames.TriUV(i)) == null);
            }
        }

    }
}
