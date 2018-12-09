using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlKeyRefCompletion.Test1
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

    interface IXmlTextInfoNode
    {
        Location TextLocation { get; set; }

        XmlNodeType NodeType { get; }
        string Name { get; }
    }

    class MyXmlDocument : XmlDocument
    {
        readonly XmlTextReader _reader;

        private MyXmlDocument(XmlTextReader reader)
        {
            _reader = reader;
            this.Load(reader);
        }

        public override XmlElement CreateElement(string prefix, string localname, string nsURI)
        {
            return this.SetTextInfo(new MyXmlElement(prefix, localname, nsURI, this));
        }

        public override XmlAttribute CreateAttribute(string prefix, string localName, string namespaceURI)
        {
            return this.SetTextInfo(new MyXmlAttribute(prefix, localName, namespaceURI, this));
        }

        public static XmlDocument LoadWithTextInfo(string filepath)
        {
            return new MyXmlDocument(new XmlTextReader(filepath));
        }

        private T SetTextInfo<T>(T node)
            where T : IXmlTextInfoNode
        {
            node.TextLocation = new Location(_reader.LineNumber, _reader.LinePosition);
            return node;
        }
    }

    class MyXmlElement : XmlElement, IXmlTextInfoNode
    {
        public Location TextLocation { get; set; }

        internal MyXmlElement(string prefix, string localname, string nsURI, XmlDocument doc) : base(prefix, localname, nsURI, doc) { }
    }

    class MyXmlAttribute : XmlAttribute, IXmlTextInfoNode
    {
        public Location TextLocation { get; set; }

        internal MyXmlAttribute(string prefix, string localName, string namespaceURI, XmlDocument doc) : base(prefix, localName, namespaceURI, doc) { }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var filepath = @"C:\Home\Ged\vs2017\cs\XmlKeyRefCompletion\XmlKeyRefCompletion\source.extension.vsixmanifest";
            var doc = MyXmlDocument.LoadWithTextInfo(filepath);

            doc.SelectNodes("//*").OfType<IXmlTextInfoNode>().ToList().ForEach(e => Console.WriteLine($"{e.NodeType}:{e.Name} [L{e.TextLocation.Line}, C{e.TextLocation.Column}]"));
            doc.SelectNodes("//@*").OfType<IXmlTextInfoNode>().ToList().ForEach(e => Console.WriteLine($"{e.NodeType}:{e.Name} [L{e.TextLocation.Line}, C{e.TextLocation.Column}]"));
        }
    }
}
