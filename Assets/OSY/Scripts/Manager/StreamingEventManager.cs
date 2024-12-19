using Cysharp.Threading.Tasks;
using OSY;
using System.Text.RegularExpressions;
using System.Threading;
using Unity.Entities;
using UnityEngine;

public class StreamingEventManager : MonoBehaviour
{
    public CancellationTokenSource ChzzkCTS;
    // Start is called before the first frame update
    public void Start()
    {
        SteveEventSystem SteveEventSystemHandle = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SteveEventSystem>();
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.SwitchToMainThread();
            await Utils.WaitUntil(() => GameManager.instance?.settingUI != null, Utils.YieldCaches.UniTaskYield, destroyCancellationToken);
            bool isOnSettingUI = GameManager.instance.settingUI.activeInHierarchy;
            //bool isOnChannelInfoUI = GameManager.instance.channelInfoUI.activeInHierarchy;
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift))
                {
                    GameManager.instance.settingUI.SetActive(!isOnSettingUI);
                    //GameManager.instance.channelInfoUI.GetComponent<Image>().color = GameManager.instance.settingUI.activeInHierarchy ? new Color(0, 0, 0, 0.7f) : new Color(0, 0, 0, 0.3f);
                    /*GameManager.instance.unknownDonationParentsTransform.parent.GetComponent<Image>().enabled = GameManager.instance.settingUI.activeInHierarchy;
                    GameManager.instance.unknownDonationParentsTransform.parent.GetComponentInChildren<TMP_Text>().enabled = GameManager.instance.settingUI.activeInHierarchy;*/
                }
                else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.LeftShift))
                {
                    isOnSettingUI = GameManager.instance.settingUI.activeInHierarchy;
                }

                /*if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.RightShift))
                {
                    GameManager.instance.channelInfoUI.SetActive(!isOnChannelInfoUI);
                }
                else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightShift))
                {
                    isOnChannelInfoUI = GameManager.instance.channelInfoUI.activeInHierarchy;
                }*/

                /*if (Input.GetKeyDown(KeyCode.RightControl))
                {
                    SteveEventSystemHandle.OnCalm.Invoke();
                }*/
                await Utils.YieldCaches.UniTaskYield;
                try
                {
                    if (destroyCancellationToken.IsCancellationRequested) return;
                }
                catch (MissingReferenceException)
                {
                    return;
                }
            }
        }, true, destroyCancellationToken).Forget();

        #region ※치지직 이벤트
        if (ChzzkUnity.instance != null)
        {
            ChzzkUnity.instance.OnConnectError = (ex) =>
            {
                GameManager.instance.ErrorPOPUP.SetActive(true);
                GameManager.instance.ErrorPOPUPText.text = $"따흐흑ㅠㅠ 에러!!\n\n{ex.Message}";
            };
            ChzzkUnity.instance.OnChat = async (profile, chatID, chatText) =>
            {
                await UniTask.SwitchToMainThread();
                await OnInit(profile.nickname, chatText, SteveEventSystemHandle, profile, profile.streamingProperty?.subscription?.accumulativeMonth ?? 0, 0);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatID, chatText, 5f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };
            ChzzkUnity.instance.OnDonation = async (profile, chatID, chatText, extra) =>
            {
                await UniTask.SwitchToMainThread();
                if (profile == null) //익명 후원
                {
                    SteveEventSystemHandle.OnChat.Invoke(null, chatText, 0, extra.payAmount);
                    //new GameManager.ChatInfo(chatID, "<b><color=orange>" + chatText + "</color></b>", 10f, GameManager.instance.unknownDonationParentsTransform, true);
                    return;
                }
                int hash = Animator.StringToHash(profile.nickname);
                await OnInit(profile.nickname, chatText, SteveEventSystemHandle, profile, profile.streamingProperty?.subscription?.accumulativeMonth ?? 0, extra.payAmount);
                //SteveEventSystemHandle.OnChat.Invoke(profile.nickname., chatText, 24, extra.payAmount);

                //new GameManager.ChatInfo(chatID, "<b><color=orange>" + chatText + "</color></b>", 10f, GameManager.instance.unknownDonationParentsTransform, true);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatID, "<b><color=orange>" + chatText + "</color></b>", 10f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };
            ChzzkUnity.instance.onSubscription = async (profile, chatID, chatText, extra) =>
            {
                await UniTask.SwitchToMainThread();
                if (profile == null) return;
                int hash = Animator.StringToHash(profile.nickname);
                await OnInit(profile.nickname, chatText, SteveEventSystemHandle, profile, extra.month, 0);
                SteveEventSystemHandle.OnSubscription.Invoke(hash, extra.month);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatID, "<b><color=red>" + chatText + "</color></b>", 10f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };

            async UniTask OnInit(string userName, string chatText, SteveEventSystem SteveEventSystemHandle, ChzzkUnity.Profile profile, int subMonth, int payAmount)
            {
                string userNameComponent = $"{PlatformNameCache.Chzzk}\n{profile.nickname}";
                //Utils.hashMemory.TryAdd(hash, profile.nickname);
                bool isInit = !GameManager.instance.viewerInfos.ContainsKey(userNameComponent);
                float addLifeTime = 0;

                if (isInit)
                {
                    GameManager.instance.viewerInfos.Add(userNameComponent, new GameManager.ViewerInfo(userNameComponent, subMonth));
                    /*GameManager.instance.spawnOrderQueue.Enqueue(new GameManager.SpawnOrder(hash,
                        initForce: new float3(Utils.GetRandom(GameManager.instance.SpawnMinSpeed.x, GameManager.instance.SpawnMaxSpeed.x), Utils.GetRandom(GameManager.instance.SpawnMinSpeed.y, GameManager.instance.SpawnMaxSpeed.y), 0)));*/
                    SteveEventSystemHandle.OnSpawn.Invoke(userNameComponent);
                    await Utils.YieldCaches.UniTaskYield;
                    await Utils.YieldCaches.UniTaskYield;
                    await Utils.YieldCaches.UniTaskYield;
                }
                addLifeTime = payAmount > 0 ? GameManager.instance.peepoConfig.addLifeTime : 86400;

                SteveEventSystemHandle.OnChat.Invoke(userNameComponent, chatText, addLifeTime, payAmount);
                //GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform.localScale = Vector3.one * GameManager.instance.chatBubbleSize;
            }
        }
        #endregion

        #region ※유튜브 이벤트
        if (YoutubeUnity.instance != null)
        {
            YoutubeUnity.instance.OnChatEvent = async (chatInfo) =>
            {
                await UniTask.SwitchToMainThread();
                int hash = Animator.StringToHash($"{chatInfo.authorDetails.channelId}{GameManager.instance.nameSpliter}{chatInfo.authorDetails.displayName}");
                await OnInit(chatInfo.authorDetails, chatInfo.snippet.displayMessage, SteveEventSystemHandle);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatInfo.id, chatInfo.snippet.displayMessage, 5f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };
            YoutubeUnity.instance.OnSuperChatEvent = async (chatInfo) =>
            {
                await UniTask.SwitchToMainThread();
                int hash = Animator.StringToHash($"{chatInfo.authorDetails.channelId}{GameManager.instance.nameSpliter}{chatInfo.authorDetails.displayName}");
                await OnInit(chatInfo.authorDetails, chatInfo.snippet.displayMessage, SteveEventSystemHandle, int.Parse(Regex.Replace(chatInfo.snippet.superChatDetails.amountDisplayString, @"\D", "")));
                //SteveEventSystemHandle.OnDonation.Invoke(hash, int.Parse(Regex.Replace(chatInfo.snippet.superChatDetails.amountDisplayString, @"\D", "")));

                //new GameManager.ChatInfo(chatID, "<b><color=orange>" + chatText + "</color></b>", 10f, GameManager.instance.unknownDonationParentsTransform, true);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatInfo.id, "<b><color=orange>" + chatInfo.snippet.displayMessage + "</color></b>", 10f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };
            YoutubeUnity.instance.OnSuperStickerEvent = async (chatInfo) =>
            {
                await UniTask.SwitchToMainThread();
                int hash = Animator.StringToHash($"{chatInfo.authorDetails.channelId}{GameManager.instance.nameSpliter}{chatInfo.authorDetails.displayName}");
                await OnInit(chatInfo.authorDetails, chatInfo.snippet.displayMessage, SteveEventSystemHandle, int.Parse(Regex.Replace(chatInfo.snippet.superChatDetails.amountDisplayString, @"\D", "")));
                //SteveEventSystemHandle.OnDonation.Invoke(hash, int.Parse(Regex.Replace(chatInfo.snippet.superStickerDetails.amountDisplayString, @"\D", "")));

                //new GameManager.ChatInfo(chatID, "<b><color=orange>" + chatText + "</color></b>", 10f, GameManager.instance.unknownDonationParentsTransform, true);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatInfo.id, "<b><color=orange>" + chatInfo.snippet.displayMessage + "</color></b>", 10f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };
            YoutubeUnity.instance.OnNewSponsorEvent = async (chatInfo) =>
            {
                await UniTask.SwitchToMainThread();
                int hash = Animator.StringToHash($"{chatInfo.authorDetails.channelId}{GameManager.instance.nameSpliter}{chatInfo.authorDetails.displayName}");
                await OnInit(chatInfo.authorDetails, chatInfo.snippet.displayMessage, SteveEventSystemHandle);
                SteveEventSystemHandle.OnSubscription.Invoke(hash, chatInfo.snippet.memberMilestoneChatDetails.memeberMonth);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatInfo.id, "<b><color=red>" + chatInfo.snippet.displayMessage + "</color></b>", 10f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };
            YoutubeUnity.instance.OnMemberMilestoneChatEvent = async (chatInfo) =>
            {
                await UniTask.SwitchToMainThread();
                int hash = Animator.StringToHash($"{chatInfo.authorDetails.channelId}{GameManager.instance.nameSpliter}{chatInfo.authorDetails.displayName}");
                await OnInit(chatInfo.authorDetails, chatInfo.snippet.displayMessage, SteveEventSystemHandle, chatInfo.snippet.memberMilestoneChatDetails.memeberMonth);
                SteveEventSystemHandle.OnSubscription.Invoke(hash, chatInfo.snippet.memberMilestoneChatDetails.memeberMonth);
                //GameManager.instance.viewerInfos[hash].chatInfos.Add(new GameManager.ChatInfo(chatInfo.id, "<b><color=red>" + chatInfo.snippet.displayMessage + "</color></b>", 10f, GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform));
            };

            async UniTask OnInit(YoutubeUnity.LiveChatInfo.Chat.AuthorDetails authorDetails, string chatText, SteveEventSystem SteveEventSystemHandle, int subMonth = 0, int payAmount = 0)
            {
                string userNameComponent = $"{PlatformNameCache.YouTube}\n{authorDetails.displayName}";
                //string authVal = $"{authorDetails.channelId}{GameManager.instance.nameSpliter}{userNameComponent}";
                //Utils.hashMemory.TryAdd(hash, authVal);
                bool isInit = !GameManager.instance.viewerInfos.ContainsKey(userNameComponent);
                float addLifeTime = 0;

                if (isInit)
                {
                    GameManager.instance.viewerInfos.Add(userNameComponent, new GameManager.ViewerInfo(userNameComponent, subMonth));
                    /*GameManager.instance.spawnOrderQueue.Enqueue(new GameManager.SpawnOrder(hash,
                        initForce: new float3(Utils.GetRandom(GameManager.instance.SpawnMinSpeed.x, GameManager.instance.SpawnMaxSpeed.x), Utils.GetRandom(GameManager.instance.SpawnMinSpeed.y, GameManager.instance.SpawnMaxSpeed.y), 0)));*/
                    SteveEventSystemHandle.OnSpawn.Invoke(userNameComponent);
                    await Utils.YieldCaches.UniTaskYield;
                    await Utils.YieldCaches.UniTaskYield;
                    await Utils.YieldCaches.UniTaskYield;
                }
                addLifeTime = payAmount > 0 ? GameManager.instance.peepoConfig.addLifeTime : 86400;

                SteveEventSystemHandle.OnChat.Invoke(userNameComponent, chatText, addLifeTime, payAmount);
                //GameManager.instance.viewerInfos[hash].chatBubbleObjects.transform.localScale = Vector3.one * GameManager.instance.chatBubbleSize;
            }
        }
        #endregion
    }
    public void StartChzzk()
    {
        UniTask.RunOnThreadPool(async () =>
        {
            if (ChzzkUnity.instance != null)
            {
                if (ChzzkUnity.instance.socket != null && ChzzkUnity.instance.socket.IsAlive)
                {
                    ChzzkCTS?.Cancel();
                    ChzzkUnity.instance.socket.Close();
                    ChzzkUnity.instance.socket = null;
                }
                ChzzkCTS = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                await ChzzkUnity.instance.Connect();
                /*UniTask.RunOnThreadPool(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    while (!ChzzkCTS.IsCancellationRequested)
                    {
                        ChzzkUnity.instance.liveStatus = await ChzzkUnity.instance.GetLiveStatus(ChzzkUnity.instance.inputChannelID.text);
                        GameManager.instance.channelViewerCount.text = $"시청자 수: {ChzzkUnity.instance.liveStatus.content.concurrentUserCount}";
                        GameManager.instance.peepoCountText.text = $"채팅 참여자 수: {GameManager.instance.viewerInfos.Count}";
                        await UniTask.Delay(TimeSpan.FromSeconds(2), false, PlayerLoopTiming.FixedUpdate, ChzzkCTS.Token, true);
                    }
                }, true, ChzzkCTS.Token).Forget();*/
            }
        }, true, destroyCancellationToken).Forget();
    }
    public void StopChzzk()
    {
        if (ChzzkUnity.instance != null)
        {
            if (ChzzkUnity.instance.socket != null && ChzzkUnity.instance.socket.IsAlive)
            {
                ChzzkUnity.instance.socket.Close();
                ChzzkUnity.instance.socket = null;
            }
        }
        ChzzkCTS?.Cancel();
        ChzzkCTS?.Dispose();
    }

    public CancellationTokenSource youtubeCTS;
    public void StartYoutube()
    {
        youtubeCTS?.Cancel();
        youtubeCTS?.Dispose();
        if (YoutubeUnity.instance != null)
        {
            youtubeCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);
            UniTask.RunOnThreadPool(() => YoutubeUnity.instance.Connect(youtubeCTS.Token), true).Forget();
        }
    }
    public void StopYoutube()
    {
        youtubeCTS?.Cancel();
        youtubeCTS?.Dispose();
    }

    public async void OnApplicationQuit()
    {
        ChzzkCTS?.Cancel();
        youtubeCTS?.Cancel();
        await UniTask.Yield();
        ChzzkCTS?.Dispose();
        youtubeCTS?.Dispose();
    }
}
