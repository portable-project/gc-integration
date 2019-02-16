using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Integration
{
    public interface IMemoryManagerFabric
    {
        IMemoryManager CreateManager(IMemoryManager underlying);
    }

    public interface IMemoryManager : IDisposable
    {
        IntPtr Alloc(int size);
        void Free(IntPtr blockPtr);
    }

    public interface IAutoMemoryManagerFabric
    {
        IAutoMemoryManagementContext CreateManagerContext(IRuntimeGlobalAccessor runtimeInfoAccessor);
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

    public interface IMemManIntegration
    {
        // TODO: compiler integration
        // Func<object> GetCompilerStageFabric();

        // called by runtime to inject gc-related fields into the object structure layout
        void AugmentObjectLayout(INativeStructureBuilder structureBuilder);
    }
}
