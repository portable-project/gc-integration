using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Simulator
{
    [System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    internal sealed class MutatorParamAttribute : Attribute
    {
        public int Order { get; private set; }
        public MutatorActionKind ActionKind { get; private set; }

        public MutatorParamAttribute(int order, MutatorActionKind actionKind)
        {
            this.Order = order;
            this.ActionKind = actionKind;
        }
    }

    public enum MutatorParametersModeKind
    {
        // [...|...|...]
        Flat,
        // [...]
        // [.......]
        // [...........]
        Override,
        // [...]
        //     [...]
        //         [...]
        Sequence
    }

    [Flags]
    public enum MutatorActionKind
    {
        None = 0,

        Call = 0b00000001,
        Return = 0b00000010,

        Newobj = 0b000000100,

        PutStatic = 0b000001000,
        ChangeStatic = 0b000010000,
        EraseStatic = 0b000100000,

        PutRef = 0b001000000,
        ChangeRef = 0b010000000,
        EraseRef = 0b100000000,
    }

    public class MutatorParametersEntry
    {
        private static readonly List<(Func<MutatorParametersEntry, int> getter, Action<MutatorParametersEntry, int> setter, MutatorActionKind actionKind)> _knownParams;

        static MutatorParametersEntry()
        {
            var t = typeof(MutatorParametersEntry);
            _knownParams = t.GetProperties(BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.Instance)
                            .Select(p => (prop: p, attr: p.GetCustomAttribute<MutatorParamAttribute>()))
                            .Where(p => p.attr != null)
                            .OrderBy(p => p.attr.Order)
                            .Select(p => (
                                p.prop.GetMethod.CreateDelegate<Func<MutatorParametersEntry, int>>(),
                                p.prop.SetMethod.CreateDelegate<Action<MutatorParametersEntry, int>>(),
                                p.attr.ActionKind
                            )).ToList();
        }

        public int StackDepth { get; set; }

        [MutatorParam(0, MutatorActionKind.Call)]
        public int CallProbability { get; set; }
        [MutatorParam(1, MutatorActionKind.Return)]
        public int ReturnProbability { get; set; }
        [MutatorParam(2, MutatorActionKind.Newobj)]
        public int NewobjProbability { get; set; }

        [MutatorParam(3, MutatorActionKind.PutStatic)]
        public int PutStatic { get; set; }
        [MutatorParam(4, MutatorActionKind.ChangeStatic)]
        public int ChangeStatic { get; set; }
        [MutatorParam(5, MutatorActionKind.EraseStatic)]
        public int EraseStatic { get; set; }

        [MutatorParam(6, MutatorActionKind.PutRef)]
        public int PutRefProbability { get; set; }
        [MutatorParam(7, MutatorActionKind.ChangeRef)]
        public int ChangeRefProbability { get; set; }
        [MutatorParam(8, MutatorActionKind.EraseRef)]
        public int EraseRefProbabilty { get; set; }

        public MutatorParametersEntry() { }

        public MutatorParametersEntry(int stackDepth, int callProbability, int returnProbability, int newobjProbability, int putStatic, int changeStatic, int eraseStatic, int putRefProbability, int changeRefProbability, int eraseRefProbabilty)
        {
            this.StackDepth = stackDepth;
            this.CallProbability = callProbability;
            this.ReturnProbability = returnProbability;
            this.NewobjProbability = newobjProbability;
            this.PutStatic = putStatic;
            this.ChangeStatic = changeStatic;
            this.EraseStatic = eraseStatic;
            this.PutRefProbability = putRefProbability;
            this.ChangeRefProbability = changeRefProbability;
            this.EraseRefProbabilty = eraseRefProbabilty;
        }

        public MutatorParametersEntry(int stackDepth, int[] values)
        {
            this.StackDepth = stackDepth;

            for (int i = 0; i < _knownParams.Count; i++)
                _knownParams[i].setter(this, values[i]);
        }

        public MutatorActionKind GetActionKind(MutatorParametersModeKind mode, Random rnd)
        {
            var act = this.GetActionKindImpl(mode, rnd);
            while (act == MutatorActionKind.None)
                act = this.GetActionKindImpl(mode, rnd);

            return act;
        }

        private MutatorActionKind GetActionKindImpl(MutatorParametersModeKind mode, Random rnd)
        {
            var result = MutatorActionKind.None;

            switch (mode)
            {
                case MutatorParametersModeKind.Flat:
                    {
                        var value = rnd.Next(0, 100);

                        foreach (var p in _knownParams)
                        {
                            var paramValue = p.getter(this);
                            if (paramValue > value)
                            {
                                result = p.actionKind;
                                break;
                            }
                            else
                            {
                                value -= paramValue;
                            }
                        }
                    }
                    break;
                case MutatorParametersModeKind.Override:
                    {
                        var value = rnd.Next(0, 100);

                        foreach (var p in _knownParams)
                        {
                            var paramValue = p.getter(this);
                            if (paramValue > value)
                            {
                                result = p.actionKind;
                                break;
                            }
                        }
                    }
                    break;
                case MutatorParametersModeKind.Sequence:
                    {
                        foreach (var p in _knownParams)
                        {
                            var value = rnd.Next(0, 100);

                            var paramValue = p.getter(this);
                            if (paramValue > value)
                            {
                                result |= p.actionKind;
                                break;
                            }
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException("Unknown mode " + mode);
            }

            return result;
        }

        public void SetValue(MutatorActionKind actionKind, int value)
        {
            _knownParams.FirstOrDefault(p => p.actionKind == actionKind).setter(this, value);
        }

        public int[] GetValues()
        {
            return _knownParams.Select(p => p.getter(this)).ToArray();
        }
    }

    public class MutatorParameters : IEnumerable<MutatorParametersEntry>
    {
        private readonly List<MutatorParametersEntry> _items = new List<MutatorParametersEntry>();

        public MutatorParametersModeKind Mode { get; set; }

        public void Add(int stackDepth, int callProbability, int returnProbability, int newobjProbability, int putStatic, int changeStatic, int eraseStatic, int putRefProbability, int changeRefProbability, int eraseRefProbabilty)
        {
            var newItem = new MutatorParametersEntry(stackDepth, callProbability, returnProbability, newobjProbability, putStatic, changeStatic, eraseStatic, putRefProbability, changeRefProbability, eraseRefProbabilty);
            this.Add(newItem);
        }

        public void Add(MutatorParametersEntry newItem)
        {
            var index = _items.BinarySearch(newItem, Comparer<MutatorParametersEntry>.Create((a, b) => a.StackDepth.CompareTo(b.StackDepth)));
            if (index < 0)
                index = ~index;

            _items.Insert(index, newItem);
        }

        public MutatorParametersEntry GetParameters(int depth)
        {
            var index = _items.BinarySearch(null, Comparer<MutatorParametersEntry>.Create((a, b) => (a?.StackDepth ?? depth).CompareTo(b?.StackDepth ?? depth)));

            MutatorParametersEntry result;

            if (index >= 0)
            {
                result = _items[index];
            }
            else
            {
                index = ~index;

                if (index >= _items.Count)
                    result = _items.Last();
                else if (index == 0)
                    result = _items.First();
                else
                    result = this.InterpolateEntry(_items[index - 1], _items[index], depth);
            }

            return result;
        }

        private MutatorParametersEntry InterpolateEntry(MutatorParametersEntry a, MutatorParametersEntry b, int depth)
        {
            // y = y0 + (y1-y0)/(x1-x0)*(x-x0)
            return new MutatorParametersEntry(depth, a.GetValues().Zip(b.GetValues(), (y0, y1) => y0 + (y1 - y0) * (depth - a.StackDepth) / (b.StackDepth - a.StackDepth)).ToArray());
        }

        public IEnumerator<MutatorParametersEntry> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

}
