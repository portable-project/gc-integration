using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Gc.Integration;

namespace Portable.Gc.Simulator.Impl
{
    internal class NativeStructureBuilderImpl : INativeStructureBuilder
    {
        private readonly List<NativeStructureFieldBuilderImpl> _fields;
        private readonly INativeLayoutContext _ctx;

        public string StructureName { get; private set; }

        public bool UseDefaultFieldAlignment { get; set; }
        public int? FieldsAligmnent { get; set; }
        public int? StructureAlignment { get; set; }

        public NativeStructureBuilderImpl(INativeLayoutContext ctx, string structureName)
        {
            _ctx = ctx;

            this.StructureName = structureName;
            this.UseDefaultFieldAlignment = true;
            this.FieldsAligmnent = null;
            this.StructureAlignment = null;

            _fields = new List<NativeStructureFieldBuilderImpl>();
        }

        public NativeStructureBuilderImpl(INativeLayoutContext ctx, string structureName, NativeStructureBuilderImpl other)
        {
            _ctx = ctx;

            this.StructureName = structureName;
            this.UseDefaultFieldAlignment = other.UseDefaultFieldAlignment;
            this.FieldsAligmnent = other.FieldsAligmnent;
            this.StructureAlignment = other.StructureAlignment;

            _fields = new List<NativeStructureFieldBuilderImpl>(other._fields);
        }

        public INativeStructureFieldBuilder DefineField(string name)
        {
            var fieldBuilder = new NativeStructureFieldBuilderImpl(_fields.Count, name);
            _fields.Add(fieldBuilder);

            return fieldBuilder;
        }

        public NativeStructureLayoutInfoImpl Complete()
        {
            // TODO: merge bit fields

            var fields = new List<NativeStructureFieldInfoImpl>(_fields.Count);
            var off = 0;

            foreach (var item in _fields)
            {
                if (item.Offset.HasValue)
                    throw new NotImplementedException();
                if (item.BitIndex.HasValue)
                    throw new NotImplementedException();

                if (item.IsReference)
                {
                    if (item.BitIndex.HasValue || item.BitsCount != 0 || item.Size != 0)
                        throw new ArgumentOutOfRangeException();

                    item.Size = IntPtr.Size;
                }
                else
                {
                    if (item.BitIndex.HasValue && (item.BitIndex - item.BitsCount < 0) ||
                        item.BitIndex.HasValue && (item.BitIndex >= item.Size * 8) ||
                        (item.Size <= 0 && item.BitsCount <= 0))
                        throw new ArgumentOutOfRangeException();
                }

                var size = item.BitsCount == 0 ? item.Size : (item.BitsCount - 1 / 8) + 1;
                if (item.Alignment.HasValue)
                    off = off.AlignTo(item.Alignment.Value);
                if (this.UseDefaultFieldAlignment)
                    off = off.AlignTo(size);
                else if (this.FieldsAligmnent.HasValue)
                    off = off.AlignTo(this.FieldsAligmnent.Value);

                fields.Add(new NativeStructureFieldInfoImpl(_ctx, item.Number, item.Name, off, size, 0, item.Size > 0 ? 0 : item.BitsCount, item.IsReference));

                off += size;
            }

            return new NativeStructureLayoutInfoImpl(this.StructureName, this.StructureAlignment, new ReadOnlyCollection<NativeStructureFieldInfoImpl>(fields));
        }
    }

    internal class NativeStructureFieldBuilderImpl : INativeStructureFieldBuilder
    {
        public int Number { get; private set; }
        public string Name { get; private set; }

        public int? Offset { get; set; }
        public int Size { get; set; }
        public int? BitIndex { get; set; }
        public int BitsCount { get; set; }
        public int? Alignment { get; set; }

        public bool IsReference { get; set; }

        public NativeStructureFieldBuilderImpl(int number, string name)
        {
            this.Number = number;
            this.Name = name;
        }
    }
}
