using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Portable.Gc.Integration;
using Portable.Gc.Simulator.Impl;

namespace Portable.Gc.Simulator
{

    public struct ObjPtr : IComparable<ObjPtr>
    {
        public readonly IntPtr value;

        public static readonly ObjPtr Zero = new ObjPtr(IntPtr.Zero);

        public ObjPtr(IntPtr value)
        {
            this.value = value;
        }

        public int CompareTo(ObjPtr other)
        {
            return value.ToInt64().CompareTo(other.value.ToInt64());
        }

        public override bool Equals(object obj)
        {
            return obj is ObjPtr other ? value.Equals(other.value) : false;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return "O#" + Convert.ToString(value.ToInt64(), 16);
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var options = new CommandLineArgs();
            var argsAnalyzer = new CommandLineAnalyzer<CommandLineArgs>();

            if (argsAnalyzer.TryParse(args, options) && !options.Help)
            {
                if (argsAnalyzer.AllDefaultArgsFound)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        DoWork(options);
                    }
                    else
                    {
                        try
                        {
                            DoWork(options);
                        }
                        catch (Exception ex)
                        {
                            PrepareExceptionInfo(ex, options.Verbose);
                        }
                    }
                }
                else
                {
                    Console.WriteLine(argsAnalyzer.ErrorMessage);
                }
            }
            else
            {
                Console.WriteLine(argsAnalyzer.MakeHelp());
            }
        }

        private static void PrepareExceptionInfo(Exception ex, bool verbose)
        {
            Func<string, int, string> tabMsg = (msg, n) => string.Join(
                 Environment.NewLine,
                 msg.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => new string(' ', n) + s)
            );

            do
            {
                Console.Error.WriteLine(ex.Message);
                if (verbose)
                    Console.Error.WriteLine(tabMsg(ex.StackTrace, 15));

                ex = ex.InnerException;
            } while (ex != null);

            // Log.Exception(ex);
            Environment.ExitCode = -1;
        }

        private static T Load<T>(string filePath)
        {
            var xs = new XmlSerializer(typeof(T));
            using (var stream = File.OpenRead(filePath))
                return (T)xs.Deserialize(stream);
        }

        private static void DoWork(CommandLineArgs options)
        {
            var cfg = Load<GcSimulatorConfigurationType>(options.Configuration);

            var fileInfo = new FileInfo(options.AssemblyLocation);
            var asm = Assembly.LoadFile(fileInfo.FullName);
            var gcFabrics = asm.GetCustomAttributes<ExportMemoryManagerAttribute>()
                               .Select(a => a.FabricType?.GetConstructor(Type.EmptyTypes))
                               .Where(c => c != null && c.DeclaringType.GetInterfaces().Any(i => i == typeof(IAutoMemoryManagerFabric)))
                               .Select(c => c.Invoke(null))
                               .OfType<IAutoMemoryManagerFabric>()
                               .ToArray();

            if (!string.IsNullOrWhiteSpace(options.GcName))
            {
                var gcName = options.GcName;
                var gcToUse = gcFabrics.FirstOrDefault(gc => gc.Name == gcName);
                if (gcToUse == null)
                {
                    Console.WriteLine("There is no GC with name " + gcName);
                    PrintGcInfo(gcFabrics);
                }
                else
                {
                    DoWork(gcToUse, cfg);
                }
            }
            else if (gcFabrics.Length == 1)
            {
                DoWork(gcFabrics.First(), cfg);
            }
            else
            {
                PrintGcInfo(gcFabrics);
            }
        }

        private static void PrintGcInfo(IAutoMemoryManagerFabric[] gcFabrics)
        {
            Console.WriteLine("Available GCs:");

            foreach (var gc in gcFabrics)
                Console.WriteLine(gc.Name);
        }

        private static void DoWork(IAutoMemoryManagerFabric gcFabric, GcSimulatorConfigurationType cfg)
        {
            Console.WriteLine("Using GC " + gcFabric.Name);

            var p = new MutatorParameters();

            foreach (var item in cfg.Probabilities.Items.OrderBy(e => e.StackDepth))
            {
                var entry = new MutatorParametersEntry();
                entry.StackDepth = item.StackDepth; 

                foreach (var attr in item.AnyAttr)
                {
                    if (Enum.TryParse<MutatorActionKind>(attr.LocalName, true, out var actionKind) && int.TryParse(attr.Value, out var value))
                        entry.SetValue(actionKind, value);
                }

                p.Add(entry);
            }

            ////var p = new MutatorParameters() {
            ////    // stackDepth, 
            ////    // |   callProbability, 
            ////    // |   |    returnProbability, 
            ////    // |   |    |     newobjProbability, 
            ////    // |   |    |     |   putStatic, 
            ////    // |   |    |     |   |   changeStatic, 
            ////    // |   |    |     |   |   |   eraseStatic, 
            ////    // |   |    |     |   |   |   |   putRefProbability, 
            ////    // |   |    |     |   |   |   |   |   changeRefProbability, 
            ////    // |   |    |     |   |   |   |   |   |   eraseRefProbabilty
            ////    // |   |    |     |   |   |   |   |   |   |
            ////    { 0,   100, 0,    75, 10, 10, 10, 90, 90, 90 },
            ////    { 100, 100, 100,  75, 10, 10, 10, 90, 90, 90 }
            ////};
            ////p.Mode = MutatorParametersModeKind.Sequence;

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
            //    { 0,   20,  00,   20, 10, 10, 10, 10, 10, 10 },
            //    { 100, 10,  10,   20, 10, 10, 10, 10, 10, 10 },
            //};
            //p.Mode = MutatorParametersModeKind.Flat;


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
