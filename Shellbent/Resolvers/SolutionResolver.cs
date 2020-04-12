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
		public enum CallbackReason
		{
			SolutionOpened,
			SolutionClosed
		}

		internal static SolutionResolver Create(Models.SolutionModel solutionModel)
		{
			return new SolutionResolver(solutionModel);
		}

		public SolutionResolver(Models.SolutionModel solutionModel)
			: base(new [] { "solution", "solution-name", "solution-dir", "item-name", "path" })
		{
			OnSolutionOpened(solutionModel.StartupSolution);

			solutionModel.SolutionOpened += OnSolutionOpened;
			solutionModel.SolutionClosed += OnSolutionClosed;
		}

		public override bool Available => solution != null;

		public override ChangedDelegate Changed { get; set; }

		public override bool ResolveBoolean(VsState state, string tag)
		{
			return state.Solution != null;
		}

		public override string Resolve(VsState state, string tag)
		{
			if (tag.StartsWith("path"))
			{
				return SplitFunction.Parse("path", Path.DirectorySeparatorChar, tag, new FileInfo(solution.FileName).Directory.FullName);
			}
			else if ((tag == "solution-name" || tag == "item-name") && state.Solution?.FullName != null)
				return Path.GetFileNameWithoutExtension(state.Solution.FullName);
			else if (tag == "solution-dir")
				return Path.GetFileName(Path.GetDirectoryName(state.Solution.FileName)) + "\\";
			else
				throw new InvalidOperationException();
		}

		public override bool SatisfiesDependency(Tuple<string, string> d)
		{
			if (string.IsNullOrEmpty(d.Item2))
			{
				return false;
			}
			else
			{
				bool result = solution != null && new Regex(
					Regex.Escape(d.Item2).Replace(@"\*", ".*").Replace(@"\?", "."),
					RegexOptions.IgnoreCase | RegexOptions.Singleline).IsMatch(solution.FullName);

				return result;
			}
		}

		private void OnSolutionOpened(Solution solution)
		{
			this.solution = solution;
		}

		private void OnSolutionClosed()
		{
			this.solution = null;
		}

		private Solution solution;
	}
}
