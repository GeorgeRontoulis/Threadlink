namespace Threadlink.Utilities.Networking
{
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using UnityEngine.Networking;

	internal static class NetworkingOperations
	{
		internal static TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
		{
			var tcs = new TaskCompletionSource<UnityWebRequest>();
			asyncOp.completed += obj => { tcs.SetResult(asyncOp.webRequest); };
			return tcs.Task.GetAwaiter();
		}

		internal static bool FacedConnectionError(this UnityWebRequest request)
		{
			return request.result.Equals(UnityWebRequest.Result.ConnectionError);
		}
	}
}