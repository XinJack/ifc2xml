using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace ifc2xml
{
    class IfcParser
    {
        /// <summary>
        /// Method for parsing ifc file to extract properties and geometries
        /// </summary>
        /// <param name="ifcFilePath">ifc file path</param>
        /// <param name="elemProperties">properties</param>
        /// <param name="geometries">geometries</param>
        public static void ParseIfcFile(string ifcFilePath, ref LinkedList<NameValueCollection> elemProperties, ref Dictionary<string, GeometryStore> geometries)
        { 
            // try open ifc file           
            using (var model = IfcStore.Open(ifcFilePath))
            {
                // context is used to extract geometry data
                var context = new Xbim3DModelContext(model);
                var meter = context.Model.ModelFactors.OneMeter;
                context.CreateContext();

                // start to extract geometry data
                var instances = context.ShapeInstances();
                XbimColourMap colorMap = new XbimColourMap();
                foreach(var instance in instances) // each instance is a mesh
                {
                    MeshStore meshStore = new MeshStore();
                    
                    // get the color of this mesh
                    var ss = model.Instances[instance.StyleLabel] as IIfcSurfaceStyle;
                    XbimColour color = XbimColour.DefaultColour;
                    if(ss != null)
                    {
                        var texture = XbimTexture.Create(ss);
                        color = texture.ColourMap.FirstOrDefault();
                    }else
                    {
                        var styleId = instance.StyleLabel > 0 ? instance.StyleLabel : instance.IfcTypeId;
                        var theType = model.Metadata.GetType((short)styleId);
                        color = colorMap[theType.Name];
                    }
                    
                    meshStore.Color = string.Format("{0},{1},{2},{3}", (int)(color.Red * 255), (int)(color.Green * 255), (int)(color.Blue * 255), color.Alpha * 255);

                    var geometry = context.ShapeGeometry(instance);
                    var data = (geometry as IXbimShapeGeometryData).ShapeData;

                    // multiple geometry may belong to one product, like frame and glass belong to a ifcwindow
                    var product = model.Instances[instance.IfcProductLabel] as IIfcProduct;

                    if (!geometries.ContainsKey(product.GlobalId))
                    {
                        geometries.Add(product.GlobalId, new GeometryStore(product.GlobalId, product.Name));
                        
                    }
                    var geometryStore = geometries[product.GlobalId];
                    
                    // reading the real geometry data
                    using (var stream = new MemoryStream(data))
                    {
                        using(var reader = new BinaryReader(stream))
                        {
                            // triangle the instance and transform it to the correct position
                            var mesh = reader.ReadShapeTriangulation();
                            mesh = mesh.Transform(instance.Transformation);

                            var verticesData = new List<double>();
                            var normalsData = new List<double>();
                            var indexesData = new List<int>(); 

                            var faces = mesh.Faces as List<XbimFaceTriangulation>;
                            foreach(var face in faces)
                            {
                                foreach(var indice in face.Indices)
                                {
                                    indexesData.Add(indice);
                                }
                                foreach(var normal in face.Normals)
                                {
                                    normalsData.Add(normal.Normal.X);
                                    normalsData.Add(normal.Normal.Y);
                                    normalsData.Add(normal.Normal.Z);
                                }
                            }
                            var vertices = mesh.Vertices as List<XbimPoint3D>;
                            foreach(var point in vertices)
                            {
                                verticesData.Add(point.X / meter); // convert to meter
                                verticesData.Add(point.Y / meter);
                                verticesData.Add(point.Z / meter);
                            }

                            meshStore.Vertexes = verticesData;
                            meshStore.Normals = normalsData;
                            meshStore.Indexes = indexesData;                         
                        }
                    }
                    geometryStore.Meshes.Add(meshStore);
                }

                // ifcproject is the root and normally there is only one in an ifc file
                var project = model.Instances.FirstOrDefault<IIfcProject>();

                // process ifc file recursively
                Process(project, ref elemProperties);
            }
        }

        /// <summary>
        /// Help method. Recursively visited elements
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="elemProperties"></param>
        private static void Process(IIfcObjectDefinition obj, ref LinkedList<NameValueCollection> elemProperties)
        {
            // try cast the obj to spatial structure element which contains geometry infomation
            var spatialElement = obj as IIfcSpatialStructureElement;
            if(spatialElement != null)
            {

                elemProperties.AddLast(ExtractPropertiesForElement(spatialElement));

                // process element contained in this spartial structure element
                var containedElements = spatialElement.ContainsElements.SelectMany(rel => rel.RelatedElements);
                foreach (var element in containedElements)
                {
                    elemProperties.AddLast(ExtractPropertiesForElement(element));
                }
            }

            // process elements that are associated with this spatial element
            foreach (var item in obj.IsDecomposedBy.SelectMany(r => r.RelatedObjects))
            {
                Process(item, ref elemProperties);
            }
        }

        /// <summary>
        /// Help function for convert IfcProduct to Element
        /// </summary>
        /// <param name="product">IIfcProduct</param>
        /// <returns>Element object</returns>
        private static NameValueCollection ExtractPropertiesForElement(IIfcProduct product)
        {
            NameValueCollection properties = new NameValueCollection();

            // extract all the properties of this spatial structure element
            var ifcProperties = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertySingleValue>();
            foreach (var property in ifcProperties)
            {
                properties.Add(property.Name, property.NominalValue.ToString());
            }

            // add basic information
            properties.Add("GlobalId", product.GlobalId);
            properties.Add("Name", product.Name);
            properties.Add("Description", product.Description);
            properties.Add("IfcType", product.GetType().Name);

            return properties;
        }

        /// <summary>
        /// Save properties of elements into json file
        /// </summary>
        /// <param name="path">json file path</param>
        /// <param name="elemProperties">element properties</param>
        public static void SaveProperties(string path, ref LinkedList<NameValueCollection> elemProperties)
        {
            // manager data into json object
            JObject json = new JObject();
            foreach (var properties in elemProperties)
            {
                JObject propJson = new JObject();
                foreach (var property in properties.AllKeys)
                {
                    propJson.Add(property, properties.Get(property));
                }
                json.Add(properties.Get("GlobalId"), propJson);
            }

            // write json data into file
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.Write(json.ToString());
            }
        }

        /// <summary>
        /// Save geometries into xml files
        /// </summary>
        /// <param name="path">xml file path</param>
        /// <param name="geometries">geometries</param>
        /// <param name="fileSizeLimit">file size limit for each xml file</param>
        /// <param name="threshold">threshold of triangle count for mesh decimation</param>
        /// <param name="quality">quality for mesh decimation</param>
        public static void SaveGeometries(string path, ref Dictionary<string, GeometryStore> geometries, double fileSizeLimit, int threshold, double quality)
        {
            string baseName = path.Substring(0, path.Length - 4);

            // xml content holder
            StringBuilder doc = new StringBuilder();

            // name index for each output xml file
            int nameIndex = 0;

            foreach (string globalId in geometries.Keys)
            {
                // before processing the next geometry, check whether we should output the current document
                // reach file size limit. write to file and start a new document
                if(doc.Length >= fileSizeLimit)
                {
                    // add end tag
                    doc.Append("</IfcModel>");

                    // write to xml file
                    using (var writer = new StreamWriter(baseName + "_" + nameIndex + ".xml", false, Encoding.UTF8))
                    {
                        writer.Write(doc.ToString());
                    }

                    doc.Clear();
                    nameIndex++;                 
                }

                // empty document, add the first two lines
                if(doc.Length == 0)
                {
                    doc.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n")
                        .Append("<IfcModel>\n");
                }

                var geometry = geometries[globalId];

                // add Element start tag
                doc.Append(string.Format("\t<Element ElementId=\"{0}\" LevelName=\"\" Name=\"{1}\">\n", geometry.GlobalId, geometry.Name));
                
                // an element node can have multiple mesh nodes
                for(int i = 0; i < geometry.Meshes.Count; ++i)
                {
                    var meshStore = geometry.Meshes[i];
                    // add mesh start tag, lod0 start tag, uvs tag(empty), uvindexes tag(empty)
                    doc.Append(string.Format("\t\t<Mesh ElementId=\"{0}\" Color=\"{1}\" Material=\"\">\n", geometry.GlobalId, meshStore.Color))
                        .Append("\t\t\t<Lod0>\n")
                        .Append("\t\t\t\t<UVs />\n")
                        .Append("\t\t\t\t<UVIndexes />\n");

                    // decimate mesh if needed
                    int originTriangleCount = meshStore.Indexes.Count / 3;
                    if(originTriangleCount >= threshold)
                    {
                        Logger.Info("Origin mesh vertice count: {0}, triangle count: {1}", meshStore.Vertexes.Count / 3, originTriangleCount);
                        int targetTriangleCount = (int)Math.Ceiling(originTriangleCount * quality);
                        MeshDecimator.Decimate(ref meshStore, targetTriangleCount);
                        Logger.Info("Decimated mesh vertice count: {0}, triangle count: {1}", meshStore.Vertexes.Count / 3, meshStore.Indexes.Count / 3);
                    }

                    // add vertices tag
                    doc.Append("\t\t\t\t<Vertices>");
                    foreach(var vertex in meshStore.Vertexes)
                    {
                        doc.Append(vertex).Append(",");
                    }
                    doc.Remove(doc.Length - 1, 1) // remove the last ","
                        .Append("</Vertices>\n");

                    // add pointindexes tag
                    if(meshStore.Indexes.Count / 3 > 500)
                        Console.WriteLine("face number:{0}", meshStore.Indexes.Count / 3);
                    doc.Append("\t\t\t\t<PointIndexes>");
                    foreach(var index in meshStore.Indexes)
                    {
                        doc.Append(index).Append(",");
                    }
                    doc.Remove(doc.Length - 1, 1)
                         .Append("</PointIndexes>\n");

                    // end lod0 tag
                    doc.Append("\t\t\t</Lod0>\n");

                    // ... can add lod1 lod2 lod3, but currently they are the same so we ignore them

                    // end this mesh
                    doc.Append("\t\t</Mesh>\n");
                }
                // end this geometry
                doc.Append("\t</Element>\n");     
            }
            // the last document may not reach the file size limit
            if(doc.Length != 0)
            {
                // add end tag
                doc.Append("</IfcModel>");

                // write to xml file
                using (var writer = new StreamWriter(baseName + "_" + nameIndex + ".xml", false, Encoding.UTF8))
                {
                    writer.Write(doc.ToString());
                }

                doc.Clear();
            }     
        }
    }
}
