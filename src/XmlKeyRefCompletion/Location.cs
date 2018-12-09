using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlKeyRefCompletion
{
    public struct Location
    {
        private int _line, _column;

        public int Line { get { return _line; } }
        public int Column { get { return _column; } }

        public Location(int line, int column)
        {
            _line = line;
            _column = column;
        }

        public override string ToString()
        {
            return string.Format("[L{0}, C{1}]", _line, _column);
        }

        public static bool operator >(Location a, Location b)
        {
            return a.Line > b.Line ? true : (a.Line == b.Line && a.Column > b.Column);
        }

        public static bool operator <(Location a, Location b)
        {
            return a.Line < b.Line ? true : (a.Line == b.Line && a.Column < b.Column);
        }

        public static bool operator >=(Location a, Location b)
        {
            return a > b || a == b;
        }

        public static bool operator <=(Location a, Location b)
        {
            return a < b || a == b;
        }

        public static bool operator ==(Location a, Location b)
        {
            return a.Line == b.Line && a.Column == b.Column;
        }

        public static bool operator !=(Location a, Location b)
        {
            return a.Line != b.Line || a.Column != b.Column;
        }

        public override int GetHashCode()
        {
            return _line.GetHashCode() ^ _column.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != typeof(Location))
                return false;

            return this == ((Location)obj);
        }
    }
}
