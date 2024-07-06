using Shellbent.Settings;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shell;

namespace Shellbent.Resolvers
{
	public class SolutionResolver : Resolver
	{
		public SolutionResolver(Models.SolutionModel solutionModel)
			: base(new [] { "solution", "solution-name", "solution-path" })
		{
			this.solutionModel = solutionModel;
		}

		private Models.SolutionModel solutionModel;

		public override bool Available =>
			!string.IsNullOrEmpty(solutionModel.SolutionFilepath);

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
				case "solution-name": return solutionModel.SolutionName;
				case "solution-path": return ResolverUtils.PathFunction(
					"solution-path", Path.DirectorySeparatorChar, tag,
					Path.GetDirectoryName(solutionModel.SolutionFilepath));
				default: return string.Empty;
			}
		}

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "solution": return true;
				case "solution-name": return GlobMatch(value, solutionModel.SolutionName);
				case "solution-path": return GlobMatch(value, solutionModel.SolutionFilepath);
				default: return false;
			}
		}
	}
}
