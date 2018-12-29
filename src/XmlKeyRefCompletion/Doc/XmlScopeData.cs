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
        // public IReadOnlyCollection<string> Values { get { return _values; } }
        public IReadOnlyCollection<string> Values { get { return _valueDefs.Keys; } }

        // readonly HashSet<string> _values = new HashSet<string>();
        readonly Dictionary<string, MyXmlAttribute> _valueDefs = new Dictionary<string, MyXmlAttribute>();

        public XmlScopeKeyPartData(XmlScopeKeyData keyData, int index, XmlSchemaXPath partInfo)
        {
            this.KeyData = keyData;
            this.Index = index;
            this.PartInfo = partInfo;
        }

        public void RegisterValue(MyXmlAttribute attr)
        {
            // _values.Add(attr.Value);
            _valueDefs.Add(attr.Value, attr);
        }

        public bool TryGetValueDef(string value, out MyXmlAttribute defAttr)
        {
            return _valueDefs.TryGetValue(value, out defAttr);
        }

        //public bool HasValue(string value)
        //{
        //    //return _values.Contains(value);
        //    return _valueDefs.ContainsKey(value);
        //}

        public bool RegisterReference(MyXmlAttribute reference)
        {
            var hasTarget = _valueDefs.TryGetValue(reference.Value, out var target);

            if (hasTarget)
                target.RegisterReference(reference);

            return hasTarget;
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
