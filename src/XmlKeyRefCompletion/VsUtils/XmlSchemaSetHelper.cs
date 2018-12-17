using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.XmlEditor;
using Microsoft.XmlEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Package = Microsoft.XmlEditor.Package;

namespace XmlKeyRefCompletion.VsUtils
{
    static class XmlSchemaSetHelper
    {
        public static XmlSchemaSet ResolveSchemaSetForXmlDocTextView(ITextView textView)
        {
            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            IVsEditorAdaptersFactoryService editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            IVsTextView view = editorFactory.GetViewAdapter(textView);

            XmlLanguageService langSvc = (XmlLanguageService)Package.GetGlobalService(Marshal.GetTypeFromCLSID(Guid.Parse("f6819a78-a205-47b5-be1c-675b3c7f0b8e")));

            var source = langSvc.GetSource(view);

            var schemaCache = new MySchemaCache();

            var doc = langSvc.GetParseTree(source, view, 0, 0, ParseReason.CodeSpan);
            if (doc == null || true.Equals(doc.GetProperty("IsSchema")))
                return null;

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

            return xmlSchemaSet;
        }
    }
}
