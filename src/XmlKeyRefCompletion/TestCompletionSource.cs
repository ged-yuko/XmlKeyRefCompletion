using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
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

using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using CompletionSet = Microsoft.VisualStudio.Language.Intellisense.CompletionSet;
using System.ComponentModel.Design;
using Microsoft.XmlEditor;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.XmlEditor;
using Package = Microsoft.XmlEditor.Package;
using Microsoft.VisualStudio.Text.Editor;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;

namespace XmlKeyRefCompletion
{
    class TestCompletionSource : ICompletionSource
    {
        class KeyRefInfo
        {
            public string XPath { get; private set; }
            public ReadOnlyCollection<string> FieldXPaths { get; private set; }

            public KeyRefInfo(string xPath, string[] v)
            {
                this.XPath = xPath;
                this.FieldXPaths = v.AsReadOnly();
            }


        }

        class KeyInfo
        {
            public string Name { get; private set; }
            public int Arity { get { return _transposedValues.Count; } }
            public string XPath { get; private set; }
            public ReadOnlyCollection<string> FieldXPaths { get; private set; }

            readonly List<KeyRefInfo> _refs = new List<KeyRefInfo>();
            readonly List<string[]> _values = new List<string[]>();
            readonly List<HashSet<string>> _transposedValues = new List<HashSet<string>>();

            public KeyInfo(string name, string xPath, string[] fieldXpaths)
            {
                this.Name = name;
                this.XPath = xPath;
                this.FieldXPaths = fieldXpaths.AsReadOnly();
            }

            public void AddRef(KeyRefInfo keyRefInfo)
            {
                _refs.Add(keyRefInfo);
            }

            public void AddValue(string[] value)
            {
                _values.Add(value);

                value.ForEach((s, n) => {
                    while (_transposedValues.Count <= n)
                        _transposedValues.Add(new HashSet<string>());

                    _transposedValues[n].Add(s);
                });
            }

            public ReadOnlyCollection<string> GetValues(int index)
            {
                return new ReadOnlyCollection<string>(_transposedValues[index].ToList());
            }

            public ReadOnlyCollection<KeyRefInfo> GetRefs()
            {
                return new ReadOnlyCollection<KeyRefInfo>(_refs);
            }
        }

        class SchemaContext
        {
            public string Namespace { get; private set; }

            readonly Dictionary<string, KeyInfo> _keysByName = new Dictionary<string, KeyInfo>();

            public SchemaContext(string ns)
            {
                this.Namespace = ns;
            }

            public void AddKey(KeyInfo key)
            {
                _keysByName.Add(key.Name, key);
            }

            public bool TryGetKey(string name, out KeyInfo key)
            {
                return _keysByName.TryGetValue(name, out key);
            }

            public ReadOnlyCollection<KeyInfo> GetKeys()
            {
                return new ReadOnlyCollection<KeyInfo>(_keysByName.Values.ToList());
            }
        }

        private TestCompletionSourceProvider _sourceProvider;
        private ITextBuffer _textBuffer;
        //private List<Completion> m_compList;
        //private XmlRepository _repository = new XmlRepository();

        readonly Dictionary<string, SchemaContext> _contextsByNs = new Dictionary<string, SchemaContext>();

        public TestCompletionSource(TestCompletionSourceProvider sourceProvider, ITextBuffer textBuffer)
        {
            _sourceProvider = sourceProvider;
            _textBuffer = textBuffer;
        }

        MyXmlDocument CollectInfo(XmlSchemaSet schemaSet)
        {
            _contextsByNs.Clear();

            var text = _textBuffer.CurrentSnapshot.GetText();
            var doc = MyXmlDocument.LoadWithTextInfo(new StringReader(text));

            doc.Schemas = schemaSet;
            doc.Validate((sender, ea) => {
                System.Diagnostics.Debug.Print("Logged: " + ea.Severity + " : " + ea.Message);
                if (ea.Exception != null)
                    System.Diagnostics.Debug.Print("Logged: " + ea.Exception.ToString());
            });

            foreach (var xsd in doc.Schemas.Schemas().OfType<XmlSchema>())
            {
                var ctx = new SchemaContext(xsd.TargetNamespace);

                foreach (var element in xsd.Elements.Values.OfType<XmlSchemaElement>())
                {
                    if (element.Constraints.Count > 0)
                    {
                        var nsman = this.MakeNsMan(doc.NameTable, element);

                        var scopeElements = doc.SelectNodes($"//*[local-name()='{element.Name}' and namespace-uri()='{xsd.TargetNamespace}']", nsman)
                                               .OfType<System.Xml.XmlElement>()
                                               .Where(e => e.SchemaInfo.SchemaElement.QualifiedName == element.QualifiedName);

                        foreach (var key in element.Constraints.OfType<XmlSchemaKey>())
                        {
                            var keyInfo = new KeyInfo(key.Name, key.Selector.XPath, key.Fields.OfType<XmlSchemaXPath>().Select(p => p.XPath).ToArray());
                            ctx.AddKey(keyInfo);

                            var keyValues = scopeElements.SelectMany(e => e.SelectNodes(key.Selector.XPath, nsman).OfType<System.Xml.XmlElement>())
                                                         .Select(e => keyInfo.FieldXPaths.Select(k => e.SelectSingleNode(k, nsman).Value).ToArray())
                                                         .ToList();

                            keyValues.ForEach(vv => keyInfo.AddValue(vv));
                        }

                        foreach (var keyref in element.Constraints.OfType<XmlSchemaKeyref>().ToList())
                        {
                            if (ctx.TryGetKey(keyref.Refer.Name, out var key))
                            {
                                var refInfo = new KeyRefInfo(keyref.Selector.XPath, keyref.Fields.OfType<XmlSchemaXPath>().Select(p => p.XPath).ToArray());
                                key.AddRef(refInfo);

                                var keyReferents = scopeElements.SelectMany(e => e.SelectNodes(refInfo.XPath, nsman).OfType<System.Xml.XmlElement>())
                                                                .Select(e => refInfo.FieldXPaths.Select(k => e.SelectSingleNode(k, nsman)).OfType<MyXmlAttribute>().ToArray())
                                                                .ToList();

                                keyReferents.ForEach(vv => vv.ForEach((a, n) => a.SetPossibleValues(key.Arity > 1 ? (key.Name + "#" + n) : key.Name, key.GetValues(n))));

                            }
                        }
                    }
                }
            }

            return doc;
        }

        private XmlNamespaceManager MakeNsMan(XmlNameTable table, XmlSchemaObject obj)
        {
            var nsman = new XmlNamespaceManager(table);

            XmlSchemaObject o = obj;
            while (o != null)
            {
                o.Namespaces.ToArray().ForEach(n => nsman.AddNamespace(n.Name, n.Namespace));
                o = o.Parent;
            }

            return nsman;
        }

        private void GetCurrentSource(IVsTextView view)
        {
            try
            {
                IVsMonitorSelection selection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
                object pvar = null;
                if (!ErrorHandler.Succeeded(selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out pvar)))
                {
                    //this.currentDocument = null;
                    return;
                }
                IVsWindowFrame frame = pvar as IVsWindowFrame;
                if (frame == null)
                {
                    //this.currentDocument = null;
                    return;
                }

                object docData = null;
                if (!ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData)))
                {
                    //this.currentDocument = null;
                    return;
                }
                object docViewServiceObject;
                if (!ErrorHandler.Succeeded(frame.GetProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_SPFrame, out docViewServiceObject)))
                {
                    //this.currentDocument = null;
                    return;
                }

                IVsTextLines buffer = docData as IVsTextLines;
                if (buffer == null)
                {
                    IVsTextBufferProvider tb = docData as IVsTextBufferProvider;
                    if (tb != null)
                    {
                        tb.GetTextBuffer(out buffer);
                    }
                }
                if (buffer == null)
                {
                    //this.currentDocument = null;
                    return;
                }

                Guid xmlLanguageServiceGuid = new Guid("f6819a78-a205-47b5-be1c-675b3c7f0b8e");

                Guid languageServiceGuid;
                buffer.GetLanguageServiceID(out languageServiceGuid);
                if (languageServiceGuid != xmlLanguageServiceGuid)
                {
                    return;
                }

                IOleServiceProvider docViewService = (IOleServiceProvider)docViewServiceObject;

                //IOleServiceProvider docViewService = (IOleServiceProvider)docViewServiceObject;
                IntPtr ptr;
                Guid guid = xmlLanguageServiceGuid;
                Guid iid = typeof(IVsLanguageInfo).GUID;
                if (!ErrorHandler.Succeeded(docViewService.QueryService(ref guid, ref iid, out ptr)))
                {
                    return;
                }

                var currentDocumentLanguageInfo = (IVsLanguageInfo)Marshal.GetObjectForIUnknown(ptr);
                Marshal.Release(ptr);

                LanguageService langsvc = currentDocumentLanguageInfo as LanguageService;
                var source = langsvc.GetSource(buffer);

                //XmlLanguageService xmlLangSvc = (XmlLanguageService)langsvc;
                //xmlLangSvc.GetParseTree(source, 

                Console.WriteLine(source);

                //if (this.currentDocument == null || buffer != this.currentDocument.TextEditorBuffer)
                //{
                //    this.currentDocument = new VisualStudioDocument(frame, buffer, docViewService);
                //    this.changeCount = this.currentDocument.Source.ChangeCount;
                //}
                //else
                //{
                //    if (this.changeCount != this.currentDocument.Source.ChangeCount)
                //    {
                //        this.currentDocument.Reload();
                //        this.changeCount = this.currentDocument.Source.ChangeCount;
                //    }
                //}
                return;

            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                // ReportError(e);
            }
        }

        XmlSchemaSet ObtainXmlSchemaSet(ITextView textView)
        {
            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            IVsEditorAdaptersFactoryService editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            IOleServiceProvider sp = Package.GetGlobalService(typeof(IOleServiceProvider)) as IOleServiceProvider;
            IVsTextView view = editorFactory.GetViewAdapter(textView);

            //IVsTextBuffer buffer = editorFactory.GetBufferAdapter(session.TextView.TextBuffer);
            //if (!ErrorHandler.Succeeded(buffer.GetLanguageServiceID(out var langServiceId)))
            //    return;

            XmlLanguageService langSvc = (XmlLanguageService)Package.GetGlobalService(Marshal.GetTypeFromCLSID(Guid.Parse("f6819a78-a205-47b5-be1c-675b3c7f0b8e")));

            var source = langSvc.GetSource(view);

            var schemaCache = new MySchemaCache();

            var filename = source.GetFilePath();
            // string str = (string)schemaCache.GetResolver(filename).GetEntity(new Uri(filename), null, typeof(string));
            // XmlParseRequest request = new XmlParseRequest(0, 0, new TokenInfo(), filename, 5, null, options, null, synchronous: true, new StringSnapshot(str), needXlinqModel);

            // var request = langSvc.CreateParseRequest(source, 0, 0, new TokenInfo(), source.GetText(), source.GetFilePath(), ParseReason.CodeSpan, view);

            //AuthoringSink sink = new AuthoringSink(ParseReason.CodeSpan, 0, 0, int.MaxValue);
            //var scope = (Microsoft.XmlEditor.AuthoringScope)langSvc.ParseSource((0, 0, token, session.TextView.TextBuffer.CurrentSnapshot.GetText(), source.GetFilePath(), sink.Reason, view, sink, true));
            //scope.Document.EndColumn
            var doc = langSvc.GetParseTree(source, view, 0, 0, ParseReason.CodeSpan);
            if (true.Equals(doc.GetProperty("IsSchema")))
                return null;

            // nsResolver = new MyXmlNamespaceResolver(nameTable);
            NamespaceFilter filter = nsuri => nsuri == "http://www.w3.org/2001/XMLSchema-instance";

            var schemas = (IList<XmlSchemaReference>)doc.GetProperty("Schemas").CallMethod("GetValidationSet", filter);

            var options = new XmlProjectOptions();
            var errorsList = new ErrorNodeList();
            var errorHandler = new Microsoft.XmlEditor.ErrorHandler(errorsList);

            IList<XmlSchemaReference> candidateSchemas = schemaCache.GetCandidateSchemas(doc);
            IList<XmlSchemaReference> associatedSchemas = schemaCache.GetAssociatedSchemas(doc, options, errorHandler);
            XmlSchemaSetBuilder schemaSetBuilder = schemaCache.SchemaSetBuilder;

            IList<XmlSchemaReference> sources = schemaSetBuilder.Sources;
            sources.Clear();
            schemaSetBuilder.Candidates = candidateSchemas;
            foreach (XmlSchemaReference item in associatedSchemas)
                sources.Add(item);
            foreach (XmlSchemaReference schema in schemas)
                sources.Add(schema);
            schemaSetBuilder.Compile();

            var xmlSchemaSet = schemaSetBuilder.CompiledSet;

            //schemaCache.AddSet(xmlSchemaSet);
            //foreach (Exception error in schemaSetBuilder.Errors)
            //{
            //    compileErrors++;
            //    HandleSchemaException(error, XmlSeverityType.Error);
            //}
            //if (xmlSchemaSet != null && validationErrorsBySourceUri != null)
            //{
            //    foreach (XmlSchema item2 in xmlSchemaSet.Schemas())
            //    {
            //        if (!string.IsNullOrEmpty(item2.SourceUri) && !validationErrorsBySourceUri.ContainsKey(item2.SourceUri))
            //        {
            //            CacheEntry cacheEntry = schemaCache.FindSchema(item2);
            //            if (cacheEntry != null)
            //            {
            //                cacheEntry.Error = ErrorCode.None;
            //            }
            //        }
            //    }
            //}
            //ReportUnresolvedImports(xmlSchemaSet);
            //if (xmlSchemaSet == null)
            //{
            //    xmlSchemaSet = new XmlSchemaSet(nameTable);
            //}

            return xmlSchemaSet;

        }


        void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                var schemaSet = this.ObtainXmlSchemaSet(session.TextView);
                if (schemaSet == null)
                    return;

                var doc = this.CollectInfo(schemaSet);

                //dynamic xmlSchemaService = (IVsShell)Package.GetGlobalService(Marshal.GetTypeFromCLSID(Guid.Parse("1A5ACA9F-DFC2-44d4-8E3D-A2ADAC944FAB")));
                //IList<dynamic> schemas = xmlSchemaService.GetKnownSchemas();
                //var knownSchemas = schemas.Select(s => new { tns = (string)s.TargetNamespace, uri = (Uri)s.Location }).OrderBy(s => s.tns).ToArray();
                //Console.WriteLine(knownSchemas);

                //dynamic schemaSetBuilder = xmlSchemaService.CreateSchemaSetBuilder();


                //var shell = (IVsShell)Package.GetGlobalService(typeof(SVsShell));

                //if (!ErrorHandler.Succeeded(shell.GetPackageEnum(out var packagesIterator)))
                //    return;

                //var pp = new IVsPackage[1];
                //var packages = new List<IVsPackage>();
                //while (ErrorHandler.Succeeded(packagesIterator.Next(1, pp, out var taken)) && taken > 0)
                //    packages.Add(pp[0]);

                //var xmlEditorPackageId = Guid.Parse("87569308-4813-40A0-9CD0-D7A30838CA3F");
                //var xmlEditorVsPackage = packages.FirstOrDefault(p => p.GetType().TryGetCustomAttribute<GuidAttribute>(out var id) && Guid.Parse(id.Value) == xmlEditorPackageId);
                //var xmlEditorPackage = (Package)xmlEditorVsPackage;
                //var xmlEditorServiceContainer = (IServiceContainer)xmlEditorVsPackage;

                //var xmlSchemaServiceType = Marshal.GetTypeFromCLSID(Guid.Parse("1A5ACA9F-DFC2-44d4-8E3D-A2ADAC944FAB"));
                //var xmlSchemaServiceInstance = xmlEditorServiceContainer.GetService(xmlSchemaServiceType);
                //if (xmlSchemaServiceInstance != null)
                //{
                //    dynamic xmlSchemaService = xmlSchemaServiceInstance;
                //    IList<dynamic> schemas = xmlSchemaService.GetKnownSchemas();
                //    var knownSchemas = schemas.Select(s => new { tns = (string)s.TargetNamespace, uri = (Uri)s.Location }).OrderBy(s => s.tns).ToArray();
                //    Console.WriteLine(knownSchemas);
                //}

                var dte = (DTE)Package.GetGlobalService(typeof(DTE));
                var path = dte?.ActiveDocument?.FullName;

                // _repository.LoadXml(_textBuffer.CurrentSnapshot.GetText(), path);

                var triggerPoint = session.GetTriggerPoint(_textBuffer);

                var point = triggerPoint.GetPoint(session.TextView.TextSnapshot);
                var line = point.GetContainingLine();
                var lineNumber = line.LineNumber;
                var linePosition = point.Position - line.Start.Position;

                //var rootElement = _repository.GetRootElement();
                //var selectedElement = _repository.GetElement(rootElement, lineNumber, linePosition);
                //var selectedAttribute = _repository.GetAttribute(selectedElement, lineNumber, linePosition);

                var attr = doc.FindAttributeAt(lineNumber, linePosition);
                if (attr != null && attr.PossibleValues != null && (attr.TextLocation.Column + attr.Name.Length < linePosition))
                {
                    var compList = new List<Completion>();
                    foreach (string str in attr.PossibleValues)
                        compList.Add(new Completion(str, str, str, null, null));


                    var name = "Keys of " + attr.CompletionName;
                    completionSets.Add(new CompletionSet(
                        name,
                        name,
                        this.FindTokenSpanAtPosition(triggerPoint, session),
                        compList,
                        null)
                    );
                }
                else
                {
                    var compList = new List<Completion>();

                    var name = "Keys...";
                    completionSets.Add(new CompletionSet(
                        name,    //the non-localized title of the tab
                        name,    //the display title of the tab
                        this.FindTokenSpanAtPosition(triggerPoint, session),
                        compList,
                        null)
                    );
                }

                //List<string> strList = new List<string>();
                //strList.Add("addition");
                //strList.Add("adaptation");
                //strList.Add("subtraction");
                //strList.Add("summation");
                //m_compList = new List<Completion>();
                //foreach (string str in strList)
                //    m_compList.Add(new Completion(str, str, str, null, null));

                //completionSets.Add(new CompletionSet(
                //    "Tokens",    //the non-localized title of the tab
                //    "Tokens",    //the display title of the tab
                //    this.FindTokenSpanAtPosition(triggerPoint, session),
                //    m_compList,
                //    null)
                //);
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        }

        private ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint point, ICompletionSession session)
        {
            SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition) - 1;
            ITextStructureNavigator navigator = _sourceProvider.NavigatorService.GetTextStructureNavigator(_textBuffer);
            TextExtent extent = navigator.GetExtentOfWord(currentPoint);
            return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        }

        private bool m_isDisposed = false;

        void IDisposable.Dispose()
        {
            if (!m_isDisposed)
            {
                GC.SuppressFinalize(this);
                m_isDisposed = true;
            }
        }
    }
}
