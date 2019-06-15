using MeshDecimator;
using MeshDecimator.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2xml
{
    /// <summary>
    /// Class used for mesh decimation
    /// </summary>
    class MeshDecimator
    {
        /// <summary>
        /// Method for mesh decimation
        /// </summary>
        /// <param name="meshStore">mesh data</param>
        /// <param name="targetTriangleCount">target triangle count for decimation</param>
        public static void Decimate(ref MeshStore meshStore, int targetTriangleCount)
        {
            // extract origin mesh data and create mesh instance
            Vector3d[] vertices = new Vector3d[meshStore.Vertexes.Count / 3];
            for(int i = 0, j = 0; i < meshStore.Vertexes.Count; i += 3, ++j)
            {
                vertices[j] = new Vector3d(meshStore.Vertexes[i], meshStore.Vertexes[i + 1], meshStore.Vertexes[i + 2]);
            }
            Mesh sourceMesh = new Mesh(vertices, meshStore.Indexes.ToArray());

            // decimate mesh
            var algorithm = MeshDecimation.CreateAlgorithm(Algorithm.Default);
            algorithm.Verbose = true;
            Mesh destMesh = MeshDecimation.DecimateMesh(algorithm, sourceMesh, targetTriangleCount);

            // change meshstore
            meshStore.Indexes = destMesh.Indices.ToList();
            List<double> newVertices = new List<double>();
            foreach(Vector3d vec in destMesh.Vertices)
            {
                newVertices.Add(vec.x);
                newVertices.Add(vec.y);
                newVertices.Add(vec.z);
            }
            meshStore.Vertexes = newVertices;
        }
    }
}
