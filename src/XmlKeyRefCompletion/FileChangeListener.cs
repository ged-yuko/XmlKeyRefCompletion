//using Microsoft.VisualStudio.Shell;
//using Microsoft.VisualStudio.Shell.Interop;
//using System;
//using System.Collections.Generic;
//using System.ComponentModel.Composition;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace XmlKeyRefCompletion
//{
//    [Export(typeof(SaveEventsService))]
//    internal class FileChangeListener : IVsRunningDocTableEvents3, IVsRunningDocTableEvents2, IVsRunningDocTableEvents, IVsTrackProjectDocumentsEvents2, IVsSolutionEvents, IDisposable
//    {
//        private IServiceProvider serviceProvider;

//        private IdleTaskManager taskMgr;

//        private RunningDocumentTable runningDocTable;

//        private uint trackDocEventsCookie;

//        private bool disposed;

//        private uint vsRDTEventscookie;

//        private uint solutionEventsCookie;

//        internal FileChangeListener(IServiceProvider site, IdleTaskManager taskmgr)
//        {
//            this.serviceProvider = site;
//            this.taskMgr = taskmgr;
//            this.runningDocTable = new RunningDocumentTable(site);
//            this.AdviseRunningDocumentTableEvents(true);
//            this.AdviceTrackProjectEvents(true);
//            this.AdviseSolutionEvents(true);
//        }

//        private void AdviceTrackProjectEvents(bool advice)
//        {
//            IVsTrackProjectDocuments2 vsTrackProjectDocuments = this.serviceProvider.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments2;
//            if (vsTrackProjectDocuments != null)
//            {
//                try
//                {
//                    if (advice)
//                    {
//                        if (!ErrorHandler.Succeeded(vsTrackProjectDocuments.AdviseTrackProjectDocumentsEvents(this, out this.trackDocEventsCookie)))
//                        {
//                        }
//                    }
//                    else if (this.trackDocEventsCookie != 0u)
//                    {
//                        ErrorHandler.Succeeded(vsTrackProjectDocuments.UnadviseTrackProjectDocumentsEvents(this.trackDocEventsCookie));
//                        this.trackDocEventsCookie = 0u;
//                    }
//                }
//                catch (COMException)
//                {
//                }
//            }
//        }

//        private void AdviseRunningDocumentTableEvents(bool advise)
//        {
//            if (advise)
//            {
//                if (this.vsRDTEventscookie == 0u)
//                {
//                    this.vsRDTEventscookie = this.runningDocTable.Advise(this);
//                    return;
//                }
//            }
//            else if (this.vsRDTEventscookie != 0u)
//            {
//                this.runningDocTable.Unadvise(this.vsRDTEventscookie);
//                this.vsRDTEventscookie = 0u;
//            }
//        }
//    }
//}
