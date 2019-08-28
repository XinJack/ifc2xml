using System;
using System.Collections.Generic;
using Xbim.Common.Geometry;

namespace ifc2xml
{
    /// <summary>
    /// Class for store an ifcproduct's geometry
    /// </summary>
    class GeometryStore
    {
        public string GlobalId { set; get; } // GlobalId for IfcProduct
        public string Name { set; get; } // Name for IfcProduct

        public List<MeshStore> Meshes{ set; get; } // An IfcProduct may have multiple mesh

        public XbimRect3D BoundBox { set; get; } // Bounding box of all mesh in this geometry store

        /// <summary>
        /// Helper function to calculate the intersect value of two (XbimRect3D) Bounding box.
        /// </summary>
        /// <param name="boxA">Bounding box A</param>
        /// <param name="boxB">Bounding box B</param>
        /// <returns>Intersect volume.</returns>
        public static double Intersect(XbimRect3D boxA, XbimRect3D boxB)
        {
            double lowX = Math.Max(boxA.Min.X, boxB.Min.X), lowY = Math.Max(boxA.Min.Y, boxB.Min.Y), lowZ = Math.Max(boxA.Min.Z, boxB.Min.Z);
            double highX = Math.Min(boxA.Max.X, boxB.Max.X), highY = Math.Min(boxA.Max.Y, boxB.Max.Y), highZ = Math.Min(boxA.Max.Z, boxB.Max.Z);

            double xA = Math.Min(lowX, highX), yA = Math.Min(lowY, highY), zA = Math.Min(lowZ, highZ);
            double xB = Math.Max(lowX, highX), yB = Math.Max(lowY, highY), zB = Math.Max(lowZ, highZ);

            if (xA > xB || yA > yB || zA > zB) return 0.0;
            return (xB - xA) * (yB - yA) * (zB - zA);
        }

        /// <summary>
        /// Helper function to calculate the union bounding box of two bounding box(XbimRect3D).
        /// </summary>
        /// <param name="boxA">Bounding box A</param>
        /// <param name="boxB">Bounding box B</param>
        /// <returns>Union bounding box.</returns>
        public static XbimRect3D Union(XbimRect3D boxA, XbimRect3D boxB)
        {
            if (boxA.IsEmpty) return new XbimRect3D(boxB.X, boxB.Y, boxB.Z, boxB.SizeX, boxB.SizeY, boxB.SizeZ);
            if (boxB.IsEmpty) return new XbimRect3D(boxA.X, boxA.Y, boxA.Z, boxA.SizeX, boxA.SizeY, boxA.SizeZ);

            double lowX = Math.Min(boxA.Min.X, boxB.Min.X), lowY = Math.Min(boxA.Min.Y, boxB.Min.Y), lowZ = Math.Min(boxA.Min.Z, boxB.Min.Z);
            double highX = Math.Max(boxA.Max.X, boxB.Max.X), highY = Math.Max(boxA.Max.Y, boxB.Max.Y), highZ = Math.Max(boxA.Max.Z, boxB.Max.Z);

            return new XbimRect3D(lowX, lowY, lowZ, highX - lowX, highY - lowY, highZ - lowZ);
        }

        public GeometryStore(string globalId, string name)
        {
            this.GlobalId = globalId;
            this.Name = name;
            this.Meshes = new List<MeshStore>();
            this.BoundBox = XbimRect3D.Empty;
        }
    }

    /// <summary>
    /// Class for store vertex, indexes and normals
    /// </summary>
    class MeshStore
    {
        public string Color { get; set; } // color: "R,G,B,A" where each component is between 0-255
        public List<int> Indexes { set; get; }
        public List<double> Vertexes { set; get; }
        public List<double> Normals { set; get; }        
    }
}
