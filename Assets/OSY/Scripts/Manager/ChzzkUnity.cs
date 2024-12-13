using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;
using MessageEventArgs = WebSocketSharp.MessageEventArgs;
using WebSocket = WebSocketSharp.WebSocket;

public class ChzzkUnity : MonoBehaviour
{
    public static ChzzkUnity instance;
    public TMP_InputField inputChannelID;
    public LiveStatus liveStatus;
    //WSS(WS 말고 WSS) 쓰려면 필요함.
    private enum SslProtocolsHack
    {
        Tls = 192,
        Tls11 = 768,
        Tls12 = 3072
    }

    string cid;
    string token;


    public WebSocket socket = null;
    string wsURL = "wss://kr-ss3.chat.naver.com/chat";

    float timer = 0f;
    bool running = false;

    string heartbeatRequest = "{\"ver\":\"2\",\"cmd\":0}";
    string heartbeatResponse = "{\"ver\":\"2\",\"cmd\":10000}";

    public Action<UnityWebRequestException> OnConnectError = (ex) => { };
    public Action<Profile, string, string> OnChat = (profile, chatID, str) => { };
    public Action<Profile, string, string, DonationExtras> OnDonation = (profile, chatID, str, extra) => { };
    public Action<Profile, string, string, SubscriptionExtras> onSubscription = (profile, chatID, str, extra) => { };

    public void Awake()
    {
        instance = this;
        var init = destroyCancellationToken.IsCancellationRequested;
    }

    public void removeAllOnMessageListener()
    {
        OnChat = (profile, chatID, str) => { };
    }

    public void removeAllOnDonationListener()
    {
        OnDonation = (profile, chatID, str, extra) => { };
    }

    //20초에 한번 HeartBeat 전송해야 함.
    //서버에서 먼저 요청하면 안 해도 됨.
    //TimeScale에 영향 안 받기 위해서 Fixed
    public void FixedUpdate()
    {
        if (running)
        {
            timer += Time.unscaledDeltaTime;
            if (timer > 15)
            {
                timer = 0;
                socket.Send(heartbeatRequest);
            }
        }
    }

    public async UniTask<ChannelInfo> GetChannelInfo(string channelId)
    {
        string URL = $"https://api.chzzk.naver.com/service/v1/channels/{channelId}";
        UnityWebRequest request = UnityWebRequest.Get(URL);
        await request.SendWebRequest();
        ChannelInfo channelInfo = null;
        Debug.Log(request.downloadHandler.text);
        if (request.result == UnityWebRequest.Result.Success)
        {
            //Cid 획득
            channelInfo = JsonUtility.FromJson<ChannelInfo>(request.downloadHandler.text);
        }
        return channelInfo;
    }

    public async UniTask<LiveStatus> GetLiveStatus(string channelId)
    {
        string URL = $"https://api.chzzk.naver.com/polling/v2/channels/{channelId}/live-status";
        await UniTask.SwitchToMainThread();
        UnityWebRequest request = UnityWebRequest.Get(URL);
        try
        {
            await request.SendWebRequest();
        }
        catch (UnityWebRequestException ex)
        {
            OnConnectError(ex);
            return null;
        }
        LiveStatus liveStatus = null;
        if (request.result == UnityWebRequest.Result.Success)
        {
            //Cid 획득
            liveStatus = JsonUtility.FromJson<LiveStatus>(request.downloadHandler.text);
        }
        return liveStatus;
    }

    public async UniTask<AccessTokenResult> GetAccessToken(string cid)
    {
        string URL = $"https://comm-api.game.naver.com/nng_main/v1/chats/access-token?channelId={cid}&chatType=STREAMING";
        await UniTask.SwitchToMainThread();
        UnityWebRequest request = UnityWebRequest.Get(URL);
        try
        {
            await request.SendWebRequest();
        }
        catch (UnityWebRequestException ex)
        {
            GameManager.instance.ErrorPOPUP.SetActive(true);
            GameManager.instance.ErrorPOPUPText.text = $"따흐흑ㅠㅠ 에러!! (혹시 연령제한 방송인가요?)\n\n{ex.Message}";
            return null;
        }
        AccessTokenResult accessTokenResult = null;
        if (request.result == UnityWebRequest.Result.Success)
        {
            //Cid 획득
            accessTokenResult = JsonUtility.FromJson<AccessTokenResult>(request.downloadHandler.text);
        }

        return accessTokenResult;
    }

    public async UniTask Connect()
    {
        liveStatus = await GetLiveStatus(inputChannelID.text);
        cid = liveStatus.content.chatChannelId;
        AccessTokenResult accessTokenResult = await GetAccessToken(cid);
        if (accessTokenResult == null)
            return;
        token = accessTokenResult.content.accessToken;

        socket = new WebSocket(wsURL);
        //wss라서 ssl protocol을 활성화 해줘야 함.
        var sslProtocolHack = (System.Security.Authentication.SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);
        socket.SslConfiguration.EnabledSslProtocols = sslProtocolHack;

        //이벤트 등록
        socket.OnMessage += Recv;
        socket.OnClose += CloseConnect;
        socket.OnOpen += OnStartChat;

        //연결
        socket.Connect();
    }

    public void Connect(string channelId)
    {
        inputChannelID.text = channelId;
        UniTask.RunOnThreadPool(Connect, true, destroyCancellationToken).Forget();
    }


    void Recv(object sender, MessageEventArgs e)
    {
        try
        {
            IDictionary<string, object> data = JsonConvert.DeserializeObject<IDictionary<string, object>>(e.Data);

            JArray bdy;
            //Cmd에 따라서
            switch ((long)data["cmd"])
            {
                case 0://HeartBeat Request
                    //하트비트 응답해줌.
                    socket.Send(heartbeatResponse);
                    //서버가 먼저 요청해서 응답했으면 타이머 초기화해도 괜찮음.
                    timer = 0;
                    break;
                case 93101://Chat
                    //Debug.Log($"{(long)data["cmd"]}: \n{e.Data}");
                    bdy = (JArray)data["bdy"];
                    UniTask.RunOnThreadPool(async () =>
                    {
                        foreach (JObject chatInfo in bdy)
                        {
                            //프로필이.... json이 아니라 string으로 들어옴.
                            string profileText = chatInfo["profile"].ToString();
                            profileText = profileText.Replace("\\", "");
                            Profile profile = JsonUtility.FromJson<Profile>(profileText);

                            //Debug.Log(profileText);
                            string chatTxt = chatInfo["msg"].ToString().Trim();
                            string chatID = (string)chatInfo["uid"] + (string)chatInfo["msgTime"];
                            //Debug.Log(profile.nickname + ": " + chatTxt);
                            if (GameManager.instance.SpawnMinDonationAmount <= 0 && (profile?.streamingProperty?.subscription == null || GameManager.instance.SpawnMinSubscriptionMonth <= profile.streamingProperty.subscription.accumulativeMonth))
                                OnChat(profile, chatID, chatTxt);
                            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
                        }
                    }, true, destroyCancellationToken).Forget();
                    break;
                case 93102://Donation
                    bdy = (JArray)data["bdy"];
                    UniTask.RunOnThreadPool(async () =>
                    {
                        foreach (JObject chatInfo in bdy)
                        {
                            //프로필 스트링 변환
                            string profileText = chatInfo["profile"].ToString();
                            profileText = profileText.Replace("\\", "");
                            Profile profile = JsonUtility.FromJson<Profile>(profileText);

                            Debug.Log(chatInfo);
                            var msgTypeCode = int.Parse(chatInfo["msgTypeCode"].ToString());
                            //도네이션과 관련된 데이터는 extra
                            string extraText = null;
                            if (chatInfo.ContainsKey("extra"))
                            {
                                extraText = chatInfo["extra"].ToString();
                            }
                            else if (chatInfo.ContainsKey("extras"))
                            {
                                extraText = chatInfo["extras"].ToString();
                            }
                            string chatID = (string)chatInfo["uid"] + (string)chatInfo["msgTime"]; // 도네이션 json요소도 똑같을까?
                            extraText = extraText.Replace("\\", "");
                            switch (msgTypeCode)
                            {
                                case 10: // Donation
                                    DonationExtras donation = JsonUtility.FromJson<DonationExtras>(extraText);
                                    if (GameManager.instance.SpawnMinDonationAmount <= donation.payAmount && (profile?.streamingProperty?.subscription == null || GameManager.instance.SpawnMinSubscriptionMonth <= profile.streamingProperty.subscription.accumulativeMonth))
                                        OnDonation(profile, chatID, chatInfo["msg"].ToString(), donation);
                                    break;
                                case 11: // Subscription
                                    SubscriptionExtras subscription = JsonUtility.FromJson<SubscriptionExtras>(extraText);
                                    if (profile?.streamingProperty?.subscription == null || GameManager.instance.SpawnMinSubscriptionMonth <= profile.streamingProperty.subscription.accumulativeMonth)
                                        onSubscription(profile, chatID, chatInfo["msg"].ToString(), subscription);
                                    break;
                                default:
                                    /*Debug.LogError($"MessageTypeCode-{msgTypeCode} is not supported");
                                    Debug.LogError(bdyObject.ToString());*/
                                    break;
                            }
                            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
                        }
                    }, true, destroyCancellationToken).Forget();
                    break;
                case 93006://Temporary Restrict 블라인드 처리된 메세지.
                    Debug.Log($"{(long)data["cmd"]}: \n{e.Data}");
                    break;
                case 94008://Blocked Message(CleanBot) 차단된 메세지.
                    Debug.Log($"{(long)data["cmd"]}: \n{e.Data}");
                    break;
                case 94201://Member Sync 멤버 목록 동기화.
                    Debug.Log($"{(long)data["cmd"]}: \n{e.Data}");
                    break;
                case 10000://HeartBeat Response 하트비트 응답.
                case 10100://Token ACC
                    break;//Nothing to do
                default:
                    //내가 놓친 cmd가 있나?
                    Debug.Log(data["cmd"]);
                    Debug.Log(e.Data);
                    break;
            }
        }

        catch (Exception er)
        {
            Debug.LogException(er);
        }
    }

    void CloseConnect(object sender, CloseEventArgs e)
    {
        Debug.Log(e.Reason);
        Debug.Log(e.Code);
        Debug.Log(e);

        try
        {
            if (socket == null) return;

            if (socket.IsAlive) socket.Close();
        }
        catch (Exception ex)
        {
            Debug.Log(ex.StackTrace);
        }
    }

    void OnStartChat(object sender, EventArgs e)
    {
        Debug.Log($"OPENED : {cid} + {token}");

        string message = $"{{\"ver\":\"2\",\"cmd\":100,\"svcid\":\"game\",\"cid\":\"{cid}\",\"bdy\":{{\"uid\":null,\"devType\":2001,\"accTkn\":\"{token}\",\"auth\":\"READ\"}},\"tid\":1}}";
        timer = 0;
        running = true;
        socket.Send(message);
    }

    public void OnApplicationQuit()
    {
        removeAllOnDonationListener();
        removeAllOnMessageListener();
        if (socket != null && socket.IsAlive)
        {
            socket.Close();
            socket = null;
        }
    }



    public void StopListening()
    {
        socket.Close();
        socket = null;
    }

    [Serializable]
    public class LiveStatus
    {
        public int code;
        public string message;
        public Content content;

        [Serializable]
        public class Content
        {
            public string liveTitle;
            public string status;
            public int concurrentUserCount;
            public int accumulateCount;
            public bool paidPromotion;
            public bool adult;
            public string chatChannelId;
            public string categoryType;
            public string liveCategory;
            public string liveCategoryValue;
            public string livePollingStatusJson;
            public string faultStatus;
            public string userAdultStatus;
            public bool chatActive;
            public string chatAvailableGroup;
            public string chatAvailableCondition;
            public int minFollowerMinute;
        }
    }

    [Serializable]
    public class AccessTokenResult
    {
        public int code;
        public string message;
        public Content content;
        [Serializable]
        public class Content
        {
            public string accessToken;

            [Serializable]
            public class TemporaryRestrict
            {
                public bool temporaryRestrict;
                public int times;
                public int duration;
                public int createdTime;
            }
            public bool realNameAuth;
            public string extraToken;
        }
    }

    [Serializable]
    public class Profile
    {
        public string userIdHash;
        public string nickname;
        public string profileImageUrl;
        public string userRoleCode;
        public string badge;
        public string title;
        public string verifiedMark;
        public List<String> activityBadges;
        [Serializable]
        public class StreamingProperty
        {
            [Serializable]
            public class Subscription
            {
                public int accumulativeMonth;
                public int tier;
                [Serializable]
                public class Badge
                {
                    public string imageUrl;
                }
                public Badge badge;
            }
            public Subscription subscription;
        }
        public StreamingProperty streamingProperty;
    }


    [Serializable]
    public class DonationExtras
    {
        System.Object emojis;
        public bool isAnonymous;
        public string payType;
        public int payAmount;
        public string streamingChannelId;
        public string nickname;
        public string osType;
        public string donationType;

        public List<WeeklyRank> weeklyRankList;
        [Serializable]
        public class WeeklyRank
        {
            public string userIdHash;
            public string nickName;
            public bool verifiedMark;
            public int donationAmount;
            public int ranking;
        }
        public WeeklyRank donationUserWeeklyRank;
    }
    [Serializable]
    public class SubscriptionExtras
    {
        public int month;
        public string tierName;
        public string nickname;
        public int tierNo;
    }

    [Serializable]
    public class ChannelInfo
    {
        public int code;
        public string message;
        public Content content;

        [Serializable]
        public class Content
        {
            public string channelId;
            public string channelName;
            public string channelImageUrl;
            public bool verifiedMark;
            public string channelType;
            public string channelDescription;
            public int followerCount;
            public bool openLive;
        }
    }
}