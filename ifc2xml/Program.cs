using CommandLine;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.QuantityResource;

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
                        try
                        {
                            LinkedList<NameValueCollection> elemProperties = new LinkedList<NameValueCollection>();
                            Dictionary<string, GeometryStore> geometries = new Dictionary<string, GeometryStore>();
                            IfcParser.ParseIfcFile(options.IfcFilePath, ref elemProperties, ref geometries);
                            string propertyPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "json";
                            IfcParser.SaveProperties(propertyPath, ref elemProperties);
                            Logger.Info("Save properties successfully!");
                            string geometryPath = options.IfcFilePath.Substring(0, options.IfcFilePath.Length - 3) + "xml";
                            IfcParser.SaveGeometries(geometryPath, ref geometries);
                            Logger.Info("Save geometries successfully!");
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
