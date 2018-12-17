using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using XmlKeyRefCompletion.Doc;

namespace XmlKeyRefCompletion
{
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("token completion handler")]
    [ContentType("xml")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class XmlKeyRefCompletionHandlerProvider : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        private readonly FileChangeListener _fileChangeListener;

        public XmlKeyRefCompletionHandlerProvider()
        {
            _fileChangeListener = new FileChangeListener();
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;

            Func<XmlKeyRefCompletionCommandHandler> createCommandHandler = delegate () { return new XmlKeyRefCompletionCommandHandler(textViewAdapter, textView, this); };
            textView.Properties.GetOrCreateSingletonProperty(typeof(XmlKeyRefCompletionCommandHandler).GUID, createCommandHandler);
        }
    }

    [Guid("41E35D93-7736-45F0-9A74-E972B775B560")]
    internal class XmlKeyRefCompletionCommandHandler : IOleCommandTarget
    {
        private IOleCommandTarget m_nextCommandHandler;
        private ITextView m_textView;
        private XmlKeyRefCompletionHandlerProvider m_provider;

        private ICompletionSession m_session;

        public XmlDocumentLoader DocumentDataLoader { get; private set; }

        internal XmlKeyRefCompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, XmlKeyRefCompletionHandlerProvider provider)
        {
            if (!ErrorHandler.Failed(textViewAdapter.GetBuffer(out var textLines)))
            {
                IVsUserData userData = textLines as IVsUserData;
                Guid id = typeof(XmlKeyRefCompletionCommandHandler).GUID;
                userData.SetData(ref id, this);
            }

            this.DocumentDataLoader = new XmlDocumentLoader(textView);

            m_textView = textView;
            m_provider = provider;

            //add the command to the command chain
            textViewAdapter.AddCommandFilter(this, out m_nextCommandHandler);

            textView.TextBuffer.PostChanged += (sender, ea) => this.DocumentDataLoader.ScheduleReloading(XmlDocumentLoader.EditTimeout);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            for (int i = 0; i < cCmds; i++)
            {
                var status = this.QueryStatusImpl(pguidCmdGroup, prgCmds[i]);

                if (status == VSConstants.E_FAIL)
                    return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                else
                    prgCmds[i].cmdf = (uint)status;
            }

            return VSConstants.S_OK;
        }


        private int QueryStatusImpl(Guid pguidCmdGroup, OLECMD cmd)
        {
            if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                // Debug.Print("Query {0}-{1}", "VSStd2KCmdID", (VSConstants.VSStd2KCmdID)cmd.cmdID);

                switch ((VSConstants.VSStd2KCmdID)cmd.cmdID)
                {
                    case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                    //case VSConstants.VSStd2KCmdID.PARAMINFO:
                    //case VSConstants.VSStd2KCmdID.QUICKINFO:
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                        return (int)OLECMDF.OLECMDF_ENABLED | (int)OLECMDF.OLECMDF_SUPPORTED;
                }
            }

            return VSConstants.E_FAIL;
        }

        // TODO: clean it up
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider))
            {
                return m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            //var tid = pguidCmdGroup;
            //var t = typeof(VSConstants).GetNestedTypes().FirstOrDefault(tt => tt.GUID == tid);
            //if (t.IsEnum)
            //{
            //    try
            //    {
            //        Debug.Print("cmd: {0}: {1}", t.Name, Enum.ToObject(t, nCmdID));
            //    }
            //    catch { }
            //}

                //make a copy of this so we can look at it after forwarding some commands
            uint commandID = nCmdID;
            char typedChar = char.MinValue;
            //make sure the input is a char before getting it
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
            {
                typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
            }

            //check for a commit character
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN
                || nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB
                || (char.IsWhiteSpace(typedChar) || char.IsPunctuation(typedChar)))
            {
                //check for a selection
                if (m_session != null && !m_session.IsDismissed)
                {
                    //if the selection is fully selected, commit the current session
                    if (m_session.SelectedCompletionSet.SelectionStatus.IsSelected)
                    {
                        m_session.Commit();
                        //also, don't add the character to the buffer

                        // this.DocumentDataLoader.ScheduleReloading(XmlDocumentLoader.EditTimeout);
                        return VSConstants.S_OK;
                    }
                    else
                    {
                        //if there is no selection, dismiss the session
                        m_session.Dismiss();
                    }
                }
            }

            //pass along the command so the char is added to the buffer
            int retVal = m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;
            if ((!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar))
                || (pguidCmdGroup == VSConstants.VSStd2K && (nCmdID == (uint)VSConstants.VSStd2KCmdID.AUTOCOMPLETE
                    || nCmdID == (uint)VSConstants.VSStd2KCmdID.COMPLETEWORD
                    || nCmdID == (uint)VSConstants.VSStd2KCmdID.SHOWMEMBERLIST
                )))
            {
                if (m_session == null || m_session.IsDismissed) // If there is no active session, bring up completion
                {
                    this.TriggerCompletion();
                    if (m_session != null && !((nCmdID == (uint)VSConstants.VSStd2KCmdID.AUTOCOMPLETE
                        || nCmdID == (uint)VSConstants.VSStd2KCmdID.COMPLETEWORD
                        || nCmdID == (uint)VSConstants.VSStd2KCmdID.SHOWMEMBERLIST
                    ))) // TODO: wtf?
                    {
                        m_session.Filter();
                    }
                }
                else    //the completion session is already active, so just filter
                {
                    m_session.Filter();
                }
                handled = true;
            }
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE   //redo the filter if there is a deletion
                || commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
            {
                if (m_session != null && !m_session.IsDismissed)
                    m_session.Filter();
                handled = true;
            }

            //if (
            //    (pguidCmdGroup == VSConstants.VSStd2K && (
            //        nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR||
            //        nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE ||
            //        nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB ||
            //        nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN
            //    )) || (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && (
            //        nCmdID == (uint)VSConstants.VSStd97CmdID.Cut ||
            //        nCmdID == (uint)VSConstants.VSStd97CmdID.Paste ||
            //        nCmdID == (uint)VSConstants.VSStd97CmdID.Undo ||
            //        nCmdID == (uint)VSConstants.VSStd97CmdID.Redo
            //    ))
            //    )
            //    this.DocumentDataLoader.ScheduleReloading(XmlDocumentLoader.EditTimeout);

            if (handled) return VSConstants.S_OK;
            return retVal;
        }

        private bool TriggerCompletion()
        {
            //the caret must be in a non-projection location 
            SnapshotPoint? caretPoint =
            m_textView.Caret.Position.Point.GetPoint(textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
            if (!caretPoint.HasValue)
            {
                return false;
            }

            m_session = m_provider.CompletionBroker.CreateCompletionSession(
                m_textView,
                caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
                true
            );

            //subscribe to the Dismissed event on the session 
            m_session.Dismissed += this.OnSessionDismissed;
            m_session.Start();
            // m_session.Filter();

            return true;
        }

        private void OnSessionDismissed(object sender, EventArgs e)
        {
            m_session.Dismissed -= this.OnSessionDismissed;
            m_session = null;
        }
    }
}
