using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

[assembly: ExportMemoryManager(typeof(Portable.Gc.Example.AutoMemoryManagerFabricImpl))]

namespace Portable.Gc.Example
{
    public class AutoMemoryManagerFabricImpl : IAutoMemoryManagerFabric
    {
        public string Name { get; }
        public Version Version { get; }

        public IAutoMemoryManagementContext CreateManagerContext(IRuntimeGlobalAccessor runtimeGlobalAccessor)
        {
            return new AutoMemoryManagementContextImpl(runtimeGlobalAccessor);
        }
    }

    public class AutoMemoryManagementContextImpl : IAutoMemoryManagementContext
    {
        public IMemManIntegration Integration { get; }

        public AutoMemoryManagementContextImpl(IRuntimeGlobalAccessor runtimeGlobalAccessor)
        {
        }

        public IAutoMemoryManager CreateManager(IMemoryManager underlying, IRuntimeContextAccessor runtimeAccessor)
        {
            return new AutoMemoryManagerImpl(underlying);
        }

        public void Dispose()
        {
        }
    }

    public class AutoMemoryManagerImpl : IAutoMemoryManager
    {
        readonly IMemoryManager _underlying;

        public AutoMemoryManagerImpl(IMemoryManager underlying)
        {
        }

        public IntPtr Alloc(int size)
        {
            return _underlying.Alloc(size);
        }

        public void ForceCollection(int generation)
        {
        }

        public void Free(IntPtr blockPtr)
        {
            _underlying.Free(blockPtr);
        }

        public void OnWriteRefMember(IntPtr blockPtr, IntPtr refPtr)
        {
        }

        public void Dispose()
        {
        }
    }
}
