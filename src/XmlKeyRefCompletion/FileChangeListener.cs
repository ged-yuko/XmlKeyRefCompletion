﻿using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XmlKeyRefCompletion.Doc;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using MsVsShell = Microsoft.VisualStudio.Shell;

namespace XmlKeyRefCompletion
{
    public class FileChangeListener : IDisposable, IVsRunningDocTableEvents // IVsRunningDocTableEvents3, IVsRunningDocTableEvents2, IVsTrackProjectDocumentsEvents2, IVsSolutionEvents
    {
        // RDT
        private readonly uint _rdtCookie;
        private readonly RunningDocumentTable _rdt;

        public FileChangeListener()
        {
            IOleServiceProvider sp = Package.GetGlobalService(typeof(IOleServiceProvider)) as IOleServiceProvider;
            if (sp == null)
                return;

            _rdt = new RunningDocumentTable(new ServiceProvider(sp));
            if (_rdt == null) return;

            _rdtCookie = _rdt.Advise(this);
        }

        private void ReloadXmlDoCompletionData(uint docCookie)
        {
            var doc = _rdt.GetDocumentInfo(docCookie);
            // var docTextBufferAdapter = doc.DocData as IVsTextBuffer;
            var textLines = doc.DocData as IVsTextLines;

            IVsUserData userData = textLines as IVsUserData;
            if (userData != null)
            {
                Guid id = typeof(XmlKeyRefCompletionCommandHandler).GUID;
                userData.GetData(ref id, out var cmdHandler);
                (cmdHandler as XmlKeyRefCompletionCommandHandler)?.DocumentDataLoader.ScheduleReloading(XmlDocumentLoader.InitTimeout);
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).

                    try
                    {
                        if (_rdtCookie != 0)
                            _rdt.Unadvise(_rdtCookie);
                    }
                    finally { }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~FileChangeListener() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion

        #region handlers

        int IVsRunningDocTableEvents.OnAfterSave(uint docCookie)
        {
            this.ReloadXmlDoCompletionData(docCookie);

            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        #endregion
    }
}
