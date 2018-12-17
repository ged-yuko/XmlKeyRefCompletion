using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace XmlKeyRefCompletion.Doc
{
    class XmlScopeKeyPartData
    {
        public XmlScopeKeyData KeyData { get; private set; }
        public int Index { get; private set; }

        public XmlSchemaXPath PartInfo { get; private set; }
        public IEnumerable<string> Values { get { return _values; } }

        readonly HashSet<string> _values = new HashSet<string>();

        public XmlScopeKeyPartData(XmlScopeKeyData keyData, int index, XmlSchemaXPath partInfo)
        {
            this.KeyData = keyData;
            this.Index = index;
            this.PartInfo = partInfo;
        }

        public void RegisterValue(string value)
        {
            _values.Add(value);
        }
    }

    class XmlScopeKeyData
    {
        public XmlScopeData ScopeData { get; private set; }
        public XmlSchemaKey KeyInfo { get; private set; }
        public string Name { get { return this.KeyInfo.Name; } }

        public int Arity { get { return _parts.Count; } }

        readonly List<XmlScopeKeyPartData> _parts = new List<XmlScopeKeyPartData>();

        public XmlScopeKeyData(XmlScopeData scopeData, XmlSchemaKey keyInfo)
        {
            this.ScopeData = scopeData;
            this.KeyInfo = keyInfo;
        }

        public XmlScopeKeyPartData RegisterField(XmlSchemaXPath partInfo)
        {
            var partData = new XmlScopeKeyPartData(this, _parts.Count, partInfo);
            _parts.Add(partData);
            return partData;
        }

        public XmlScopeKeyPartData FindField(int index)
        {
            return index >= 0 && index < _parts.Count ? _parts[index] : null;
        }
    }

    class XmlScopeData
    {
        public MyXmlElement ScopeElement { get; private set; }

        readonly Dictionary<string, XmlScopeKeyData> _keys = new Dictionary<string, XmlScopeKeyData>();

        public XmlScopeData(MyXmlElement scopeElement)
        {
            this.ScopeElement = scopeElement;
        }

        public XmlScopeKeyData RegisterKey(XmlSchemaKey keyInfo)
        {
            var keyData = new XmlScopeKeyData(this, keyInfo);
            _keys.Add(keyData.Name, keyData);
            return keyData;
        }

        public XmlScopeKeyData FindKey(XmlSchemaKeyref schemaKeyRefInfo)
        {
            return _keys.TryGetValue(schemaKeyRefInfo.Refer.Name, out var keyData) ? keyData : null;
        }
    }


}
