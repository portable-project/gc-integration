using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;
using Portable.Gc.Simulator.Impl;

namespace Portable.Gc.Simulator
{

    internal class MutatorOperationInfo
    {
    }

    internal interface IMutatorContext
    {
        HashSet<ObjPtr> Statics { get; }
        RuntimeGlobalAccessorImpl Runtime { get; }
    }


    internal class MutatorContext : IMutatorContext
    {
        private readonly RuntimeGlobalAccessorImpl _runtime;
        private readonly HashSet<ObjPtr> _statics = new HashSet<ObjPtr>();
        private readonly List<Mutator> _mutators = new List<Mutator>();

        HashSet<ObjPtr> IMutatorContext.Statics { get { return _statics; } }
        RuntimeGlobalAccessorImpl IMutatorContext.Runtime { get { return _runtime; } }

        public MutatorContext(RuntimeGlobalAccessorImpl runtime)
        {
            _runtime = runtime;
        }

        public IEnumerable<ObjPtr> GetRoots()
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

    internal class Mutator : IDisposable
    {
        private class LocalCtxFrame
        {
            public LocalCtxFrame Prev { get; private set; }
            public LocalCtxFrame Next { get; private set; }

            public int Depth { get; private set; }
            public HashSet<ObjPtr> Locals { get; private set; }

            public LocalCtxFrame()
            {
                this.Prev = null;
                this.Next = null;
                this.Depth = 0;
                this.Locals = new HashSet<ObjPtr>();
            }

            public LocalCtxFrame(LocalCtxFrame prev)
            {
                this.Prev = prev;
                this.Next = null;
                this.Depth = prev.Depth + 1;
                this.Locals = new HashSet<ObjPtr>();
            }

            public LocalCtxFrame CreateNext()
            {
                var nextFrame = new LocalCtxFrame(this);
                this.Next = nextFrame;
                return nextFrame;
            }

            public LocalCtxFrame ResumePrev()
            {
                if (this.Prev != null)
                {
                    this.Prev.Next = null;
                    return this.Prev;
                }
                else
                {
                    return this;
                }
            }
        }

        private readonly IMutatorContext _ctx;
        private readonly MutatorParameters _params;
        private readonly Random _rnd;

        private readonly LocalCtxFrame _root = new LocalCtxFrame();
        private readonly RefBuffer _refBuffer = new RefBuffer();

        public Mutator(IMutatorContext ctx, int? seed, MutatorParameters parameters)
        {
            _ctx = ctx;
            _params = parameters;

            _rnd = new Random(seed ?? Environment.TickCount);

            ctx.Runtime.SpliceObjRefs += this.SpliceObject;
        }

        public IEnumerable<ObjPtr> GetRoots()
        {
            return this.GetFrames().SelectMany(f => f.Locals);
        }

        private IEnumerable<LocalCtxFrame> GetFrames()
        {
            for (var frame = _root; frame != null; frame = frame.Next)
                yield return frame;
        }

        public void SpliceObject(ObjPtr oldRef, ObjPtr newRef)
        {
            if (_ctx.Statics.Remove(oldRef))
                _ctx.Statics.Add(newRef);

            for (var f = _root; f != null; f = f.Next)
                if (f.Locals.Remove(oldRef))
                    f.Locals.Add(newRef);
        }

        public IEnumerable<(MutatorActionKind, ObjPtr)> DoWork()
        {
            for (var frame = _root; ;)
            {
                var probabilities = _params.GetParameters(frame.Depth);
                var actions = probabilities.GetActionKind(_params.Mode, _rnd);

                foreach (var actionEntry in actions.GetEnumValues().Except(new[] { MutatorActionKind.None })
                                                   .Where(a => actions.HasFlag(a))
                                                   .Select(a => (a, this.PerformAction(a, ref frame))))
                    yield return actionEntry;
            }
        }

        private ObjPtr PerformAction(MutatorActionKind actionKind, ref LocalCtxFrame frame)
        {
            ObjPtr ptr;

            switch (actionKind)
            {
                case MutatorActionKind.None:
                    ptr = ObjPtr.Zero;
                    break;
                case MutatorActionKind.Call:
                    frame = frame.CreateNext();
                    ptr = ObjPtr.Zero;
                    break;
                case MutatorActionKind.Return:
                    frame = frame.ResumePrev();
                    ptr = ObjPtr.Zero;
                    break;
                case MutatorActionKind.Newobj:
                    frame.Locals.Add(ptr = _ctx.Runtime.AllocRandomObj(_rnd));
                    break;
                case MutatorActionKind.PutStatic:
                    {
                        lock (_ctx.Statics)
                        {
                            if (frame.Locals.Count > 0)
                                _ctx.Statics.Add(ptr = frame.Locals.Skip(_rnd.Next(0, frame.Locals.Count)).First());
                            else
                                ptr = ObjPtr.Zero;
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
                                ptr = ObjPtr.Zero;
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
                                ptr = ObjPtr.Zero;
                        }
                    }
                    break;
                case MutatorActionKind.PutRef:
                    {
                        lock (_ctx.Statics)
                        {
                            ptr = this.SelectObject(frame);

                            if (ptr.value != IntPtr.Zero)
                            {
                                _refBuffer.Value = _ctx.Runtime.ObjToBlock(this.SelectObject(frame, useStatics: false)).value;

                                var refs = _ctx.Runtime.GetRefs(ptr);
                                var field = refs.FirstOrDefault(f => { f.GetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr); return _refBuffer.Value == IntPtr.Zero; });

                                if (field != null)
                                {
                                    field.SetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr);
                                }
                                else
                                {
                                    refs[_rnd.Next(0, refs.Length)].SetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr);
                                }
                            }
                        }
                    }
                    break;
                case MutatorActionKind.ChangeRef:
                    {
                        lock (_ctx.Statics)
                        {
                            ptr = this.SelectObject(frame);

                            if (ptr.value != IntPtr.Zero)
                            {
                                _refBuffer.Value = _ctx.Runtime.ObjToBlock(this.SelectObject(frame, useStatics: false)).value;

                                var refs = _ctx.Runtime.GetRefs(ptr);
                                refs[_rnd.Next(0, refs.Length)].SetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr);
                            }
                        }
                    }
                    break;
                case MutatorActionKind.EraseRef:
                    {
                        lock (_ctx.Statics)
                        {
                            ptr = this.SelectObject(frame);

                            if (ptr.value != IntPtr.Zero)
                            {
                                var refs = _ctx.Runtime.GetRefs(ptr);
                                var field = refs.FirstOrDefault(f => { f.GetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr); return _refBuffer.Value != IntPtr.Zero; });

                                _refBuffer.Value = IntPtr.Zero;
                                if (field != null)
                                    field.SetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr);
                            }
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException("Unknown action " + actionKind);
            }

            return ptr;
        }

        private ObjPtr SelectObject(LocalCtxFrame frame, bool useStatics = true)
        {
            List<ObjPtr> ptrs;
            if (useStatics) ptrs = frame.Locals.Concat(_ctx.Statics).Distinct().ToList();
            else ptrs = frame.Locals.ToList();

            if (ptrs.Count == 0)
                return ObjPtr.Zero;
            else
                return this.SelectDeepObject(ptrs[_rnd.Next(0, ptrs.Count)]);

            //if (_rnd.Next(100) > 50 && useStatics)
            //{
            //    if (_ctx.Statics.Count > 0)
            //        return this.SelectDeepObject(_ctx.Statics.Skip(_rnd.Next(0, _ctx.Statics.Count)).First());
            //    else
            //        return ObjPtr.Zero;
            //}
            //else
            //{
            //    if (frame.Locals.Count > 0)
            //        return this.SelectDeepObject(frame.Locals.Skip(_rnd.Next(0, frame.Locals.Count)).First());
            //    else
            //        return ObjPtr.Zero;
            //}
        }

        private ObjPtr SelectDeepObject(ObjPtr ptr, int depth = 0)
        {
            if (depth > 100)// || _rnd.Next(100) > 50)
                return ptr;

            var refs = _ctx.Runtime.GetRefs(ptr);
            refs[_rnd.Next(0, refs.Length)].GetValue(_ctx.Runtime.ObjToBlock(ptr), _refBuffer.buffPtr);
            return _refBuffer.Value == IntPtr.Zero ? ptr : this.SelectDeepObject(_ctx.Runtime.BlockToObj(new BlockPtr(_refBuffer.Value)), depth + 1);
        }

        public void Dispose()
        {
            _refBuffer.Dispose();
        }
    }
}