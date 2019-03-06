﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

namespace Portable.Gc.Simulator.Impl
{
    internal unsafe class RuntimeGlobalAccessorImpl : IRuntimeGlobalAccessor, INativeLayoutContext, IDisposable
    {
        public bool IsRunning { get; }

        private readonly SystemMemoryManagerImpl _systemMemoryManager = new SystemMemoryManagerImpl();
        private readonly IAutoMemoryManagementContext _gcCtx;
        private readonly NativeStructureLayoutInfoImpl _objectLayout;
        private readonly INativeStructureFieldInfo _typeIdFieldInfo;
        private readonly Dictionary<IntPtr, INativeStructureLayoutInfo> _knownTypes = new Dictionary<IntPtr, INativeStructureLayoutInfo>();
        private readonly IAutoMemoryManager _memoryManager;

        public event Action StopRequested = delegate { };
        public event Action StopReleased = delegate { };
        public event Func<ObjPtr[]> GetRoots = delegate { return new ObjPtr[0]; };

        int INativeLayoutContext.ObjRefDiff { get { return _typeIdFieldInfo.Offset; } }

        public RuntimeGlobalAccessorImpl(IAutoMemoryManagerFabric gcFabric)
        {
            var stb = new NativeStructureBuilderImpl(this, "object");

            _gcCtx = gcFabric.CreateManagerContext(this);
            if (_gcCtx.Integration != null)
                _gcCtx.Integration.AugmentObjectLayout(stb);

            stb.UseDefaultFieldAlignment = true;
            stb.FieldsAligmnent = null;
            stb.StructureAlignment = null;

            var typeIdFieldBuilder = stb.DefineField("typeId");
            typeIdFieldBuilder.Size = IntPtr.Size;

            _objectLayout = stb.Complete();
            _typeIdFieldInfo = _objectLayout.Fields.First(f => f.Number == typeIdFieldBuilder.Number);

            this.GenerateTypes(stb);

            _memoryManager = _gcCtx.CreateManager(_systemMemoryManager, this);
        }

        private void GenerateTypes(NativeStructureBuilderImpl objectStb)
        {
            _knownTypes.Add(new IntPtr(_knownTypes.Count), _objectLayout);

            var fieldSize = new[] { 1, 2, 4, 8, 16, 32 };

            var rnd = new Random(); // TODO: use external seed
            var count = 1000;
            for (int i = 1; i < count; i++)
            {
                var stb = new NativeStructureBuilderImpl(this, "r" + i, objectStb);

                var fieldsCount = rnd.Next(0, 40);
                for (int j = 0; j < fieldsCount; j++)
                {
                    var f = stb.DefineField("r" + i + "f" + j);

                    if (rnd.Next(0, 100) < 50)
                    {
                        f.IsReference = true;
                    }
                    else
                    {
                        f.Size = fieldSize[rnd.Next(0, fieldSize.Length)];
                    }
                }

                _knownTypes.Add(new IntPtr(_knownTypes.Count), stb.Complete());
            }
        }

        public ObjPtr AllocRandomObj(Random rnd)
        {
            var typeIdValue = rnd.Next(0, _knownTypes.Count);
            return this.AllocImpl(new IntPtr(typeIdValue));
        }

        private ObjPtr AllocImpl(IntPtr typeId)
        {
            var layoutInfo = this.GetLayoutInfo(typeId);

            var blockPtr = _memoryManager.Alloc(layoutInfo.AlignedSize);
            WinApi.RtlZeroMemory(blockPtr.value, new IntPtr(layoutInfo.AlignedSize));
            var objPtr = new ObjPtr(blockPtr.value + _typeIdFieldInfo.Offset);

            return objPtr;
        }

        private INativeStructureLayoutInfo GetLayoutInfo(IntPtr typeId)
        {
            if (!_knownTypes.TryGetValue(typeId, out var layoutInfo))
                throw new InvalidOperationException("Unknown block typeId " + typeId);

            return layoutInfo;
        }

        INativeStructureLayoutInfo IRuntimeGlobalAccessor.GetLayoutInfo(BlockPtr blockPtr)
        {
            var ptr = stackalloc IntPtr[1];
            _typeIdFieldInfo.GetValue(blockPtr, new IntPtr(ptr));
            return this.GetLayoutInfo(ptr[0]);
        }

        IRuntimeCollectionSession IRuntimeContextAccessor.BeginCollection()
        {
            return new RuntimeCollectionSessionImpl(() => this.GetRoots().Select(p => new BlockPtr(p.value - _typeIdFieldInfo.Offset)).ToArray(), this.StopReleased);
        }

        void IRuntimeContextAccessor.RequestStop(Action callback)
        {
            this.StopRequested();
            callback();
        }

        public void Dispose()
        {
            _gcCtx.Dispose();
        }

        INativeStructureLayoutInfo IRuntimeGlobalAccessor.GetDefaultLayoutInfo()
        {
            return _objectLayout;
        }
    }
}