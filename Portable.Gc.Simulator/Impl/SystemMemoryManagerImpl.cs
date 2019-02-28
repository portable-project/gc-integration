using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

namespace Portable.Gc.Simulator.Impl
{
    class SystemMemoryManagerFabricImpl : IMemoryManagerFabric
    {
        public string Name { get { return "system"; } }
        public Version Version { get { return new Version("1.0"); } }

        public IMemoryManager CreateManager(IMemoryManager underlying)
        {
            return new SystemMemoryManagerImpl();
        }
    }

    class SystemMemoryManagerImpl : IMemoryManager
    {
        readonly Dictionary<IntPtr, int> _allocations = new Dictionary<IntPtr, int>();

        int _counter = 0;

        public SystemMemoryManagerImpl()
        {
        }

        public IntPtr Alloc(int size)
        {
            var ptr = Marshal.AllocHGlobal(size);

            _allocations.Add(ptr, _counter++);

            return ptr;
        }

        public void Free(IntPtr blockPtr)
        {
            _allocations.Remove(blockPtr);
            Marshal.FreeHGlobal(blockPtr);
        }

        public void Dispose()
        {
            if (_allocations.Count > 0)
                throw new ApplicationException("There are forgotten system allocations");
        }
    }
}
