using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace XmlKeyRefCompletion.Doc
{
    internal interface IXmlTextInfoNode
    {
        Location TextLocation { get; set; }

        XmlNodeType NodeType { get; }
        string Name { get; }
    }

    [Guid("5F05D289-1644-46B4-9526-ABA9DAAA5F91")]
    internal class MyXmlDocument : XmlDocument
    {
        private readonly XmlTextReader _reader;

        // readonly List<List<MyXmlElement>> _elementsByLine = new List<List<MyXmlElement>>();
        // readonly List<List<MyXmlAttribute>> _attributesByLine = new List<List<MyXmlAttribute>>();
        private readonly List<List<MyXmlText>> _textByLine = new List<List<MyXmlText>>();

        public ReadOnlyCollection<MyXmlElement> AllElements { get; private set; }
        public ReadOnlyCollection<MyXmlAttribute> InvalidKeyrefs { get; private set; }

        private readonly List<MyXmlElement> _elements = new List<MyXmlElement>();
        private readonly List<MyXmlAttribute> _invalidKeyrefs = new List<MyXmlAttribute>();

        private MyXmlDocument(XmlTextReader reader)
        {
            _reader = reader;

            this.AllElements = new ReadOnlyCollection<MyXmlElement>(_elements);
            this.InvalidKeyrefs = new ReadOnlyCollection<MyXmlAttribute>(_invalidKeyrefs);
            this.Load(reader);
        }

        //public MyXmlAttribute FindAttributeAt(int lineNumber, int linePosition)
        //{
        //    return this.FindAt(_attributesByLine, new Location(lineNumber + 1, linePosition + 1));
        //}

        public MyXmlText FindTextAt(int lineNumber, int linePosition)
        {
            return this.FindAt(_textByLine, new Location(lineNumber + 1, linePosition + 1));
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

                        result = candidate;
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
            var element = new MyXmlElement(prefix, localname, nsURI, this);
            _elements.Add(element);
            return element;
        }

        public override XmlAttribute CreateAttribute(string prefix, string localName, string namespaceURI)
        {
            return this.SetTextInfo(new MyXmlAttribute(prefix, localName, namespaceURI, this, _invalidKeyrefs));
        }

        public override XmlText CreateTextNode(string text)
        {
            return this.Register(_textByLine, this.SetTextInfo(new MyXmlText(text, this)));
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

    internal class MyXmlElement : XmlElement
    {
        public XmlScopeData ScopeData { get; private set; }

        internal MyXmlElement(string prefix, string localname, string nsURI, XmlDocument doc) : base(prefix, localname, nsURI, doc) { }

        public XmlScopeKeyData RegisterKey(XmlSchemaKey schemaKeyInfo)
        {
            if (this.ScopeData == null)
                this.ScopeData = new XmlScopeData(this);

            return this.ScopeData.RegisterKey(schemaKeyInfo);
        }

        public XmlScopeKeyData FindKey(XmlSchemaKeyref schemaKeyRefInfo)
        {
            XmlScopeKeyData result;

            var element = this;
            do
            {
                result = element.ScopeData?.FindKey(schemaKeyRefInfo);
                element = element.ParentNode as MyXmlElement;
            } while (result == null && element != null);

            return result;
        }
    }

    internal class MyXmlAttribute : XmlAttribute, IXmlTextInfoNode
    {
        public Location TextLocation { get; set; }

        readonly List<MyXmlAttribute> _invalidKeyrefs;

        public XmlScopeKeyPartData ReferencedKeyPartData { get; private set; }

        internal MyXmlAttribute(string prefix, string localName, string namespaceURI, XmlDocument doc, List<MyXmlAttribute> invalidKeyrefs) 
            : base(prefix, localName, namespaceURI, doc)
        { _invalidKeyrefs = invalidKeyrefs;
        }

        public void BindKeyPartData(XmlScopeKeyPartData partData)
        {
            if (this.ReferencedKeyPartData != null)
                throw new InvalidOperationException();

            this.ReferencedKeyPartData = partData;

            if (!partData.HasValue(this.Value))
                _invalidKeyrefs.Add(this);
        }
    }

    internal class MyXmlText : XmlText, IXmlTextInfoNode
    {
        public Location TextLocation { get; set; }

        internal MyXmlText(string strData, XmlDocument doc) : base(strData, doc) { }
    }
}
