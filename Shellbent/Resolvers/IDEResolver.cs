using EnvDTE;
using EnvDTE80;
using System;

namespace Shellbent.Resolvers
{
	public class IDEResolver : Resolver
	{
		public static IDEResolver Create(Models.IDEModel ideModel)
		{
			return new IDEResolver(ideModel);
		}

		public IDEResolver(Models.IDEModel ideModel)
			: base(new[] { "ide-name", "ide-mode" })
		{
			ideModel.IdeModeChanged += OnModeChanged;
			vsMode = ideModel.VsMode;
		}

		public override bool Available => true;

		public override ChangedDelegate Changed { get; set; }

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

		private void OnModeChanged(dbgDebugMode mode)
		{
			if (mode != vsMode)
			{
				vsMode = mode;
				Changed?.Invoke(this);
			}
		}

		private string GetModeTitle(VsState state)
		{
			if (state.Mode == dbgDebugMode.dbgDesignMode)
				return string.Empty;
			else if (state.Mode == dbgDebugMode.dbgRunMode)
				return "(Running)";
			else
				return "(Debugging)";
		}

		private dbgDebugMode vsMode;
	}
}
