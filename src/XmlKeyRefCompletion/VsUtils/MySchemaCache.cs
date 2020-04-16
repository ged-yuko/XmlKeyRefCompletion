//using Microsoft.VisualStudio.ComponentModelHost;
//using Microsoft.VisualStudio.Shell.Interop;
//using Microsoft.VisualStudio.XmlEditor;
//using Microsoft.XmlEditor;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Threading.Tasks;
//using System.Xml.Schema;
//using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;

//namespace XmlKeyRefCompletion.VsUtils
//{
//    internal static class ReflectionHelpers
//    {
//        public static object CallMethod(this object obj, string methodName, params object[] args)
//        {
//            var method = obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//            return method.Invoke(obj, args == null || args.Length == 0 ? null : args);
//        }

//        public static object GetProperty(this object obj, string propertyName)
//        {
//            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//            return prop.GetValue(obj);
//        }
//    }

//    internal class MySchemaCache
//    {
//        private object _schemaCache;

//        public XmlSchemaSetBuilder SchemaSetBuilder
//        {
//            get
//            {
//                return (XmlSchemaSetBuilder)_schemaCache.GetProperty("SchemaSetBuilder");
//            }
//        }

//        public MySchemaCache()
//        {
//            // test:
//            //var p0 = (Package)Package.GetGlobalService(typeof(Package));

//            //var sp = Package.GetGlobalService(typeof(System.IServiceProvider));
//            //var p1 = (sp as System.IServiceProvider).GetService(typeof(Package));

//            //var componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
//            //var p2 = componentModel.GetService<Package>();



//            var shell = (IVsShell)Package.GetGlobalService(typeof(SVsShell));
            
//            if (!ErrorHandler.Succeeded(shell.GetPackageEnum(out var packagesIterator)))
//                return;

//            var pp = new IVsPackage[1];
//            var packages = new List<IVsPackage>();
//            while (ErrorHandler.Succeeded(packagesIterator.Next(1, pp, out var taken)) && taken > 0)
//                packages.Add(pp[0]);

//            var xmlEditorPackageId = Guid.Parse("87569308-4813-40A0-9CD0-D7A30838CA3F");
//            var xmlEditorVsPackage = packages.OfType<Package>().FirstOrDefault();

//            _schemaCache = xmlEditorVsPackage.CallMethod("GetCache");
//        }

//        internal IList<XmlSchemaReference> GetCandidateSchemas(object doc)
//        {
//            return (IList<XmlSchemaReference>)_schemaCache.CallMethod("GetCandidateSchemas", doc);
//        }

//        internal IList<XmlSchemaReference> GetAssociatedSchemas(object doc, object options, object errorHandler)
//        {
//            return (IList<XmlSchemaReference>)_schemaCache.CallMethod("GetAssociatedSchemas", doc, options, errorHandler);
//        }

//        internal void AddSet(XmlSchemaSet xmlSchemaSet)
//        {
//            _schemaCache.CallMethod("AddSet", xmlSchemaSet);
//        }
//    }
//}

