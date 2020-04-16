//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Xml.Schema;
//using Microsoft.VisualStudio.ComponentModelHost;
//using Microsoft.VisualStudio.Editor;
//using Microsoft.VisualStudio.Package;
//using Microsoft.VisualStudio.Text.Editor;
//using Microsoft.VisualStudio.TextManager.Interop;
//using Microsoft.VisualStudio.XmlEditor;
//using Microsoft.XmlEditor;
//using Package = Microsoft.XmlEditor.Package;

//namespace XmlKeyRefCompletion.VsUtils
//{
//    internal class XmlSchemaSetHelper
//    {
//        private readonly XmlDocument _doc;

//        public XmlSchemaSetHelper(XmlDocument doc)
//        {
//            _doc = doc;
//        }

//        public static bool TryParseXmlDocFromTextView(ITextView textView, out bool isSchema, out XmlSchemaSetHelper helper)
//        {
//            IVsTextView view = textView.GetVsTextView();

//            XmlLanguageService langSvc = (XmlLanguageService)Package.GetGlobalService(Marshal.GetTypeFromCLSID(Guid.Parse("f6819a78-a205-47b5-be1c-675b3c7f0b8e")));

//            var source = langSvc.GetSource(view);

//            var doc = langSvc.GetParseTree(source, view, 0, 0, ParseReason.CodeSpan);

//            if (doc != null)
//            {
//                isSchema = true.Equals(doc.GetProperty("IsSchema"));
//                helper = isSchema ? null : new XmlSchemaSetHelper(doc);
//            }
//            else
//            {
//                isSchema = false;
//                helper = null;
//            }

//            return helper != null;
//        }

//        public bool TryResolveSchemaSetForXmlDocTextView(out XmlSchemaSet xmlSchemaSet, out bool retryLater)
//        {
//            var schemaCache = new MySchemaCache();

//            NamespaceFilter filter = nsuri => nsuri == "http://www.w3.org/2001/XMLSchema-instance";

//            var schemas = (IList<XmlSchemaReference>)_doc.GetProperty("Schemas").CallMethod("GetValidationSet", filter);

//            var options = new XmlProjectOptions();
//            var errorsList = new ErrorNodeList();
//            var errorHandler = new Microsoft.XmlEditor.ErrorHandler(errorsList);

//            IList<XmlSchemaReference> candidateSchemas = schemaCache.GetCandidateSchemas(_doc);
//            IList<XmlSchemaReference> associatedSchemas = schemaCache.GetAssociatedSchemas(_doc, options, errorHandler);
//            XmlSchemaSetBuilder schemaSetBuilder = schemaCache.SchemaSetBuilder;

//            IList<XmlSchemaReference> sources = schemaSetBuilder.Sources;
//            sources.Clear();
//            schemaSetBuilder.Candidates = candidateSchemas;

//            foreach (XmlSchemaReference item in associatedSchemas)
//                sources.Add(item);
//            foreach (XmlSchemaReference schema in schemas)
//                sources.Add(schema);

//            schemaSetBuilder.Compile();

//            xmlSchemaSet = schemaSetBuilder.CompiledSet;

//            retryLater = (sources.Count > 0 || candidateSchemas.Count > 0) && xmlSchemaSet.Count == 0;

//            return xmlSchemaSet != null && xmlSchemaSet.Count > 0;
//        }
//    }
//}
