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
        BlockPtr Alloc(int size);
        void Free(BlockPtr blockPtr);
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
        void OnWriteRefMember(BlockPtr blockPtr, BlockPtr refPtr);

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

    public struct BlockPtr : IComparable<BlockPtr>
    {
        public readonly IntPtr value;

        public BlockPtr(IntPtr value)
        {
            this.value = value;
        }

        public int CompareTo(BlockPtr other)
        {
            return this.value.ToInt64().CompareTo(other.value.ToInt64());
        }

        public override bool Equals(object obj)
        {
            return obj is BlockPtr other ? this.value.Equals(other.value) : false;
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public override string ToString()
        {
            return "O#" + this.value.ToString();
        }
    }
}
