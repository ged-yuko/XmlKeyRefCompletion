using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Xml;
using System.Xml.Schema;
using XmlKeyRefCompletion.VsUtils;

namespace XmlKeyRefCompletion.Doc
{
    internal class XmlDocumentLoader
    {
        public static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan EditTimeout = TimeSpan.FromSeconds(3);

        private readonly Timer _reloadTimer;

        public MyXmlDocument DocumentData { get; private set; }

        private readonly ITextView _textView;

        public XmlDocumentLoader(ITextView textView)
        {
            _textView = textView;

            _reloadTimer = new Timer() {
                Interval = 1000,
                AutoReset = false,
            };

            _reloadTimer.Elapsed += (sender, ea) => this.ReloadDocument();

            this.ReloadDocument();
        }

        private void ReloadDocument()
        {
            Debug.Print("Reloading..");
            this.DocumentData = this.TryLoadDocument(_textView, out var doc) ? doc : null;
        }

        public void ScheduleReloading(TimeSpan interval)
        {
            Debug.Print("Scheduling reload in " + interval);
            _reloadTimer.Stop();
            _reloadTimer.Interval = interval.TotalMilliseconds;
            _reloadTimer.Start();
        }

        private bool TryFillCompletionData(MyXmlDocument doc)
        {
            var refsDataCollected = false;

            foreach (var element in doc.AllElements)
            {
                var schemaElement = element.SchemaInfo.SchemaElement;

                refsDataCollected |= this.TryCollectElementData(element, schemaElement);

                if (!schemaElement.RefName.IsEmpty)
                {
                    var referencedSchemaElements = doc.Schemas.Schemas(schemaElement.RefName.Namespace)
                                                      .OfType<XmlSchema>()
                                                      .Select(s => s.Elements[schemaElement.RefName])
                                                      .OfType<XmlSchemaElement>()
                                                      .ToList();

                    if (referencedSchemaElements.Count > 1)
                        Debug.Print("wtf");

                    foreach (var referencedSchemaElement in referencedSchemaElements)
                        refsDataCollected |= this.TryCollectElementData(element, referencedSchemaElement);
                }
            }

            return refsDataCollected;
        }

        private bool TryCollectElementData(MyXmlElement element, XmlSchemaElement schemaElement)
        {
            var hasKeyRefEntries = false;

            var constraints = schemaElement.Constraints;


            if (constraints.Count > 0)
            {
                var nsMan = MakeNsMan(element.OwnerDocument.NameTable, schemaElement);

                foreach (var schemaKey in constraints.OfType<XmlSchemaKey>())
                {
                    ForEachConstraintEntry(
                        nsMan, element, schemaKey,
                        (e, k) => e.RegisterKey(k),
                        (f, n, kd) => kd.RegisterField(f),
                        (a, pd) => pd.RegisterValue(a.Value)
                    );
                }

                foreach (var schemaKeyRef in constraints.OfType<XmlSchemaKeyref>())
                {
                    ForEachConstraintEntry(
                        nsMan, element, schemaKeyRef,
                        (e, r) => e.FindKey(r),
                        (f, n, kd) => kd?.FindField(n),
                        (a, pd) => { a.BindKeyPartData(pd); hasKeyRefEntries = true; }
                    );
                }
            }

            return hasKeyRefEntries;
        }

        private static void ForEachConstraintEntry<TConstraint, TConstraintData, TConstraintPartData>(
            XmlNamespaceManager nsMan, MyXmlElement scope, TConstraint schemaConstraint,
            Func<MyXmlElement, TConstraint, TConstraintData> constraintInitAction,
            Func<XmlSchemaXPath, int, TConstraintData, TConstraintPartData> constraintPartInitAction,
            Action<MyXmlAttribute, TConstraintPartData> constraintPartAction
        )
            where TConstraint : XmlSchemaIdentityConstraint
        {
            using (NsScope(nsMan, schemaConstraint))
            {
                List<MyXmlElement> elements;

                var constraintData = constraintInitAction(scope, schemaConstraint);

                using (NsScope(nsMan, schemaConstraint.Selector))
                {
                    elements = scope.SelectNodes(schemaConstraint.Selector.XPath, nsMan).OfType<MyXmlElement>().ToList();
                }

                int n = 0;
                foreach (var schemaConstraintKeyField in schemaConstraint.Fields.OfType<XmlSchemaXPath>())
                {
                    var constraintPartData = constraintPartInitAction(schemaConstraintKeyField, n, constraintData);

                    using (NsScope(nsMan, schemaConstraintKeyField))
                    {
                        foreach (var element in elements)
                        {
                            var attribute = element.SelectSingleNode(schemaConstraintKeyField.XPath, nsMan) as MyXmlAttribute;
                            if (attribute != null)
                                constraintPartAction(attribute, constraintPartData);
                        }
                    }

                    n++;
                }
            }
        }

        private static XmlNamespaceManager MakeNsMan(XmlNameTable table, XmlSchemaObject obj)
        {
            var nsMan = new XmlNamespaceManager(table);

            XmlSchemaObject o = obj;
            while (o != null)
            {
                foreach (var ns in o.Namespaces.ToArray())
                    nsMan.AddNamespace(ns.Name, ns.Namespace);

                o = o.Parent;
            }

            return nsMan;
        }

        private static IDisposable NsScope(XmlNamespaceManager nsMan, XmlSchemaObject obj)
        {
            return new NsManScope(nsMan, obj.Namespaces.ToArray());
        }

        private struct NsManScope : IDisposable
        {
            private readonly XmlQualifiedName[] _namespaces;
            private readonly XmlNamespaceManager _nsMan;

            public NsManScope(XmlNamespaceManager nsMan, params XmlQualifiedName[] namespaces)
            {
                _nsMan = nsMan;
                _namespaces = namespaces;

                foreach (var ns in namespaces)
                    nsMan.AddNamespace(ns.Name, ns.Namespace);
            }

            public void Dispose()
            {
                foreach (var ns in _namespaces)
                    _nsMan.RemoveNamespace(ns.Name, ns.Namespace);

            }
        }

        private bool TryLoadDocument(ITextView textView, out MyXmlDocument doc)
        {
            try
            {
                if (XmlSchemaSetHelper.TryParseXmlDocFromTextView(textView, out var schemaSetHelper))
                {
                    if (schemaSetHelper.TryResolveSchemaSetForXmlDocTextView(out var schemaSet, out var retryLater))
                    {
                        var text = textView.TextBuffer.CurrentSnapshot.GetText();
                        doc = MyXmlDocument.LoadWithTextInfo(new StringReader(text));

                        doc.Schemas = schemaSet;
                        doc.Validate((sender, ea) => {
                            System.Diagnostics.Debug.Print("Logged: " + ea.Severity + " : " + ea.Message);
                            if (ea.Exception != null)
                                System.Diagnostics.Debug.Print("Logged: " + ea.Exception.ToString());
                        });

                        if (!this.TryFillCompletionData(doc))
                            doc = null;
                    }
                    else
                    {
                        doc = null;

                        if (retryLater)
                            this.ScheduleReloading(InitTimeout);
                    }
                }
                else
                {
                    doc = null;
                    this.ScheduleReloading(InitTimeout);
                }
            }
            catch
            {
                doc = null;
            }

            return doc != null;
        }
    }
}
