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

        [Option('t', "threshold", Required =false, Default =500,HelpText ="Threshold of triangle count for a mesh to decimate")]
        public int Threshold { get; set; }

        [Option('q', "quality", Required =false, Default =0.5, HelpText ="Quality control for mesh decimate, between 0.1 ~ 1.0")]
        public double Quality { get; set; }
    }
}
