using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Integration
{
    public interface IMemoryManagerFabric
    {
        string Name { get; }
        Version Version { get; }

        IMemoryManager CreateManager(IMemoryManager underlying);
    }

    public interface IMemoryManager : IDisposable
    {
        IntPtr Alloc(int size);
        void Free(IntPtr blockPtr);
    }

    public interface IAutoMemoryManagerFabric
    {
        string Name { get; }
        Version Version { get; }

        IAutoMemoryManagementContext CreateManagerContext(IRuntimeGlobalAccessor runtimeGlobalAccessor);
    }

    public interface IAutoMemoryManagementContext : IDisposable
    {
        // accessed only at run time
        IAutoMemoryManager CreateManager(IMemoryManager underlying, IRuntimeContextAccessor runtimeAccessor);

        // can be accessed at AOT compilation time
        IMemManIntegration Integration { get; }
    }

    public interface IAutoMemoryManager : IMemoryManager
    {
        void OnWriteRefMember(IntPtr blockPtr, IntPtr refPtr);

        void ForceCollection(int generation);
    }

    //public interface ICollectionSessionAccessor
    //{
    //    bool IsAlive(IntPtr blockPtr);
    //}

    public interface IMemManIntegration
    {
        // TODO: compiler integration
        // Func<object> GetCompilerStageFabric();

        // called by runtime to inject gc-related fields into the object structure layout
        void AugmentObjectLayout(INativeStructureBuilder structureBuilder);
    }
}
