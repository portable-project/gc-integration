using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Integration
{
    [System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class ExportMemoryManagerAttribute : Attribute
    {
        public Type FabricType { get; private set; }

        public ExportMemoryManagerAttribute(Type fabricType)
        {
            this.FabricType = fabricType;
        }
    }
}
