using Shellbent.Settings;
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
		internal static SolutionResolver Create(Models.SolutionModel solutionModel)
		{
			return new SolutionResolver(solutionModel);
		}

		public SolutionResolver(Models.SolutionModel solutionModel)
			: base(new [] { "solution", "item-name", "solution-name", "solution-path", "path" })
		{
			solutionModel.SolutionAfterOpen += OnAfterOpenSolution;
			solutionModel.SolutionAfterClosed += OnAfterSolutionClosed;
		}

		public override bool Available =>
			!string.IsNullOrEmpty(solutionName) && !string.IsNullOrEmpty(solutionFilepath);

		public override ChangedDelegate Changed { get; set; }

		protected override bool ResolvableImpl(VsState state, string tag)
		{
			return state.Solution != null;
		}

		public override string Resolve(VsState state, string tag)
		{
			if (tag.StartsWith("path"))
			{
				return SplitFunction.Parse("path", Path.DirectorySeparatorChar, tag, new FileInfo(solutionFilepath).Directory.FullName);
			}
			else if ((tag == "solution-name" || tag == "item-name") && state.Solution?.FullName != null)
				return Path.GetFileNameWithoutExtension(state.Solution.FullName);
			else if (tag == "solution-path")
				return Path.GetFileName(Path.GetDirectoryName(state.Solution.FileName)) + "\\";
			else
				throw new InvalidOperationException();
		}

		protected override bool SatisfiesPredicateImpl(string tag, string value)
		{
			switch (tag)
			{
				case "solution": return true;
				case "solution-name":
				case "item-name": return GlobMatch(value, solutionName);
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
