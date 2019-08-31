using CommandLine;

namespace ifc2xml
{
    /// <summary>
    /// CommandLineParser options
    /// </summary>
    class Options
    {
        [Option('g', "outputGeometry", Required =false, HelpText ="If Set, output geometry data")]
        public bool OutputGeometry { get; set; }

        [Option('p', "outputProperty", Required =false, HelpText ="If Set, output property data")]
        public bool OutputProperty { get; set; }

        [Option('i', "input", Required =true, HelpText ="The path of ifc file")]
        public string IfcFilePath { get; set; }

        [Option('x', "tileX", Required =false, Default =100, HelpText ="X size of each tile")]
        public double TileXSize { get; set; }

        [Option('y', "tileY", Required =false, Default =100, HelpText ="Y size of each tile")]
        public double TileYSize { get; set; }

        [Option('z', "tileZ", Required =false, Default =100, HelpText ="Z size of each tile")]
        public double TileZSize { get; set; }

        [Option('m', "maxPerTile", Required =false, Default = 100, HelpText ="Maximum number of element in each tile")]
        public int MaxElementPerTile { get; set; }

        [Option('s', "size", Required =false, Default = 10.0, HelpText ="Maximum size of output xml in MB, deprecated for now")]
        public double FileSizeLimit { get; set; }

        [Option('t', "threshold", Required =false, Default =500, HelpText ="Threshold of triangle count for a mesh to decimate")]
        public int Threshold { get; set; }

        [Option('q', "quality", Required =false, Default =0.5, HelpText ="Quality control for mesh decimate, between 0.1 ~ 1.0")]
        public double Quality { get; set; }
    }
}
