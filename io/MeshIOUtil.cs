using System;
using System.Collections.Generic;

namespace g3 
{
	public static class MeshIOUtil 
	{
		
		public static List<GenericMaterial> FindUniqueMaterialList(List<WriteMesh> meshes) 
		{
			List<GenericMaterial> unique = new List<GenericMaterial>();
			foreach (WriteMesh mesh in meshes) {
				if (mesh.Materials == null )
					continue;
				foreach ( GenericMaterial mat in mesh.Materials ) {
					if ( unique.Contains(mat) == false )
						unique.Add(mat);
				}
			}
			return unique;
		}
	}
}
