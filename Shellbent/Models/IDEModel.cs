using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shellbent.Models
{
	public class IDEModel
	{
		public IDEModel(DTE dte)
		{
			this.dte = dte;

			dteEvents = this.dte.Events.DTEEvents;
			dteEvents.OnStartupComplete += () => StartupComplete?.Invoke();
			dteEvents.OnBeginShutdown += () => ShutdownInitiated?.Invoke();

			debuggerEvents = this.dte.Events.DebuggerEvents;
			debuggerEvents.OnEnterDesignMode += (dbgEventReason e) => OnModeChanged(dbgDebugMode.dbgDesignMode);
			debuggerEvents.OnEnterRunMode += (dbgEventReason e) => OnModeChanged(dbgDebugMode.dbgRunMode);
			debuggerEvents.OnEnterBreakMode += (dbgEventReason e, ref dbgExecutionAction action) => OnModeChanged(dbgDebugMode.dbgBreakMode);

			VsMode = this.dte.Debugger.CurrentMode;

			// callbacks for IDE windows being opened
			if (dte.Events is Events2 events2)
			{
				windowVisibilityEvents = events2.WindowVisibilityEvents;
				windowVisibilityEvents.WindowShowing += (Window w) => WindowShown?.Invoke(w);
			}
		}

		public dbgDebugMode VsMode { get; set; }

		public delegate void StartupCompleteDelegate();
		public delegate void ShutdownInitiatedDelegate();
		public delegate void IdeModeChangedDelegate(dbgDebugMode mode);
		public delegate void WindowShownDelegate(Window window);

		public event StartupCompleteDelegate StartupComplete;
		public event ShutdownInitiatedDelegate ShutdownInitiated;
		public event IdeModeChangedDelegate IdeModeChanged;
		public event WindowShownDelegate WindowShown;

		private void OnModeChanged(dbgDebugMode mode)
		{
			VsMode = mode;
			IdeModeChanged?.Invoke(VsMode);
		}

		// we need to save these fields on DTE as members because otherwise they
		// can get garbage-collected
		private readonly DTE dte;
		private readonly DTEEvents dteEvents;
		private readonly DebuggerEvents debuggerEvents;
		private readonly WindowVisibilityEvents windowVisibilityEvents;
	}
}
