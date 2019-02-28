using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

namespace Portable.Gc.Simulator.Impl
{
    internal class NativeStructureLayoutInfoImpl : INativeStructureLayoutInfo
    {
        public string Name { get; }

        public int DataSize { get; }
        public int AlignedSize { get; }
        public IReadOnlyCollection<INativeStructureFieldInfo> Fields { get; }

        public NativeStructureLayoutInfoImpl(string name, int? structureSizeAlignment, IReadOnlyCollection<INativeStructureFieldInfo> fields)
        {
            this.Name = name;
            this.Fields = fields;

            this.DataSize = fields.Max(f => f.Offset + f.Size);
            this.AlignedSize = structureSizeAlignment.HasValue ? this.DataSize.AlignTo(structureSizeAlignment.Value) : this.DataSize;
        }
    }

    internal class NativeStructureFieldInfoImpl : INativeStructureFieldInfo
    {
        public int Number { get; }
        public string Name { get; }

        public int Offset { get; }
        public int Size { get; }
        public int BitIndex { get; }
        public int BitsCount { get; }
        public bool IsReference { get; }

        public NativeStructureFieldInfoImpl(int number, string name, int offset, int size, int bitIndex, int bitsCount, bool isReference)
        {
            this.Number = number;
            this.Name = name;
            this.Offset = offset;
            this.Size = size;
            this.BitIndex = bitIndex;
            this.BitsCount = bitsCount;
            this.IsReference = isReference;
        }

        public void GetValue(IntPtr blockPtr, IntPtr buffer)
        {
            if (this.BitIndex == 0)
            {
                WinApi.CopyMemory(buffer, blockPtr + this.Offset, this.Size);
            }
            else
            {
                var buff = new byte[this.Size];
                Marshal.Copy(blockPtr + this.Offset, buff, 0, this.Size);
                var rawValue = new BigInteger(buff);
                var valueMask = BigInteger.Pow(2, this.BitsCount) - 1;

                var value = (rawValue >> (this.BitIndex - this.BitsCount)) & valueMask;

                var valueBytes = value.ToByteArray();
                Marshal.Copy(valueBytes, 0, buffer, valueBytes.Length);
            }
        }

        public void SetValue(IntPtr blockPtr, IntPtr buffer)
        {
            if (this.BitIndex == 0)
            {
                WinApi.CopyMemory(blockPtr + this.Offset, buffer, this.Size);
            }
            else
            {
                var buff = new byte[this.Size];
                Marshal.Copy(blockPtr + this.Offset, buff, 0, this.Size);
                var rawValue = new BigInteger(buff);
                var valueMask = BigInteger.Pow(2, this.BitsCount) - 1;

                var valueBytes = new byte[this.Size];
                Marshal.Copy(buffer, valueBytes, 0, valueBytes.Length);
                var value = new BigInteger(valueBytes);

                rawValue = (rawValue ^ (rawValue & (valueMask << this.BitIndex))) | (value << this.BitIndex);
                buff = rawValue.ToByteArray();
                Marshal.Copy(buff, 0, blockPtr + this.Offset, this.Size);
            }
        }
    }
}
