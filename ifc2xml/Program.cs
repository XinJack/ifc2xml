using CommandLine;
using MeshDecimator.Math;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Xbim.Common.Geometry;
using Xbim.Ifc;

namespace ifc2xml
{
    class Program
    {
        static void Main(string[] args)
        {
            // parse command line arguments
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(options =>
                {

                    if (!options.IfcFilePath.EndsWith(".ifc"))
                    {
                        Logger.Error("...input file does not have .ifc suffix", options.IfcFilePath);
                    }
                    else if (!File.Exists(options.IfcFilePath))
                    {
                        Logger.Error("...input file does not exist, please check it!", options.IfcFilePath);
                    }
                    else
                    {
                        // default to output geometries
                        if (!options.OutputGeometry && !options.OutputProperty)
                        {
                            options.OutputGeometry = true;
                        }

                        try
                        {
                            // restrict options
                            int threshold = Math.Abs(options.Threshold);
                            double quality = MathHelper.Clamp01(options.Quality);
                            double tileXSize = options.TileXSize <= 0 ? 100.0 : options.TileXSize;
                            double tileYSize = options.TileYSize <= 0 ? 100.0 : options.TileYSize;
                            double tileZSize = options.TileZSize <= 0 ? 100.0 : options.TileZSize;
                            int maxElementPerTile = options.MaxElementPerTile < 1 ? 1 : options.MaxElementPerTile;

                            using (var model = IfcStore.Open(options.IfcFilePath))
                            {
                                // output properties
                                if (options.OutputProperty)
                                {
                                    Logger.Info("Start to extract properties...");

                                    // extract properties                                  
                                    LinkedList<NameValueCollection> elemProperties = new LinkedList<NameValueCollection>();
                                    IfcParser.ExtractProperties(model, ref elemProperties);

                                    // output properties
                                    string propertyPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "json";
                                    IfcParser.SaveProperties(propertyPath, ref elemProperties);
                                    Logger.Info("Save properties successfully!");
                                }
                                if (options.OutputGeometry)
                                {
                                    Logger.Info("Start to extract geometries...");

                                    // exract geometries
                                    Dictionary<string, GeometryStore> geometries = new Dictionary<string, GeometryStore>();
                                    XbimRect3D modelBoundingBox = XbimRect3D.Empty;
                                    IfcParser.ExtractGeometries(model, ref geometries, ref modelBoundingBox);

                                    // tile geometries
                                    Dictionary<XbimRect3D, List<GeometryStore>> tiles = new Dictionary<XbimRect3D, List<GeometryStore>>();
                                    IfcParser.SplitBoundingBox(modelBoundingBox, ref tiles, tileXSize, tileYSize, tileZSize);

                                    // arrange geometries to tile
                                    IfcParser.AllocateGeometryToTile(ref geometries, ref tiles, maxElementPerTile);

                                    // output geometries
                                    string ifcFileName = Path.GetFileNameWithoutExtension(options.IfcFilePath);
                                    string baseDir = Path.GetDirectoryName(options.IfcFilePath);
                                    if (!Directory.Exists(baseDir + "\\goutput"))
                                    {
                                        Directory.CreateDirectory(baseDir + "\\goutput");
                                    }
                                    int count = 0;
                                    foreach (XbimRect3D bbox in tiles.Keys)
                                    {
                                        List<GeometryStore> geometryStores = tiles[bbox];
                                        if (geometryStores.Count > 0)
                                        {
                                            string xmlPath = String.Format(@"{0}/goutput/{1}_{2}.xml", baseDir, ifcFileName, count);
                                            IfcParser.SaveGeometries(xmlPath, ref geometryStores, threshold, quality);
                                            count++;
                                        }
                                    }

                                    //string geometryPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "xml";
                                    //IfcParser.SaveGeometries(geometryPath, ref geometries, options.FileSizeLimit * 1048576, threshold, quality);
                                    Logger.Info("Save geometries successfully!");
                                }

                            }
                            Logger.Info("Work done!");
                        }
                        catch (Exception why)
                        {
                            Logger.Error(why.Message);
                        }
                    }
                });

            Console.WriteLine("Enter any key to exit...");
            Console.ReadLine();
        }
    }
}
