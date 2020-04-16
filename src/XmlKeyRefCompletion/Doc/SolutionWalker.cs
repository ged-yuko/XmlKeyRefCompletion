// Note: add references to the following assemblies of the Visual Studio SDK or from the GAC:
// Microsoft.VisualStudio.OLE.Interop.dll
// Microsoft.VisualStudio.Shell.Interop.dll
using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSHierarchyAddin
{
    public class SolutionWalker : IDTExtensibility2
    {
        private const int S_OK = 0;
        private const uint VSITEMID_NIL = 0xFFFFFFFF;
        private const uint VSITEMID_ROOT = 0xFFFFFFFE;

        private DTE2 _applicationObject;
        private AddIn _addInInstance;

        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            _applicationObject = (DTE2)application;
            _addInInstance = (AddIn)addInInst;

            switch (connectMode)
            {
                case ext_ConnectMode.ext_cm_Startup:

                    // Do nothing; OnStartupComplete will be called
                    break;

                case ext_ConnectMode.ext_cm_AfterStartup:

                    InitializeAddIn();
                    break;
            }
        }

        public void OnStartupComplete(ref Array custom)
        {
            InitializeAddIn();
        }

        private void InitializeAddIn()
        {
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider;
            IVsHierarchy hierarchy;

            try
            {
                serviceProvider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_applicationObject;

                hierarchy = (IVsHierarchy)GetService(serviceProvider, typeof(SVsSolution), typeof(IVsSolution));

                // Traverse the nodes of the hierarchy
                ProcessHierarchy(hierarchy);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ProcessHierarchy(IVsHierarchy hierarchy)
        {
            // Traverse the nodes of the hierarchy from the root node
            ProcessHierarchyNodeRecursively(hierarchy, VSITEMID_ROOT);
        }

        private void ProcessHierarchyNodeRecursively(IVsHierarchy hierarchy, uint itemId)
        {
            int result;
            IntPtr nestedHiearchyValue = IntPtr.Zero;
            uint nestedItemIdValue = 0;
            object value = null;
            uint visibleChildNode;
            Guid nestedHierarchyGuid;
            IVsHierarchy nestedHierarchy;

            // First, guess if the node is actually the root of another hierarchy (a project, for example)
            nestedHierarchyGuid = typeof(IVsHierarchy).GUID;
            result = hierarchy.GetNestedHierarchy(itemId, ref nestedHierarchyGuid, out nestedHiearchyValue, out nestedItemIdValue);

            if (result == S_OK && nestedHiearchyValue != IntPtr.Zero && nestedItemIdValue == VSITEMID_ROOT)
            {
                // Get the new hierarchy
                nestedHierarchy = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(nestedHiearchyValue) as IVsHierarchy;
                System.Runtime.InteropServices.Marshal.Release(nestedHiearchyValue);

                if (nestedHierarchy != null)
                {
                    ProcessHierarchy(nestedHierarchy);
                }
            }
            else // The node is not the root of another hierarchy, it is a regular node
            {
                ShowNodeName(hierarchy, itemId);

                // Get the first visible child node
                result = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out value);

                while (result == S_OK && value != null)
                {
                    if (value is int && (uint)(int)value == VSITEMID_NIL)
                    {
                        // No more nodes
                        break;
                    }
                    else
                    {
                        visibleChildNode = Convert.ToUInt32(value);

                        // Enter in recursion
                        ProcessHierarchyNodeRecursively(hierarchy, visibleChildNode);

                        // Get the next visible sibling node
                        value = null;
                        result = hierarchy.GetProperty(visibleChildNode, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out value);
                    }
                }
            }
        }

        private void ShowNodeName(IVsHierarchy hierarchy, uint itemId)
        {
            int result;
            object value = null;
            string name = "";
            string canonicalName = "";

            result = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_Name, out value);

            if (result == S_OK && value != null)
            {
                name = value.ToString();
            }

            result = hierarchy.GetCanonicalName(itemId, out canonicalName);

            MessageBox.Show("Name: " + name + "\r\n" + "Canonical name: " + canonicalName);
        }

        private object GetService(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider, System.Type serviceType, System.Type interfaceType)
        {
            object service = null;
            IntPtr servicePointer;
            int hr = 0;
            Guid serviceGuid;
            Guid interfaceGuid;

            serviceGuid = serviceType.GUID;
            interfaceGuid = interfaceType.GUID;

            hr = serviceProvider.QueryService(ref serviceGuid, ref interfaceGuid, out servicePointer);
            if (hr != S_OK)
            {
                System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
            }
            else if (servicePointer != IntPtr.Zero)
            {
                service = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(servicePointer);
                System.Runtime.InteropServices.Marshal.Release(servicePointer);
            }
            return service;
        }

        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
        {
        }

        public void OnAddInsUpdate(ref Array custom)
        {
        }

        public void OnBeginShutdown(ref Array custom)
        {
        }

    }
}