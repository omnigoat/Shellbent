﻿using EnvDTE;
using EnvDTE80;
using System;

namespace Atma.TitleBarNone.Resolvers
{
	public class IDEResolver : Resolver
	{
		public struct IDEState
		{
			public dbgDebugMode Mode;
		}

		public enum CallbackReason
		{
			StartupComplete,
			ModeChanged,
			ShutdownInitiated
		}

		public IDEResolver(DTE2 dte, Action<CallbackReason, IDEState> callback)
			: base(new[] { "ide-name", "ide-mode" })
		{
			m_DTE = dte;
			m_DebuggerEvents = m_DTE.Events.DebuggerEvents;

			m_VsMode = m_DTE.Debugger.CurrentMode;
			m_Callback = callback;

			m_DTE.Events.DTEEvents.OnStartupComplete += () => OnExecutionChanged(true);
			m_DTE.Events.DTEEvents.OnBeginShutdown += () => OnExecutionChanged(false);

			m_DebuggerEvents.OnEnterDesignMode += (dbgEventReason e) => OnModeChanged(dbgDebugMode.dbgDesignMode);
			m_DebuggerEvents.OnEnterRunMode += (dbgEventReason e) => OnModeChanged(dbgDebugMode.dbgRunMode);
			m_DebuggerEvents.OnEnterBreakMode += (dbgEventReason e, ref dbgExecutionAction action) => OnModeChanged(dbgDebugMode.dbgBreakMode);
		}

		public static Resolver Create(DTE2 dte, Action<CallbackReason, IDEState> callback)
		{
			return new IDEResolver(dte, callback);
		}

		public override bool ResolveBoolean(VsState state, string tag)
		{
			if (tag == "ide-mode")
				return (state.Mode != dbgDebugMode.dbgDesignMode);
			else
				return true;
		}

		public override string Resolve(VsState state, string tag)
		{
			if (tag == "ide-name")
				return "Microsoft Visual Studio";
			else if (tag == "ide-mode")
				return GetModeTitle(state);
			else
				return null;
		}

		private void OnExecutionChanged(bool going)
		{
			if (going)
				m_Callback(CallbackReason.StartupComplete, new IDEState { Mode = m_VsMode });
			else
				m_Callback(CallbackReason.ShutdownInitiated, new IDEState { Mode = m_VsMode });
		}

		private void OnModeChanged(dbgDebugMode mode)
		{
			m_VsMode = mode;
			m_Callback(CallbackReason.ModeChanged, new IDEState { Mode = m_VsMode });
		}

		private string GetModeTitle(VsState state)
		{
			if (state.Mode == dbgDebugMode.dbgDesignMode)
				return "";
			else if (state.Mode == dbgDebugMode.dbgRunMode)
				return "(Running)";
			else
				return "(Debugging)";
		}

		private DTE2 m_DTE;
		private EnvDTE.DebuggerEvents m_DebuggerEvents;
		private dbgDebugMode m_VsMode;
		private Action<CallbackReason, IDEState> m_Callback;
	}
}
