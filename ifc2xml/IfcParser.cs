using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Xbim.Common;
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

        private static XbimMatrix3D Translate(XbimMatrix3D matrix, IVector3D translation)
        {
            if (translation == null) return matrix;
            var translationMatrix = XbimMatrix3D.CreateTranslation(translation.X, translation.Y, translation.Z);
            return XbimMatrix3D.Multiply(matrix, translationMatrix);
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
        /// Save geometries to xml file(self defined format)
        /// </summary>
        /// <param name="path">xml path</param>
        /// <param name="geometries">geometries</param>
        public static void SaveGeometries(string path, ref Dictionary<string, GeometryStore> geometries)
        {
            // create document
            XmlDocument doc = new XmlDocument();
            XmlDeclaration xmldec = doc.CreateXmlDeclaration("1.0", "utf-8", "yes");            doc.AppendChild(xmldec);

            // create root element
            XmlElement rootElement = doc.CreateElement("IfcModel");

            foreach(string globalId in geometries.Keys)
            {
                var geometry = geometries[globalId];

                // create element node
                XmlElement element = doc.CreateElement("Element");
                // add attribute to element node
                XmlAttribute elementId = doc.CreateAttribute("ElementId");
                elementId.Value = geometry.GlobalId;
                XmlAttribute levelName = doc.CreateAttribute("LevelName");
                levelName.Value = ""; // currently we don't have level name
                XmlAttribute name = doc.CreateAttribute("Name");
                name.Value = geometry.Name;
                element.Attributes.Append(elementId);
                element.Attributes.Append(levelName);
                element.Attributes.Append(name);  
                
                // an element node can have multiple mesh nodes
                foreach(var meshStore in geometry.Meshes)
                {
                    // create a mesh node
                    XmlElement mesh = doc.CreateElement("Mesh");

                    // add attribute to mesh node
                    XmlAttribute meshId = doc.CreateAttribute("ElementId");
                    meshId.Value = geometry.GlobalId; // the same as element
                    XmlAttribute color = doc.CreateAttribute("Color");
                    color.Value = meshStore.Color;
                    XmlAttribute material = doc.CreateAttribute("Material");
                    material.Value = ""; // currently we don't have material    

                    mesh.Attributes.Append(meshId);
                    mesh.Attributes.Append(color);
                    mesh.Attributes.Append(material);

                    // lod0
                    XmlElement lod0 = doc.CreateElement("Lod0");
                    mesh.AppendChild(lod0);

                    XmlElement uvs = doc.CreateElement("UVs");
                    lod0.AppendChild(uvs);

                    XmlElement uvIndexes = doc.CreateElement("UVIndexes");
                    lod0.AppendChild(uvIndexes);

                    XmlElement vertices = doc.CreateElement("Vertices");
                    StringBuilder sb = new StringBuilder();
                    foreach(var vertex in meshStore.Vertexes)
                    {
                        sb.Append(vertex).Append(",");
                    }
                    vertices.InnerText = sb.Remove(sb.Length - 1, 1).ToString();
                    lod0.AppendChild(vertices);

                    sb.Clear();
                    XmlElement indexes = doc.CreateElement("PointIndexes");
                    foreach(var index in meshStore.Indexes)
                    {
                        sb.Append(index).Append(",");
                    }
                    indexes.InnerText = sb.Remove(sb.Length - 1, 1).ToString();
                    lod0.AppendChild(indexes);
                    // ... can add lod1 lod2 lod3, but currently they are the same so we ignore them

                    element.AppendChild(mesh);
                }
                rootElement.AppendChild(element);     
            }

            doc.AppendChild(rootElement);
            doc.Save(path);           
        }
    }
}
