using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio;
using VisualStudioEvents = Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell;
using EnvDTE;

namespace Shellbent.Models
{
	public class SolutionModel
	{
		public SolutionModel()
		{
			VisualStudioEvents.SolutionEvents.OnBeforeOpenSolution += 
				(object sender, VisualStudioEvents.BeforeOpenSolutionEventArgs e) =>
				{
					PerformSolutionLookup();
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
					solutionFilepath = null;
					solutionName = null;
				};
		}

		private bool PerformSolutionLookup()
		{
			// grab solution information via main thread
			return ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				var solService = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
				if (solService == null)
					return false;

				ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object solutionIsOpen));
				if (solutionIsOpen is bool solutionIsOpenBool && solutionIsOpenBool)
				{
					{ // get solution filepath
						ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_SolutionFileName, out object outarg));
						if (outarg is string filenameString)
							solutionFilepath = filenameString;
					}

					{ // get solution name
						ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_SolutionBaseName, out object outarg));
						if (outarg is string name)
							solutionName = name;
					}

					return true;
				}

				return false;
			});
		}

		public void EvaluateSolutionState()
		{
			if (PerformSolutionLookup())
			{
				SolutionBeforeOpen?.Invoke(solutionFilepath);
				SolutionAfterOpen?.Invoke();
			}
		}

		public string SolutionName => solutionName;
		public string SolutionFilepath => solutionFilepath;

		public delegate void SolutionBeforeOpenedDelegate(string solutionFilepath);
		public delegate void SolutionAfterOpenDelegate();
		public delegate void SolutionClosedDelegate();

		public event SolutionBeforeOpenedDelegate SolutionBeforeOpen;
		public event SolutionAfterOpenDelegate SolutionAfterOpen;
		public event SolutionClosedDelegate SolutionAfterClosed;

		private string solutionName;
		private string solutionFilepath;
	}
}
