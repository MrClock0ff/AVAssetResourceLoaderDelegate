namespace MrClock0ff.AVFoundation
{
	public class HttpAVUrlAssetOptions
	{
		public Func<Exception, Task> ErrorHandler { get; set; }

		public Func<HttpRequestMessage, Task> RequestOverrideHandler { get; set; }
	}
}