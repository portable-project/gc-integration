using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

namespace Portable.Gc.Simulator.Impl
{
    internal class SystemMemoryManagerFabricImpl : IMemoryManagerFabric
    {
        public string Name { get { return "system"; } }
        public Version Version { get { return new Version("1.0"); } }

        public IMemoryManager CreateManager(IMemoryManager underlying)
        {
            return new SystemMemoryManagerImpl();
        }
    }

    internal class SystemMemoryManagerImpl : IMemoryManager
    {
        private readonly Dictionary<IntPtr, int> _allocations = new Dictionary<IntPtr, int>();
        private int _counter = 0;

        public SystemMemoryManagerImpl()
        {
        }

        public BlockPtr Alloc(int size)
        {
            var ptr = Marshal.AllocHGlobal(size);

            _allocations.Add(ptr, _counter++);

            return new BlockPtr(ptr);
        }

        public void Free(BlockPtr blockPtr)
        {
            _allocations.Remove(blockPtr.value);
            Marshal.FreeHGlobal(blockPtr.value);
        }

        public void Dispose()
        {
            if (_allocations.Count > 0)
                throw new ApplicationException("There are forgotten system allocations");
        }
    }
}
