﻿using Microsoft.VisualStudio;
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

namespace XmlKeyRefCompletion
{
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("token completion handler")]
    [ContentType("xml")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class TestCompletionHandlerProvider : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        public TestCompletionHandlerProvider()
        {
            //FileChangeListener fileChangeListener = new FileChangeListener(this, this.taskmgr);



        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;

            Func<TestCompletionCommandHandler> createCommandHandler = delegate () { return new TestCompletionCommandHandler(textViewAdapter, textView, this); };
            textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);
        }
    }


    internal class TestCompletionCommandHandler : IOleCommandTarget
    {
        private IOleCommandTarget m_nextCommandHandler;
        private ITextView m_textView;
        private TestCompletionHandlerProvider m_provider;
        private ICompletionSession m_session;

        internal TestCompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView, TestCompletionHandlerProvider provider)
        {
            this.m_textView = textView;
            this.m_provider = provider;

            //add the command to the command chain
            textViewAdapter.AddCommandFilter(this, out m_nextCommandHandler);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            for (int i = 0; i < cCmds; i++)
            {
                // var status = QueryStatusImpl(pguidCmdGroup, prgCmds[i]);

                //private int QueryStatusImpl(Guid pguidCmdGroup, OLECMD cmd)
                //{
                if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
                {
                    var cmd = prgCmds[i];
                    // Debug.Print("Query {0}-{1}", "VSStd2KCmdID", (VSConstants.VSStd2KCmdID)cmd.cmdID);

                    switch ((VSConstants.VSStd2KCmdID)cmd.cmdID)
                    {
                        case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                        case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        //case VSConstants.VSStd2KCmdID.PARAMINFO:
                        //case VSConstants.VSStd2KCmdID.QUICKINFO:
                        case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                            prgCmds[i].cmdf = (uint)((int)OLECMDF.OLECMDF_ENABLED | (int)OLECMDF.OLECMDF_SUPPORTED);
                            break;
                        //return (int)OLECMDF.OLECMDF_ENABLED | (int)OLECMDF.OLECMDF_SUPPORTED;
                        default:
                            return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                    }
                }
                else
                {
                    return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                }
                //return VSConstants.E_FAIL;
                //}

                //if (status == VSConstants.E_FAIL)
                //    return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                //else
                //    prgCmds[i].cmdf = (uint)status;
            }
            return VSConstants.S_OK;

            //return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider))
            {
                return m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            //Debug.Print("Exec {0}-{1}", nCmdID, nCmdexecopt);
            //var cmdGroups = new Dictionary<Guid, Type>() {
            //    { VSConstants.VSStd2K, typeof(VSConstants.VSStd2KCmdID) },
            //    { VSConstants.VsStd2010, typeof(VSConstants.VSStd2010CmdID) },
            //    { VSConstants.VsStd11, typeof(VSConstants.VSStd11CmdID) },
            //    { VSConstants.VsStd12, typeof(VSConstants.VSStd12CmdID) },
            //    { VSConstants.VsStd14, typeof(VSConstants.VSStd14CmdID) },
            //    { VSConstants.VsStd15, typeof(VSConstants.VSStd15CmdID) },
            //    //{ VSConstants.VsStd97, typeof(VSConstants.VSStd97CmdID) },
            //};

            //if (cmdGroups.TryGetValue(pguidCmdGroup, out var t))
            //{
            //    Debug.Print("\t{0}:{1}", t, Enum.ToObject(t, nCmdID));
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
