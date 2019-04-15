using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Simulator
{
    public class CommandLineArgs
    {
        [DefaultArg(0)]
        [ArgDescription("Assembly file path")]
        public string AssemblyLocation { get; set; }

        [DefaultArg(1)]
        [ArgDescription("Simulator configuration file path")]
        public string Configuration { get; set; }

        [ArgAlias("gc")]
        [ArgDescription("Name of the GC implementation")]
        public string GcName{ get; set; }

        [ArgAlias("v")]
        [ArgDescription("Be verbose on exceptions.")]
        public bool Verbose { get; set; }

        [ArgAlias("h")]
        [ArgDescription("Shows this help.")]
        public bool Help { get; set; }
    }
}
