using EnvDTE;

namespace Shellbent.Models
{
	public class SolutionModel
	{
		public SolutionModel(DTE dte)
		{
			this.dte = dte;

			solutionEvents = this.dte.Events.SolutionEvents;

			solutionEvents.Opened += () => SolutionOpened?.Invoke(this.dte.Solution);
			solutionEvents.AfterClosing += () => SolutionClosed?.Invoke();
			
			
			StartupSolution = dte.Solution;
		}

		// this solution was the solution that was loaded when this plugin was
		// initialized - i.e., the solution specified via command line. sometimes
		// we'll have missed the Opened event by the time we're initialized, so we
		// need to allow resolvers a chance to look at the already-loaded solution.
		public Solution StartupSolution { get; }

		public delegate void SolutionOpenedDelegate(Solution solution);
		public delegate void SolutionClosedDelegate();

		public event SolutionOpenedDelegate SolutionOpened;
		public event SolutionClosedDelegate SolutionClosed;

		// members
		private DTE dte;
		private SolutionEvents solutionEvents;
	}
}
