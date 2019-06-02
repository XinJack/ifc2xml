using System.Collections.Generic;

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

        public GeometryStore(string globalId, string name)
        {
            this.GlobalId = globalId;
            this.Name = name;
            this.Meshes = new List<MeshStore>();
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
