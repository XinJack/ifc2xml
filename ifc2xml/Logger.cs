using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2xml
{
    /// <summary>
    /// Simple logger wrapper for Console
    /// </summary>
    class Logger
    {
        /// <summary>
        /// Log Error message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void Error(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] " + format, args);
            Console.ResetColor();
        }

        /// <summary>
        /// Log warnning message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void Warn(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(format, args);
            Console.ResetColor();
        }

        /// <summary>
        /// Log normal info
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void Info(string format, params object[] args)
        {
            Console.WriteLine("[INFO] " + format, args);
        }
    }
}
