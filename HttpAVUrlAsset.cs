using AVFoundation;
using CoreFoundation;

namespace MrClock0ff.AVFoundation
{
	public class HttpAVUrlAsset : AVAssetResourceLoaderDelegate
	{
		private readonly HttpClient _httpClient;
		private readonly HttpAVUrlAssetOptions _options;
		private readonly string _url;

		public static AVAsset Create(string url, HttpAVUrlAssetOptions options)
		{
			NSUrl nsUrl = new NSUrl("relay://me");
			AVUrlAsset urlAsset = new AVUrlAsset(nsUrl);
			urlAsset.ResourceLoader.SetDelegate(new HttpAVUrlAsset(url, options), DispatchQueue.DefaultGlobalQueue);
			return urlAsset;
		}

		public override bool ShouldWaitForLoadingOfRequestedResource(AVAssetResourceLoader resourceLoader,
			AVAssetResourceLoadingRequest loadingRequest)
		{
			try
			{
				if (loadingRequest.ContentInformationRequest != null)
				{
					HandleContentInfoRequest(loadingRequest).Wait();
					return true;
				}

				if (loadingRequest.DataRequest != null)
				{
					HandleDataRequest(loadingRequest).Wait();
					return true;
				}
			}
			catch (Exception ex)
			{
				_options.ErrorHandler?.Invoke(ex).Wait();
			}

			return false;
		}

		protected HttpAVUrlAsset(string url, HttpAVUrlAssetOptions options)
		{
			_url = url;
			_options = options;
			HttpClientHandler handler = new HttpClientHandler();
			handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
			_httpClient = new HttpClient(handler);
		}

		private async Task HandleContentInfoRequest(AVAssetResourceLoadingRequest loadingRequest)
		{
			try
			{
				AVAssetResourceLoadingDataRequest dataRequest = loadingRequest.DataRequest;
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _url);

				if (dataRequest != null)
				{
					long lowerRange = dataRequest.RequestedOffset;
					long upperRange = lowerRange + dataRequest.RequestedLength - 1;
					string rangeValue = $"bytes={lowerRange}-{upperRange}";
					request.Headers.Add("Range", rangeValue);
				}

				if (_options.RequestOverrideHandler != null)
				{
					await _options.RequestOverrideHandler.Invoke(request);
				}

				HttpResponseMessage response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
				}

				AVAssetResourceLoadingContentInformationRequest infoRequest = loadingRequest.ContentInformationRequest;

				if (infoRequest != null)
				{
					infoRequest.ContentType = response.Content.Headers.ContentType?.MediaType;
					infoRequest.ContentLength = response.Content.Headers.ContentLength ?? 0;
					infoRequest.ByteRangeAccessSupported = response.Headers.AcceptRanges.Count > 0;
				}

				if (infoRequest is { ByteRangeAccessSupported: false })
				{
					byte[] data = await response.Content.ReadAsByteArrayAsync();
					dataRequest?.Respond(NSData.FromArray(data));

					return;
				}

				loadingRequest.FinishLoading();
			}
			catch (Exception ex)
			{
				loadingRequest.FinishLoadingWithError(new NSExceptionError(ex));
			}
		}

		private async Task HandleDataRequest(AVAssetResourceLoadingRequest loadingRequest)
		{
			try
			{
				AVAssetResourceLoadingDataRequest dataRequest = loadingRequest.DataRequest;
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _url);

				if (dataRequest is { RequestsAllDataToEndOfResource: false })
				{
					long lowerRange = dataRequest.RequestedOffset;
					long upperRange = lowerRange + dataRequest.RequestedLength - 1;
					string rangeValue = $"bytes={lowerRange}-{upperRange}";
					request.Headers.Add("Range", rangeValue);
				}

				if (_options.RequestOverrideHandler != null)
				{
					await _options.RequestOverrideHandler.Invoke(request);
				}

				HttpResponseMessage response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
				}

				byte[] data = await response.Content.ReadAsByteArrayAsync();
				dataRequest?.Respond(NSData.FromArray(data));

				loadingRequest.FinishLoading();
			}
			catch (Exception ex)
			{
				loadingRequest.FinishLoadingWithError(new NSExceptionError(ex));
			}
		}
	}
}