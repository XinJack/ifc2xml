using CommandLine;
using MeshDecimator.Math;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
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
                        if(!options.OutputGeometry && !options.OutputProperty)
                        {
                            options.OutputGeometry = true;
                        }

                        try
                        {
                            // restrict options
                            int threshold = Math.Abs(options.Threshold);
                            double quality = MathHelper.Clamp01(options.Quality);

                            using (var model = IfcStore.Open(options.IfcFilePath))
                            {
                                // output properties
                                if (options.OutputProperty)
                                {
                                    Logger.Info("Start to extract properties...");

                                    LinkedList<NameValueCollection> elemProperties = new LinkedList<NameValueCollection>();
                                    IfcParser.ExtractProperties(model, ref elemProperties);

                                    string propertyPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "json";
                                    IfcParser.SaveProperties(propertyPath, ref elemProperties);
                                    Logger.Info("Save properties successfully!");
                                }
                                if (options.OutputGeometry)
                                {
                                    Logger.Info("Start to extract geometries...");

                                    Dictionary<string, GeometryStore> geometries = new Dictionary<string, GeometryStore>();
                                    IfcParser.ExtractGeometries(model, ref geometries);

                                    string geometryPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "xml";
                                    IfcParser.SaveGeometries(geometryPath, ref geometries, options.FileSizeLimit * 1048576, threshold, quality);
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
