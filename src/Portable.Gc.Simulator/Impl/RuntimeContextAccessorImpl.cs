//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Portable.Gc.Integration;

//namespace Portable.Gc.Simulator.Impl
//{

//    internal class RuntimeContextAccessorImpl : IRuntimeContextAccessor
//    {
//        public bool IsRunning { get; private set; }

//        public RuntimeContextAccessorImpl()
//        {
//        }

//        public IRuntimeCollectionSession BeginCollection()
//        {
//            return new RuntimeCollectionSessionImpl();
//        }

//        public void RequestStop(Action callback)
//        {
//            throw new NotImplementedException();
//        }
//    }

//}
