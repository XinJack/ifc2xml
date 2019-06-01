using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2xml
{
    /// <summary>
    /// CommandLineParser options
    /// </summary>
    class Options
    {
        [Option('i', "input", Required =true, HelpText ="The path of ifc file")]
        public string IfcFilePath { get; set; }
    }
}
