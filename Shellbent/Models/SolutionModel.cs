using VisualStudioEvents = Microsoft.VisualStudio.Shell.Events;

namespace Shellbent.Models
{
	public class SolutionModel
	{
		public SolutionModel()
		{
			VisualStudioEvents.SolutionEvents.OnBeforeOpenSolution += 
				(object sender, VisualStudioEvents.BeforeOpenSolutionEventArgs e) =>
				{
					SolutionBeforeOpen?.Invoke(e.SolutionFilename);
				};

			VisualStudioEvents.SolutionEvents.OnAfterOpenSolution +=
				(object sender, VisualStudioEvents.OpenSolutionEventArgs e) =>
				{
					SolutionAfterOpen?.Invoke();
				};

			VisualStudioEvents.SolutionEvents.OnAfterCloseSolution += (object sender, System.EventArgs e) =>
				{
					SolutionAfterClosed?.Invoke();
				};
		}

		public void SetOpenSolution()
		{
			SolutionAfterOpen?.Invoke();
		}

		public delegate void SolutionBeforeOpenedDelegate(string solutionFilepath);
		public delegate void SolutionAfterOpenDelegate();
		public delegate void SolutionClosedDelegate();

		public event SolutionBeforeOpenedDelegate SolutionBeforeOpen;
		public event SolutionAfterOpenDelegate SolutionAfterOpen;
		public event SolutionClosedDelegate SolutionAfterClosed;
	}
}
