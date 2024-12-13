using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OSY;
using System.Linq;
using UnityEngine;

public static class YouTubeAPI
{
    public static string GetLiveID(string LiveURL)
    {
        string LiveID = LiveURL.Replace("https://youtube.com/watch?v=", "").Replace("https://www.youtube.com/watch?v=", "").Replace("https://www.youtube.com/live/", "").Replace("https://youtube.com/live/", "");
        if (LiveID.Contains('?'))
            LiveID = LiveID.Split('?')[0];
        return LiveID;
    }
    public class YouTubeAPIRequester
    {
        private string apiKey;

        public YouTubeAPIRequester(string apiKey)
        {
            this.apiKey = apiKey;
            if (apiKey == null || apiKey == string.Empty)
            {
                Debug.LogError("api 초기화 실패");
            }
        }
        public async UniTask<string> GetLiveChatID(string liveID)
        {
            if (apiKey == null || apiKey == string.Empty)
            {
                Debug.LogError("api 초기화 안됨");
                return null;
            }
            JObject liveInfoJson = await Utils.GetJObject($"https://www.googleapis.com/youtube/v3/videos?id={liveID}&part=liveStreamingDetails&key={apiKey}", GameManager.instance.destroyCancellationToken);
            return (string)liveInfoJson["items"][0]["liveStreamingDetails"]["activeLiveChatId"];
        }
        public async UniTask<JObject> GetLiveChatInfo_JObject(string liveChatID, string pageToken = null)
        {
            if (apiKey == null || apiKey == string.Empty)
            {
                Debug.LogError("api 초기화 안됨");
                return null;
            }
            return await Utils.GetJObject($"https://www.googleapis.com/youtube/v3/liveChat/messages?liveChatId={liveChatID}&part=id,snippet,authorDetails&key={apiKey}" + (pageToken != null && pageToken != string.Empty ? string.Empty : $"&pageToken={pageToken}"), GameManager.instance.destroyCancellationToken);
        }
        public async UniTask<string> GetLiveChatInfo_JsonString(string liveChatID, string pageToken = null)
        {
            if (apiKey == null || apiKey == string.Empty)
            {
                Debug.LogError("api 초기화 안됨");
                return null;
            }
            return await Utils.GetJsonString($"https://www.googleapis.com/youtube/v3/liveChat/messages?liveChatId={liveChatID}&part=id,snippet,authorDetails&key={apiKey}" + (pageToken != null && pageToken != string.Empty ? $"&pageToken={pageToken}" : string.Empty), GameManager.instance.destroyCancellationToken);
        }
    }

}
