using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Simulator.Impl;

namespace Portable.Gc.Simulator
{

    internal class MutatorOperationInfo
    {
    }

    internal interface IMutatorContext
    {
        HashSet<IntPtr> Statics { get; }
        RuntimeGlobalAccessorImpl Runtime { get; }
    }


    internal class MutatorContext : IMutatorContext
    {
        private readonly RuntimeGlobalAccessorImpl _runtime;
        private readonly HashSet<IntPtr> _statics = new HashSet<IntPtr>();
        private readonly List<Mutator> _mutators = new List<Mutator>();

        HashSet<IntPtr> IMutatorContext.Statics { get { return _statics; } }
        RuntimeGlobalAccessorImpl IMutatorContext.Runtime { get { return _runtime; } }

        public MutatorContext(RuntimeGlobalAccessorImpl runtime)
        {
            _runtime = runtime;
        }

        public IEnumerable<IntPtr> GetRoots()
        {
            return _mutators.SelectMany(m => m.GetRoots()).Concat(_statics);
        }

        public Mutator CreateMutator(int? seed, MutatorParameters parameters)
        {
            var mutator = new Mutator(this, seed, parameters);
            _mutators.Add(mutator);
            return mutator;
        }
    }

    internal class Mutator
    {
        private class LocalCtxFrame
        {
            public LocalCtxFrame Prev { get; private set; }
            public LocalCtxFrame Next { get; private set; }

            public int Depth { get; private set; }
            public HashSet<IntPtr> Locals { get; private set; }

            public LocalCtxFrame()
            {
                this.Prev = null;
                this.Next = null;
                this.Depth = 0;
                this.Locals = new HashSet<IntPtr>();
            }

            public LocalCtxFrame(LocalCtxFrame prev)
            {
                this.Prev = prev;
                this.Next = null;
                this.Depth = prev.Depth + 1;
                this.Locals = new HashSet<IntPtr>();
            }

            public LocalCtxFrame CreateNext()
            {
                var nextFrame = new LocalCtxFrame(this);
                this.Next = nextFrame;
                return nextFrame;
            }

            public LocalCtxFrame ResumePrev()
            {
                this.Prev.Next = null;
                return this.Prev;
            }
        }

        private readonly IMutatorContext _ctx;
        private readonly MutatorParameters _params;
        private readonly Random _rnd;

        private readonly LocalCtxFrame _root = new LocalCtxFrame();

        public Mutator(IMutatorContext ctx, int? seed, MutatorParameters parameters)
        {
            _ctx = ctx;
            _params = parameters;

            _rnd = new Random(seed ?? Environment.TickCount);
        }

        public IEnumerable<IntPtr> GetRoots()
        {
            return this.GetFrames().SelectMany(f => f.Locals);
        }

        private IEnumerable<LocalCtxFrame> GetFrames()
        {
            for (var frame = _root; frame != null; frame = frame.Next)
                yield return frame;
        }

        public IEnumerable<(MutatorActionKind, IntPtr)> DoWork()
        {
            for (var frame = _root; ;)
            {
                var probabilities = _params.GetParameters(frame.Depth);
                var actions = probabilities.GetActionKind(_params.Mode, _rnd);

                foreach (var actionEntry in actions.GetEnumValues()
                                                   .Where(a => actions.HasFlag(a))
                                                   .Select(a => (a, this.PerformAction(a, ref frame))))
                    yield return actionEntry;
            }
        }

        private IntPtr PerformAction(MutatorActionKind actionKind, ref LocalCtxFrame frame)
        {
            IntPtr ptr;

            switch (actionKind)
            {
                case MutatorActionKind.None:
                    ptr = IntPtr.Zero;
                    break;
                case MutatorActionKind.Call:
                    frame = frame.CreateNext();
                    ptr = IntPtr.Zero;
                    break;
                case MutatorActionKind.Return:
                    frame = frame.ResumePrev();
                    ptr = IntPtr.Zero;
                    break;
                case MutatorActionKind.Newobj:
                    cnt1++;
                    frame.Locals.Add(ptr = _ctx.Runtime.AllocRandomObj(_rnd));
                    break;
                case MutatorActionKind.PutStatic:
                    {
                        lock (_ctx.Statics)
                        {
                            if (frame.Locals.Count > 0)
                                _ctx.Statics.Add(ptr = frame.Locals.Skip(_rnd.Next(0, frame.Locals.Count)).First());
                            else
                                ptr = IntPtr.Zero;
                        }
                    }
                    break;
                case MutatorActionKind.ChangeStatic:
                    {
                        lock (_ctx.Statics)
                        {
                            if (_ctx.Statics.Count > 0 && frame.Locals.Count > 0)
                            {
                                _ctx.Statics.Remove(_ctx.Statics.Skip(_rnd.Next(0, _ctx.Statics.Count)).First());
                                _ctx.Statics.Add(ptr = frame.Locals.Skip(_rnd.Next(0, frame.Locals.Count)).First());
                            }
                            else
                            {
                                ptr = IntPtr.Zero;
                            }
                        }
                    }
                    break;
                case MutatorActionKind.EraseStatic:
                    {
                        lock (_ctx.Statics)
                        {
                            if (_ctx.Statics.Count > 0)
                                _ctx.Statics.Remove(ptr = _ctx.Statics.Skip(_rnd.Next(0, _ctx.Statics.Count)).First());
                            else
                                ptr = IntPtr.Zero;
                        }
                    }
                    break;
                case MutatorActionKind.PutRef:
                case MutatorActionKind.ChangeRef:
                case MutatorActionKind.EraseRef:
                    ptr = IntPtr.Zero;
                    cnt2++;
                    break; // TODO: operations with reference fields
                default:
                    throw new NotImplementedException("Unknown action " + actionKind);
            }

            return ptr;
        }

        static int cnt1,cnt2;
    }
}
