﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Integration
{
    public interface IRuntimeContextAccessor
    {
        bool IsRunning { get; }

        void RequestStop(Action callback);

        // IRuntimeCollectionSession BeginCollection(ICollectionSessionAccessor collectionSessionAccessor);

        IRuntimeCollectionSession BeginCollection();
    }

    public interface IRuntimeCollectionSession : IDisposable
    {
        int RootPrioritiesCount { get; }

        IEnumerable<BlockPtr> GetRoots(int rootsPriority);
        void SpliceObjRef(BlockPtr oldPtr, BlockPtr newPtr);
    }

    public interface IRuntimeGlobalAccessor : IRuntimeContextAccessor
    {
        INativeStructureLayoutInfo GetLayoutInfo(BlockPtr blockPtr);
        INativeStructureLayoutInfo GetDefaultLayoutInfo();
    }

    public interface INativeStructureLayoutInfo
    {
        string Name { get; }
        int DataSize { get; }
        int AlignedSize { get; }

        IReadOnlyList<INativeStructureFieldInfo> Fields { get; }
    }

    public interface INativeStructureFieldInfo
    {
        int Number { get; }
        string Name { get; }

        int Offset { get; }
        int Size { get; }

        int BitIndex { get; }
        int BitsCount { get; }

        bool IsReference { get; }

        void GetValue(BlockPtr blockPtr, IntPtr buffer);
        void SetValue(BlockPtr blockPtr, IntPtr buffer);
    }

    public interface INativeStructureBuilder
    {
        INativeStructureFieldBuilder DefineField(string name);
    }

    public interface INativeStructureFieldBuilder
    {
        int Number { get; }
        string Name { get; }

        int? Offset { get; set; }
        int Size { get; set; }
        int? Alignment { get; set; }

        int? BitIndex { get; set; }
        int BitsCount { get; set; }

        bool IsReference { get; set; }
    }
}
