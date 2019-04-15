using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Portable.Gc.Integration;
using Portable.Gc.Simulator.Impl;

namespace Portable.Gc.Simulator
{

    public struct ObjPtr : IComparable<ObjPtr>
    {
        public readonly IntPtr value;

        public readonly static ObjPtr Zero = new ObjPtr(IntPtr.Zero);

        public ObjPtr(IntPtr value)
        {
            this.value = value;
        }

        public int CompareTo(ObjPtr other)
        {
            return this.value.ToInt64().CompareTo(other.value.ToInt64());
        }

        public override bool Equals(object obj)
        {
            return obj is ObjPtr other ? this.value.Equals(other.value) : false;
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public override string ToString()
        {
            return "O#" + Convert.ToString(this.value.ToInt64(), 16);
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var fileInfo = new FileInfo(args[0]);
                var asm = Assembly.LoadFile(fileInfo.FullName);
                var gcFabrics = asm.GetCustomAttributes<ExportMemoryManagerAttribute>()
                                   .Select(a => a.FabricType?.GetConstructor(Type.EmptyTypes))
                                   .Where(c => c != null && c.DeclaringType.GetInterfaces().Any(i => i == typeof(IAutoMemoryManagerFabric)))
                                   .Select(c => c.Invoke(null))
                                   .OfType<IAutoMemoryManagerFabric>()
                                   .ToArray();

                if (args.Length > 1)
                {
                    var gcName = args[1];
                    var gcToUse = gcFabrics.FirstOrDefault(gc => gc.Name == gcName);
                    if (gcToUse == null)
                    {
                        Console.WriteLine("There is no GC with name " + gcName);
                        PrintGcInfo(gcFabrics);
                    }
                    else
                    {
                        DoWork(gcToUse);
                    }
                }
                else if (gcFabrics.Length == 1)
                {
                    DoWork(gcFabrics.First());
                }
                else
                {
                    PrintGcInfo(gcFabrics);
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("\t" + typeof(Program).Assembly.ManifestModule.Name + " <GC Assembly file name> [GC name]");
                Console.WriteLine();
            }
        }

        private static void PrintGcInfo(IAutoMemoryManagerFabric[] gcFabrics)
        {
            Console.WriteLine("Available GCs:");

            foreach (var gc in gcFabrics)
                Console.WriteLine(gc.Name);
        }

        private static void DoWork(IAutoMemoryManagerFabric gcFabric)
        {
            Console.WriteLine("Using GC " + gcFabric.Name);

            //var p = new MutatorParameters() {
            //    // stackDepth, 
            //    // |   callProbability, 
            //    // |   |    returnProbability, 
            //    // |   |    |     newobjProbability, 
            //    // |   |    |     |   putStatic, 
            //    // |   |    |     |   |   changeStatic, 
            //    // |   |    |     |   |   |   eraseStatic, 
            //    // |   |    |     |   |   |   |   putRefProbability, 
            //    // |   |    |     |   |   |   |   |   changeRefProbability, 
            //    // |   |    |     |   |   |   |   |   |   eraseRefProbabilty
            //    // |   |    |     |   |   |   |   |   |   |
            //    { 0,   100, 0,    75, 10, 10, 10, 90, 90, 90 },
            //    { 100, 100, 100,  75, 10, 10, 10, 90, 90, 90 }
            //};
            //p.Mode = MutatorParametersModeKind.Sequence;

            var p = new MutatorParameters() {
                // stackDepth, 
                // |   callProbability, 
                // |   |    returnProbability, 
                // |   |    |     newobjProbability, 
                // |   |    |     |   putStatic, 
                // |   |    |     |   |   changeStatic, 
                // |   |    |     |   |   |   eraseStatic, 
                // |   |    |     |   |   |   |   putRefProbability, 
                // |   |    |     |   |   |   |   |   changeRefProbability, 
                // |   |    |     |   |   |   |   |   |   eraseRefProbabilty
                // |   |    |     |   |   |   |   |   |   |
                { 0,   20,  00,   20, 10, 10, 10, 10, 10, 10 },
                { 100, 10,  10,   20, 10, 10, 10, 10, 10, 10 },
            };
            p.Mode = MutatorParametersModeKind.Flat;


            using (var runtime = new RuntimeGlobalAccessorImpl(gcFabric))
            {
                var mutatorCtx = new MutatorContext(runtime);
                var m = mutatorCtx.CreateMutator(null, p);

                var mutatorWorkingEv = new ManualResetEvent(true);

                runtime.GetRoots += () => m.GetRoots().ToArray();
                runtime.StopRequested += () => mutatorWorkingEv.Reset();
                runtime.StopReleased += () => mutatorWorkingEv.Set();

                var mutatorThread = new Thread(() => {
                    foreach (var item in m.DoWork())
                    {
                        Console.WriteLine(item);
                        mutatorWorkingEv.WaitOne();
                    }
                });

                mutatorThread.Start();

                mutatorThread.Join();
            }
        }
    }
}
