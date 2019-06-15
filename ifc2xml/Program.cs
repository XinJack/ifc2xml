using CommandLine;
using MeshDecimator.Math;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

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
                        //try
                        //{
                            // restrict options
                            int threshold = Math.Abs(options.Threshold);
                            double quality = MathHelper.Clamp01(options.Quality);

                            LinkedList<NameValueCollection> elemProperties = new LinkedList<NameValueCollection>();
                            Dictionary<string, GeometryStore> geometries = new Dictionary<string, GeometryStore>();
                            IfcParser.ParseIfcFile(options.IfcFilePath, ref elemProperties, ref geometries);
                            string propertyPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "json";
                            IfcParser.SaveProperties(propertyPath, ref elemProperties);
                            Logger.Info("Save properties successfully!");
                            string geometryPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "xml";
                            IfcParser.SaveGeometries(geometryPath, ref geometries, options.FileSizeLimit * 1048576, threshold, quality);
                            Logger.Info("Save geometries successfully!");
                            Logger.Info("Work done!");
                        //}
                        //catch (Exception why)
                        //{
                        //    Logger.Error(why.Message);
                        //}
                        
                    }
                });

            Console.WriteLine("Enter any key to exit...");
            Console.ReadLine();
        }
    }
}
