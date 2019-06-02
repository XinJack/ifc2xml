using CommandLine;

namespace ifc2xml
{
    /// <summary>
    /// CommandLineParser options
    /// </summary>
    class Options
    {
        [Option('i', "input", Required =true, HelpText ="The path of ifc file")]
        public string IfcFilePath { get; set; }

        [Option('s', "size", Required =false, Default = 10.0, HelpText ="Maximum size of output xml in MB")]
        public double FileSizeLimit { get; set; }
    }
}
