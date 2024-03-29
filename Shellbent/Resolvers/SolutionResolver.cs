﻿using Shellbent.Settings;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shellbent.Resolvers
{
	public class SolutionResolver : Resolver
	{
		public SolutionResolver(Models.SolutionModel solutionModel)
			: base(new [] { "solution", "solution-name", "solution-path" })
		{
			solutionModel.SolutionAfterOpen += OnAfterOpenSolution;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available =>
			!string.IsNullOrEmpty(solutionName) && !string.IsNullOrEmpty(solutionFilepath);

		protected override bool ResolvableImpl(VsState state, string tag)
		{
			return state.Solution != null;
		}

		public override string Resolve(VsState state, string tag)
		{
			ResolverUtils.ExtractTag(tag, out string t);

			switch (t)
			{
				case "solution": return "loaded";
				case "solution-name": return solutionName;
				case "solution-path": return ResolverUtils.PathFunction("solution-path", Path.DirectorySeparatorChar, tag, Path.GetDirectoryName(solutionFilepath));
				default: return string.Empty;
			}
		}

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "solution": return true;
				case "solution-name": return GlobMatch(value, solutionName);
				case "solution-path": return GlobMatch(value, solutionFilepath);
				default: return false;
			}
		}

		private void OnAfterOpenSolution()
		{
			IVsSolution solution = (IVsSolution)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(IVsSolution));

			solution.GetProperty((int)__VSPROPID.VSPROPID_SolutionBaseName, out object the_solutionName);
			solution.GetProperty((int)__VSPROPID.VSPROPID_SolutionFileName, out object the_solutionFilepath);

			solutionName = the_solutionName as string;
			solutionFilepath = the_solutionFilepath as string;
		}

		private void OnAfterSolutionClosed()
		{
			solutionName = null;
			solutionFilepath = null;
		}

		private string solutionName;
		private string solutionFilepath;
	}
}
