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
        #region Properties
        /// <summary>
        /// Extract IFC properties.
        /// </summary>
        /// <param name="model">IfcStore model.</param>
        /// <param name="elemProperties">Properties data.</param>
        public static void ExtractProperties(IfcStore model, ref LinkedList<NameValueCollection> elemProperties)
        {
            // ifcproject is the root and normally there is only one in an ifc file
            var project = model.Instances.FirstOrDefault<IIfcProject>();

            // process ifc file recursively
            Process(project, ref elemProperties);
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
                    string value = properties.Get(property);
                    propJson.Add(property, value == null? "": value.Replace('"', '\''));
                }
                json.Add(properties.Get("GlobalId"), propJson);
            }

            // write json data into file
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.Write(json.ToString());
            }
        }
        #endregion

        #region Geometries
        /// <summary>
        /// Extract Geometries from IFC file.
        /// </summary>
        /// <param name="model">IfcStore model.</param>
        /// <param name="geometries">Geometries data.</param>
        /// <param name="modelBoundingBox">Bounding box of the ifc model.</param>
        public static void ExtractGeometries(IfcStore model, ref Dictionary<string, GeometryStore> geometries, ref XbimRect3D modelBoundingBox)
        {
            // context is used to extract geometry data
            var context = new Xbim3DModelContext(model);
            var meter = context.Model.ModelFactors.OneMeter;
            context.CreateContext();

            // start to extract geometry data
            var instances = context.ShapeInstances();
            XbimColourMap colorMap = new XbimColourMap();
            foreach (var instance in instances) // each instance is a mesh
            {
                MeshStore meshStore = new MeshStore();

                // get the color of this mesh
                var ss = model.Instances[instance.StyleLabel] as IIfcSurfaceStyle;
                XbimColour color = XbimColour.DefaultColour;
                if (ss != null)
                {
                    var texture = XbimTexture.Create(ss);
                    color = texture.ColourMap.FirstOrDefault();
                }
                else
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
                    using (var reader = new BinaryReader(stream))
                    {
                        // triangle the instance and transform it to the correct position
                        var mesh = reader.ReadShapeTriangulation();
                        mesh = mesh.Transform(instance.Transformation);

                        // geometry data
                        var verticesData = new List<double>();
                        var normalsData = new List<double>();
                        var indexesData = new List<int>();

                        // bouding box for instance.
                        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue,
                            maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                        var faces = mesh.Faces as List<XbimFaceTriangulation>;
                        foreach (var face in faces)
                        {
                            foreach (var indice in face.Indices)
                            {
                                indexesData.Add(indice);
                            }
                            foreach (var normal in face.Normals)
                            {
                                normalsData.Add(normal.Normal.X);
                                normalsData.Add(normal.Normal.Y);
                                normalsData.Add(normal.Normal.Z);
                            }
                        }
                        var vertices = mesh.Vertices as List<XbimPoint3D>;
                        foreach (var point in vertices)
                        {
                            double x = point.X / meter, y = point.Y / meter, z = point.Z / meter;
                            verticesData.Add(x); // convert to meter
                            verticesData.Add(y);
                            verticesData.Add(z);

                            // update bounding box
                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            minZ = Math.Min(minZ, z);
                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);
                            maxZ = Math.Max(maxZ, z);
                        }

                        if (minX == double.MaxValue || minY == double.MaxValue || minZ == double.MaxValue
                            || maxX == double.MinValue || maxY == double.MinValue || maxZ == double.MinValue) throw new Exception("Invalid Boundingbox");
                        
                        // do not trust instance.BoundingBox, it seems to be wrong.
                        XbimRect3D bbox = new XbimRect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
                        
                        // update boundingbox of current geometryStore
                        geometryStore.BoundBox = GeometryStore.Union(geometryStore.BoundBox, bbox);

                        // update boundingbox of ifc model
                        modelBoundingBox = GeometryStore.Union(modelBoundingBox, bbox);

                        meshStore.Vertexes = verticesData;
                        meshStore.Normals = normalsData;
                        meshStore.Indexes = indexesData;
                    }
                }
                geometryStore.Meshes.Add(meshStore);
            }
        }

        /// <summary>
        /// Tiling bounding box into small bounding boxes according to options.
        /// </summary>
        /// <param name="bbox">Bounding box to tile</param>
        /// <param name="tiles">output tiles</param>
        /// <param name="tileXSize">X size of tile</param>
        /// <param name="tileYSize">Y size of tile</param>
        /// <param name="tileZSize">Z size of tile</param>
        public static void SplitBoundingBox(XbimRect3D bbox, ref Dictionary<XbimRect3D, List<GeometryStore>> tiles, double tileXSize, double tileYSize, double tileZSize)
        {
            // calculate tile count along x, y, z axis
            int xCount = (int)Math.Ceiling(bbox.SizeX / tileXSize);
            int yCount = (int)Math.Ceiling(bbox.SizeY / tileYSize);
            int zCount = (int)Math.Ceiling(bbox.SizeZ / tileZSize);

            // tile boundingbox
            for(int i = 0; i < xCount; ++i)
            {
                for(int j = 0; j < yCount; ++j)
                {
                    for(int k = 0; k < zCount; ++k)
                    {
                        XbimRect3D range = new XbimRect3D(
                            bbox.Min.X + i * tileXSize,
                            bbox.Min.Y + j * tileYSize,
                            bbox.Min.Z + k * tileZSize,
                            tileXSize,
                            tileYSize,
                            tileZSize
                            );
                        tiles.Add(range, new List<GeometryStore>());
                    }
                }
            }

        }

        /// <summary>
        /// Allocate each geometry into appropriate tile
        /// </summary>
        /// <param name="geometries">Dictionary that contains geometries</param>
        /// <param name="tiles">Dictionary that contains tiles</param>
        /// <param name="maxElementPerTile">Maximum number of elements in each tile</param>
        public static void AllocateGeometryToTile(ref Dictionary<string, GeometryStore> geometries, ref Dictionary<XbimRect3D, List<GeometryStore>> tiles, int maxElementPerTile)
        {
            // allocate element to appropriate tile according to intersect volume
            foreach(GeometryStore gs in geometries.Values)
            {
                XbimRect3D candicateTile = XbimRect3D.Empty;
                double intersectVolume = double.MinValue;
                foreach(XbimRect3D tile in tiles.Keys)
                {
                    double volume = GeometryStore.Intersect(gs.BoundBox, tile);
                    if(volume > intersectVolume)
                    {
                        candicateTile = tile;
                        intersectVolume = volume;
                    }
                }
                if(!candicateTile.Equals(XbimRect3D.Empty))
                {
                    tiles[candicateTile].Add(gs);
                }
            }
            
            foreach(XbimRect3D bbox in tiles.Keys.ToList())
            {
                // remove empty tile
                if(tiles[bbox].Count == 0)
                {
                    tiles.Remove(bbox);
                }
                else if(tiles[bbox].Count > maxElementPerTile && bbox.SizeX > 1 && bbox.SizeY > 1 && bbox.SizeZ > 1) // retile a tile(tile size should be large than 1)
                {
                    Dictionary<XbimRect3D, List<GeometryStore>> subTiles = new Dictionary<XbimRect3D, List<GeometryStore>>();
                    SplitBoundingBox(bbox, ref subTiles, bbox.SizeX / 2.0, bbox.SizeY / 2.0, bbox.SizeZ / 2.0);

                    Dictionary<string, GeometryStore> elements = new Dictionary<string, GeometryStore>();
                    foreach(GeometryStore gs in tiles[bbox])
                    {
                        elements.Add(gs.GlobalId, gs);
                    }
                    AllocateGeometryToTile(ref elements, ref subTiles, maxElementPerTile);

                    tiles.Remove(bbox);
                    foreach(XbimRect3D subBbox in subTiles.Keys)
                    {
                        tiles.Add(subBbox, subTiles[subBbox]);
                    }
                }
            }
        }

        /// <summary>
        /// Output geometries (mesh decimated with quality) to xml file.
        /// </summary>
        /// <param name="path">xml path</param>
        /// <param name="geometries">Geometries to output</param>
        /// <param name="threshold">Threshold to decimate mesh</param>
        /// <param name="quality">Mesh decimation quality</param>
        public static void SaveGeometries(string path, ref List<GeometryStore> geometries, int threshold, double quality)
        {
            // xml content holder
            StringBuilder doc = new StringBuilder();

            // add first two line
            doc.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n")
                        .Append("<IfcModel>\n");

            foreach (GeometryStore geometry in geometries)
            {
                // add Element start tag
                doc.Append(string.Format("\t<Element ElementId=\"{0}\" LevelName=\"\" Name=\"{1}\">\n", geometry.GlobalId, geometry.Name == null? "": geometry.Name.Replace('"', '\'')));

                // an element node can have multiple mesh nodes
                for (int i = 0; i < geometry.Meshes.Count; ++i)
                {
                    var meshStore = geometry.Meshes[i];
                    // add mesh start tag, lod0 start tag, uvs tag(empty), uvindexes tag(empty)
                    doc.Append(string.Format("\t\t<Mesh ElementId=\"{0}\" Color=\"{1}\" Material=\"\">\n", geometry.GlobalId, meshStore.Color))
                        .Append("\t\t\t<Lod0>\n")
                        .Append("\t\t\t\t<UVs />\n")
                        .Append("\t\t\t\t<UVIndexes />\n");

                    // decimate mesh
                    int originTriangleCount = meshStore.Indexes.Count / 3;
                    if(originTriangleCount > threshold)
                    {
                        Logger.Info("Origin mesh vertice count: {0}, triangle count: {1}", meshStore.Vertexes.Count / 3, originTriangleCount);
                        int targetTriangleCount = (int)Math.Ceiling(originTriangleCount * quality);
                        MeshDecimator.Decimate(ref meshStore, targetTriangleCount);
                        Logger.Info("Decimated mesh vertice count: {0}, triangle count: {1}", meshStore.Vertexes.Count / 3, meshStore.Indexes.Count / 3);
                    }

                    // add vertices tag
                    doc.Append("\t\t\t\t<Vertices>");
                    foreach (var vertex in meshStore.Vertexes)
                    {
                        doc.Append(vertex).Append(",");
                    }
                    doc.Remove(doc.Length - 1, 1) // remove the last ","
                        .Append("</Vertices>\n");

                    // add pointindexes tag
                    if (meshStore.Indexes.Count / 3 > 500)
                        Console.WriteLine("face number:{0}", meshStore.Indexes.Count / 3);
                    doc.Append("\t\t\t\t<PointIndexes>");
                    foreach (var index in meshStore.Indexes)
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

            // add end tag
            doc.Append("</IfcModel>");

            // write to xml file
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.Write(doc.ToString());
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
                doc.Append(string.Format("\t<Element ElementId=\"{0}\" LevelName=\"\" Name=\"{1}\">\n", geometry.GlobalId, geometry.Name.Replace('"', '\'')));
                
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
        #endregion
    }
}
