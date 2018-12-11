using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlKeyRefCompletion
{
    interface IXmlTextInfoNode
    {
        Location TextLocation { get; set; }

        XmlNodeType NodeType { get; }
        string Name { get; }
    }

    class MyXmlDocument : XmlDocument
    {
        readonly XmlTextReader _reader;
        readonly List<List<MyXmlElement>> _elementsByLine = new List<List<MyXmlElement>>();
        readonly List<List<MyXmlAttribute>> _attributesByLine = new List<List<MyXmlAttribute>>();


        private MyXmlDocument(XmlTextReader reader)
        {
            _reader = reader;

            //var settings = reader.Settings;
            //settings.ProhibitDtd = false;
            //settings.IgnoreWhitespace = false;
            //settings.CheckCharacters = false;
            //settings.ValidationType = ValidationType.None;
            //settings.ValidationEventHandler += (sender, ea) => { Debug.Print(ea.Message); };

            this.Load(reader);
        }

        public MyXmlAttribute FindAttributeAt(int lineNumber, int linePosition)
        {
            return this.FindAt(_attributesByLine, new Location(lineNumber + 1, linePosition));
        }

        private T FindAt<T>(List<List<T>> list, Location loc)
            where T : XmlNode, IXmlTextInfoNode
        {
            T result;

            if (list.Count > loc.Line && loc.Line >= 0)
            {
                var line = list[loc.Line];
                var index = line.BinarySearch(default(T), Comparer<T>.Create((a, b) => a.TextLocation.Column.CompareTo(loc.Column)));

                if (index < 0)
                {
                    index = ~index;
                    if (index > 0)
                    {
                        var candidate = line[index - 1];
                        if (candidate.TextLocation.Column + candidate.OuterXml.Length > loc.Column)
                        {
                            result = candidate;
                        }
                        else
                        {
                            result = null;
                        }
                    }
                    else
                    {
                        result = null;
                    }
                }
                else
                {
                    result = line[index];
                }
            }
            else
            {
                result = null;
            }

            Debug.Print("found " + result?.Name ?? "<NULL>");

            return result;
        }

        public override XmlElement CreateElement(string prefix, string localname, string nsURI)
        {
            return this.Register(_elementsByLine, this.SetTextInfo(new MyXmlElement(prefix, localname, nsURI, this)));
        }

        public override XmlAttribute CreateAttribute(string prefix, string localName, string namespaceURI)
        {
            return this.Register(_attributesByLine, this.SetTextInfo(new MyXmlAttribute(prefix, localName, namespaceURI, this)));
        }

        public override XmlNode CreateNode(string nodeTypeString, string name, string namespaceURI)
        {
            return base.CreateNode(nodeTypeString, name, namespaceURI);
        }

        public override XmlNode CreateNode(XmlNodeType type, string name, string namespaceURI)
        {
            return base.CreateNode(type, name, namespaceURI);
        }

        public override XmlNode CreateNode(XmlNodeType type, string prefix, string name, string namespaceURI)
        {
            return base.CreateNode(type, prefix, name, namespaceURI);
        }

        private T SetTextInfo<T>(T node)
            where T : IXmlTextInfoNode
        {
            node.TextLocation = new Location(_reader.LineNumber, _reader.LinePosition);
            return node;
        }

        private T Register<T>(List<List<T>> list, T node)
            where T : XmlNode, IXmlTextInfoNode
        {
            while (list.Count <= node.TextLocation.Line)
                list.Add(new List<T>());

            var line = list[node.TextLocation.Line];
            var index = line.BinarySearch(node, Comparer<T>.Create((a, b) => a.TextLocation.Column.CompareTo(b.TextLocation.Column)));

            if (index < 0)
                index = ~index;

            line.Insert(index, node);

            return node;
        }

        public static MyXmlDocument LoadWithTextInfo(string filepath)
        {
            return new MyXmlDocument(new XmlTextReader(filepath));
        }

        public static MyXmlDocument LoadWithTextInfo(Stream stream)
        {
            return new MyXmlDocument(new XmlTextReader(stream));
        }

        public static MyXmlDocument LoadWithTextInfo(TextReader reader)
        {
            return new MyXmlDocument(new XmlTextReader(reader));
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

        public string CompletionName { get; private set; }
        public ReadOnlyCollection<string> PossibleValues { get; private set; }

        internal void SetPossibleValues(string name, ReadOnlyCollection<string> values)
        {
            this.CompletionName = name;
            this.PossibleValues = values;
        }
    }
}
