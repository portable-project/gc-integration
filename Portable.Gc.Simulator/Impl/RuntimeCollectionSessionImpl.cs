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
        private readonly Func<IntPtr[]> _getRoots;
        private readonly Action _stopReleased;

        public int RootPrioritiesCount { get; private set; }

        public RuntimeCollectionSessionImpl(Func<IntPtr[]> getRoots, Action stopReleased)
        {
            this.RootPrioritiesCount = 1;

            _getRoots = getRoots;
            _stopReleased = stopReleased;
        }

        public IEnumerable<IntPtr> GetRoots(int rootsPriority)
        {
            return _getRoots();
        }

        public void Dispose()
        {
            _stopReleased();
        }
    }

}
