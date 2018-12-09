using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlKeyRefCompletion
{
    internal class MyXmlNamespaceResolver : IXmlNamespaceResolver
    {
        internal Microsoft.XmlEditor.XmlNamespaceScope scope;

        internal XmlNameTable nameTable;

        private string emptyAtom;

        public bool HasNamespaceResolver => true;

        public XmlNameTable NameTable => nameTable;

        public MyXmlNamespaceResolver(XmlNameTable nameTable)
        {
            this.nameTable = nameTable;
            emptyAtom = nameTable.Add(string.Empty);
            scope = new Microsoft.XmlEditor.XmlNamespaceScope(null);
        }

        private string Atomized(string s)
        {
            if (s == null)
            {
                return null;
            }
            if (s.Length == 0)
            {
                return emptyAtom;
            }
            return nameTable.Add(s);
        }

        public string LookupPrefix(string namespaceName, bool atomizedName)
        {
            string s = null;
            if (scope != null)
            {
                s = scope.LookupPrefix(namespaceName);
            }
            return Atomized(s);
        }

        public string LookupPrefix(string namespaceName)
        {
            string s = null;
            if (scope != null)
            {
                s = scope.LookupPrefix(namespaceName);
            }
            return Atomized(s);
        }

        public string LookupNamespace(string prefix, bool atomizedName)
        {
            return LookupNamespace(prefix);
        }

        public string LookupNamespace(string prefix)
        {
            string text = null;
            Microsoft.XmlEditor.XmlNamespaceScope xmlNamespaceScope = scope;
            if (xmlNamespaceScope != null)
            {
                text = xmlNamespaceScope.LookupNamespace(prefix);
            }
            if (text == null)
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    return Atomized("");
                }
                if (prefix == "xml")
                {
                    return Atomized("http://www.w3.org/XML/1998/namespace");
                }
                if (prefix == "xmlns")
                {
                    return Atomized("http://www.w3.org/2000/xmlns/");
                }
            }
            return Atomized(text);
        }

        public IDictionary<string, string> GetNamespacesInScope(System.Xml.XmlNamespaceScope scope)
        {
            Hashtable namespacesInScope = this.scope.GetNamespacesInScope();
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            IDictionaryEnumerator enumerator = namespacesInScope.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    DictionaryEntry dictionaryEntry = (DictionaryEntry)enumerator.Current;
                    string text = dictionaryEntry.Key as string;
                    string text2 = dictionaryEntry.Value as string;
                    if (text.Length > 0 || text2.Length > 0)
                    {
                        dictionary[text2] = text;
                    }
                }
                return dictionary;
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }
}
