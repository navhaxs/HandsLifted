using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.Data
{
    public struct Indexed<T>
    {
        public int Index { get; private set; }
        public T Value { get; private set; }
        public Indexed(int index, T value) : this()
        {
            Index = index;
            Value = value;
        }

        public override string ToString()
        {
            return "(Indexed: " + Index + ", " + Value.ToString() + " )";
        }
    }

    public class Indexed
    {
        public static Indexed<T> Create<T>(int indexed, T value)
        {
            return new Indexed<T>(indexed, value);
        }
    }
}
