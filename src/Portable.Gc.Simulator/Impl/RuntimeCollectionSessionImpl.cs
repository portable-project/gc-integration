using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

namespace Portable.Gc.Simulator.Impl
{

    internal class RuntimeCollectionSessionImpl : IRuntimeCollectionSession
    {
        private readonly Func<BlockPtr[]> _getRoots;
        private readonly Action<BlockPtr,BlockPtr> _spliceObj;
        private readonly Action _stopReleased;

        public int RootPrioritiesCount { get; private set; }

        public RuntimeCollectionSessionImpl(Func<BlockPtr[]> getRoots, Action<BlockPtr, BlockPtr> spliceObj,Action stopReleased)
        {
            this.RootPrioritiesCount = 1;

            _getRoots = getRoots;
            _spliceObj = spliceObj;
            _stopReleased = stopReleased;
        }

        public IEnumerable<BlockPtr> GetRoots(int rootsPriority)
        {
            return _getRoots();
        }

        public void Dispose()
        {
            _stopReleased();
        }

        public void SpliceObjRef(BlockPtr oldPtr, BlockPtr newPtr)
        {
            _spliceObj(oldPtr, newPtr);
        }
    }

}
