using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;

public class YoutubeUnity : MonoBehaviour
{
    public static YoutubeUnity instance;
    public TMP_InputField inputLiveID;
    [SerializeField] private TMP_InputField inputAPIKey;

    public YouTubeAPI.YouTubeAPIRequester APIRequester;
    private string LiveChatID;

    public Action<LiveChatInfo.Chat> OnChatEvent = (chatInfo) => { };
    public Action<LiveChatInfo.Chat> OnSuperChatEvent = (chatInfo) => { };
    public Action<LiveChatInfo.Chat> OnSuperStickerEvent = (chatInfo) => { };
    public Action<LiveChatInfo.Chat> OnNewSponsorEvent = (chatInfo) => { };
    public Action<LiveChatInfo.Chat> OnMemberMilestoneChatEvent = (chatInfo) => { };
    public void Awake()
    {
        instance = this;
    }
    public async UniTask Connect(CancellationToken token)
    {
        await UniTask.SwitchToMainThread();
        APIRequester = new YouTubeAPI.YouTubeAPIRequester(inputAPIKey.text);
        LiveChatID = await APIRequester.GetLiveChatID(YouTubeAPI.GetLiveID(inputLiveID.text));
        Debug.Log(LiveChatID);
        await UniTask.SwitchToThreadPool();
        await UpdateChatListTask(token);
    }
    private async UniTask UpdateChatListTask(CancellationToken token)
    {
        float updateTime = 5f;
        string pageToken = null;
        const string StringTextMessageEvent = "textMessageEvent";
        const string StringSuperChatEvent = "superChatEvent";
        const string StringSuperStickerEvent = "superStickerEvent";
        const string StringNewSponsorEvent = "newSponsorEvent";
        const string StringMemberMilestoneChatEvent = "memberMilestoneChatEvent";
        await UniTask.SwitchToMainThread();
        while (!token.IsCancellationRequested)
        {
            LiveChatInfo liveChatInfo = JsonConvert.DeserializeObject<LiveChatInfo>(await APIRequester.GetLiveChatInfo_JsonString(LiveChatID, pageToken));
            try
            {
                if (pageToken == null)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), true, PlayerLoopTiming.FixedUpdate, token, true);
                    continue;
                }
                if (liveChatInfo.chats != null && liveChatInfo.chats.Count > 0)
                {
                    foreach (LiveChatInfo.Chat chat in liveChatInfo.chats)
                    {
                        switch (chat.snippet.type)
                        {
                            case StringTextMessageEvent:
                                OnChatEvent(chat);
                                break;
                            case StringSuperChatEvent:
                                OnSuperChatEvent(chat);
                                break;
                            case StringSuperStickerEvent:
                                OnSuperStickerEvent(chat);
                                break;
                            case StringNewSponsorEvent:
                                OnNewSponsorEvent(chat);
                                break;
                            case StringMemberMilestoneChatEvent:
                                OnMemberMilestoneChatEvent(chat);
                                break;
                        }
                        await UniTask.Delay(TimeSpan.FromSeconds(updateTime / liveChatInfo.chats.Count), true, PlayerLoopTiming.FixedUpdate, token, true);
                    }
                }
                else
                    await UniTask.Delay(TimeSpan.FromSeconds(updateTime), true, PlayerLoopTiming.FixedUpdate, token, true);
            }
            finally
            {
                pageToken = liveChatInfo.nextPageToken;
            }
        }
    }


    public class LiveChatInfo //Root
    {
        public string kind;
        public string etag;
        public int pollingIntervalMillis;
        public PageInfo pageInfo;
        public string nextPageToken;
        [JsonProperty("items")]
        public List<Chat> chats;

        public class PageInfo
        {
            public int totalResults;
            public int resultsPerPage;
        }

        public class Chat
        {
            public string kind;
            public string etag;
            public string id;
            public Snippet snippet;
            public AuthorDetails authorDetails;

            public class Snippet
            {
                public string type;
                public string liveChatId;
                public string authorChannelId;
                public DateTime publishedAt;
                public bool hasDisplayContent;
                public string displayMessage;
                public TextMessageDetails textMessageDetails;
                public SuperChatDetails superChatDetails;
                public SuperStickerDetails superStickerDetails;
                public NewSponsorDetails newSponsorDetails;
                public MemberMilestoneChatDetails memberMilestoneChatDetails;

                public class TextMessageDetails
                {
                    public string messageText;
                }
                public class SuperChatDetails
                {
                    public long amountMicros;
                    public string currency;
                    public string amountDisplayString;
                    public string userComment;
                    public int tier;
                }
                public class SuperStickerDetails
                {
                    public System.Object superStickerMetadata;
                    public long amountMicros;
                    public string currency;
                    public string amountDisplayString;
                    public int tier;
                }
                public class NewSponsorDetails
                {
                    public string memberLevelName;
                    public bool isUpgrade;
                }
                public class MemberMilestoneChatDetails
                {
                    public string userComment;
                    public int memeberMonth;
                    public string memberLevelName;
                }
            }

            public class AuthorDetails
            {
                public string channelId;
                public string channelUrl;
                public string displayName;
                public string profileImageUrl;
                public bool isVerified;
                public bool isChatOwner;
                public bool isChatSponsor;
                public bool isChatModerator;
            }
        }
    }




}
