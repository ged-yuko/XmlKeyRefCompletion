using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Xml;
using System.Xml.Schema;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
// using XmlKeyRefCompletion.VsUtils;

namespace XmlKeyRefCompletion.Doc
{
    internal class XmlDocumentLoader
    {
        public static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan EditTimeout = TimeSpan.FromSeconds(3);

        private readonly Timer _reloadTimer;

        public ITextSnapshot CurrentSnapshot { get; private set; }
        public MyXmlDocument DocumentData { get; private set; }
        public event EventHandler DocumentDataUpdated = null;

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
            if (!_textView.IsClosed)
            {
                Debug.Print("Reloading..");
                this.DocumentData = this.TryLoadDocument(_textView, out var doc) ? doc : null;
                this.DocumentDataUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ForceReload()
        {
            _reloadTimer.Stop();
            this.ReloadDocument();
        }

        public void ScheduleReloading(TimeSpan interval)
        {
            Debug.Print("Scheduling reload in " + interval);

            this.DocumentData = null;
            this.DocumentDataUpdated?.Invoke(this, EventArgs.Empty);

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

                if (schemaElement != null)
                {
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
                        (a, pd) => pd.RegisterValue(a)
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
                //if (XmlSchemaSetHelper.TryParseXmlDocFromTextView(textView, out var isSchema, out var schemaSetHelper))
                //{
                //    if (schemaSetHelper.TryResolveSchemaSetForXmlDocTextView(out var schemaSet, out var retryLater))
                //    {

                this.CurrentSnapshot = textView.TextBuffer.CurrentSnapshot;
                var text = this.CurrentSnapshot.GetText();
                doc = MyXmlDocument.LoadWithTextInfo(new StringReader(text));

                if (this.TryResolveSchemas(doc, out var schemaSet))
                {
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
                }

                //    }
                //    else
                //    {
                //        doc = null;

                //        if (retryLater)
                //            this.ScheduleReloading(InitTimeout);
                //    }
                //}
                //else
                //{
                //    doc = null;

                //    if (!isSchema)
                //        this.ScheduleReloading(InitTimeout);
                //}
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                doc = null;
            }

            return doc != null;
        }

        private class SchemaInfo
        {
            public XmlSchema SchemaIfLoaded { get; private set; }
            public string Namespace { get; private set; }

            public FileInfo File { get; private set; }
            public DateTime LoadStamp { get; private set; }

            public SchemaInfo(string fileName)
            {
                this.SchemaIfLoaded = null;
                this.Namespace = null;
                this.File = new FileInfo(fileName);
                this.LoadStamp = DateTime.MinValue;
            }

            public void Refresh()
            {
                if (this.File.CreationTime > this.LoadStamp || this.File.LastWriteTime > this.LoadStamp || this.SchemaIfLoaded == null)
                {
                    try
                    {
                        using (var reader = this.File.OpenRead())
                        {
                            var ok = true;
                            this.SchemaIfLoaded = XmlSchema.Read(reader, (o, ea) => ok &= ea.Severity == XmlSeverityType.Error);

                            if (!ok)
                                this.SchemaIfLoaded = null;

                            this.Namespace = this.SchemaIfLoaded.TargetNamespace;
                            this.LoadStamp = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.SchemaIfLoaded = null;
                        this.Namespace = null;
                    }
                }
            }
        }

        private static Dictionary<string, SchemaInfo> _schemaByNamespace = new Dictionary<string, SchemaInfo>();
        private static Dictionary<string, SchemaInfo> _schemaByFilePath = new Dictionary<string, SchemaInfo>();

        private void IntroduceSchema(string filePath)
        {
            if (!_schemaByFilePath.TryGetValue(filePath, out var info))
            {
                info = new SchemaInfo(filePath);
                _schemaByFilePath.Add(filePath, info);
            }

            var oldNs = info.Namespace;

            info.Refresh();

            if (oldNs != info.Namespace)
            {
                if (oldNs != null && _schemaByNamespace.TryGetValue(oldNs, out var oldInfo))
                    _schemaByNamespace.Remove(oldNs);

                if (info.Namespace != null)
                    _schemaByNamespace[info.Namespace] = info;
            }
        }

        private bool TryResolveSchemas(XmlDocument doc, out XmlSchemaSet schemaSet)
        {
            var solutionSchemas = this.GetSolutionFiles().Where(f => Path.GetFileName(f).EndsWith(".xsd", StringComparison.InvariantCultureIgnoreCase));
            var ok = true;

            foreach (var item in solutionSchemas)
                this.IntroduceSchema(item);

            schemaSet = new XmlSchemaSet();

            foreach (var node in doc.DocumentElement.Flatten(n => n.ChildNodes.OfType<XmlElement>()))
            {
                foreach (var attr in node.Attributes.OfType<XmlAttribute>())
                {
                    if (attr.Name == "xmlns" || attr.Name.StartsWith("xmlns:"))
                    {
                        if (ok &= _schemaByNamespace.TryGetValue(attr.Value, out var schemaInfo))
                            schemaSet.Add(schemaInfo.SchemaIfLoaded);
                        else
                            break;
                    }
                }
            }

            return ok;
        }

        private IEnumerable<string> GetSolutionFiles()
        {
            var dte = (DTE)Package.GetGlobalService(typeof(SDTE));

            IEnumerable<string> ScanProject(Project project)
            {
                if (project != null)
                {
                    var name = project.FileName;
                    if (!string.IsNullOrWhiteSpace(name))
                        yield return name;

                    foreach (ProjectItem item in project.ProjectItems)
                        foreach (var file in ScanProjectItem(item))
                            yield return file;
                }
            }

            IEnumerable<string> ScanProjectItem(ProjectItem item)
            {
                if (item != null)
                {
                    var name = item.FileNames[0];
                    if (!string.IsNullOrWhiteSpace(name))
                        yield return name;

                    if (item.ProjectItems != null)
                        foreach (ProjectItem subitem in item.ProjectItems)
                            if (subitem != item)
                                foreach (var file in ScanProjectItem(subitem))
                                    yield return file;
                }
            }

            foreach (Project project in dte.Solution)
                foreach (var file in ScanProject(project).Where(f => File.Exists(f)))
                    yield return file;
        }
    }
}
