using Cysharp.Threading.Tasks;
using DG.Tweening;
using OSY;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int MaxDiaCount;
    public int diaCount;
    public int MaxChatCount;
    public int chatCount;
    public int MaxPlayerCount;
    public int playerCount;
    public int MaxParticleCount;
    public int particleCount;
    public GameManagerInfoSystem gameManagerSystem;
    public Camera mainCam;
    [HideInInspector]
    public Transform mainCamTrans;
    public int targetFPS = 60;

    public const string stringNameSpliter = "!:";
    public const string stringKingEmoji = "\U0001F451";
    public const string stringZero = "0";
    public const string stringProfileImage = "ProfileImage";
    public const string stringDecimal2 = "0.00";

    //캐싱용 변수
    public float deltaTime { get; private set; }
    public float captureDeltaTime { get; private set; }
    public float unscaledDeltaTime { get; private set; }
    public float targetFrameRate { get; private set; }
    public float timeScale { get; private set; }
    public float realTimeScale { get; private set; }

    public int originVSyncCount { get; private set; }
    public int originTargetFramerate { get; private set; }
    public int origincaptureFramerate { get; private set; }

    public float gravity;
    public void SetGravity(string val)
    {
        gravity = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public int SpawnMinDonationAmount;
    public int SpawnMinSubscriptionMonth;
    public float2 SpawnMinSpeed;
    public float2 SpawnMaxSpeed;
    public void SetSpawnMinDonationAmount(string val)
    {
        SpawnMinDonationAmount = int.Parse(val);
    }
    public void SetSpawnMinSubscriptionMonth(string val)
    {
        SpawnMinSubscriptionMonth = int.Parse(val);
    }
    public void SetSpawnMinXSpeed(string val)
    {
        SpawnMinSpeed.x = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetSpawnMinYSpeed(string val)
    {
        SpawnMinSpeed.y = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetSpawnMaxXSpeed(string val)
    {
        SpawnMaxSpeed.x = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetSpawnMaxYSpeed(string val)
    {
        SpawnMaxSpeed.y = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public float dragPower;
    public float stabilityPower;
    public float physicMaxVelocity;

    [Serializable]
    public class DonationConfig
    {
        public float objectCountFactor;
        public float objectLifeTime;
        public float minSize;
        public float maxSize;
    }


    public Transform playerSpawnTransform;
    public Transform screenSpawnTransform;
    [Serializable]
    public class PlayerConfig
    {
        public float defalutLifeTime;
        public float addLifeTime;
        public float maxLifeTime;
        public float defaultSize;
        public float minSize;
        public float maxSize;

        public float switchTimeImpact;
        public float switchIdleAnimationTime;

        public float moveSpeedMin;
        public float moveSpeedMax;
        public float movingTimeMin;
        public float movingTimeMax;
        public float IdlingTimeMin;
        public float IdlingTimeMax;
    }
    public PlayerConfig playerConfig;
    public DonationConfig donationConfig;
    public void SetDefalutLifeTime(string val)
    {
        playerConfig.defalutLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetAddLifeTime(string val)
    {
        playerConfig.addLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetMaxLifeTime(string val)
    {
        playerConfig.maxLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetDefaultSize(string val)
    {
        playerConfig.defaultSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetMinSize(string val)
    {
        playerConfig.minSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetMaxSize(string val)
    {
        playerConfig.maxSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetDonationObjectCountFactor(string val)
    {
        donationConfig.objectCountFactor = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetDonationObjectLifeTime(string val)
    {
        donationConfig.objectLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetDonationObjectMinSize(string val)
    {
        donationConfig.minSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetDonationObjectMaxSize(string val)
    {
        donationConfig.maxSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetChatBubbleSize(string val)
    {
        chatBubbleSize = float.Parse(val);
    }
    public float chatBubbleSize = 1;

    public Dictionary<int, Texture2D> TexturesCacheDic = new Dictionary<int, Texture2D>();

    [Header("UI")]
    public Canvas rootCanvas;
    public GameObject chatPrefab;
    public Transform chatLineTrans;
    public TMP_Text playerCountTMP;
    public Transform leaderBodardTrans;
    public GameObject playerBoardPrefab;
    public Transform nameTagUICanvasTransform;
    public Transform chatBubbleUICanvasTransform;
    //public Transform unknownDonationParentsTransform;

    public GameObject settingUI;
    //public GameObject channelInfoUI;
    //public TMP_Text channelViewerCount;
    //public TMP_Text peepoCountText;
    public GameObject ErrorPOPUP;
    public TMP_InputField ErrorPOPUPText;
    //public RectTransform peepoSpawnRect;

    [Header("GameObject Caches")]
    /*public GameObject chatBubbles;
    public GameObject chatBubble;*/
    public GameObject nameTag;

    public Dictionary<int, BlobAssetReference<Unity.Physics.Collider>> blobAssetcolliders = new Dictionary<int, BlobAssetReference<Unity.Physics.Collider>>();

    /*public class ChatInfo
    {
        public string id;
        public GameObject bubbleObject;
        public DateTime dateTime;
        public string text;
        public ChatInfo(string id, string text, float lifeTImeSec, Transform bubbleObjectParent, bool isTop = false)
        {
            this.id = id;
            dateTime = DateTime.Now;
            this.text = text;
            bubbleObject = Instantiate(instance.chatBubble, bubbleObjectParent);
            if (isTop)
                bubbleObject.transform.SetAsFirstSibling();
            //var bubbleCTD = bubbleObject.GetCancellationTokenOnDestroy();
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.SwitchToMainThread();
                var tmp = bubbleObject.GetComponentInChildren<TMP_Text>();
                tmp.text = text;
                await bubbleObject.transform.GetChild(0).DoScaleAsync(Vector3.zero, Vector3.one, 0.5f, Utils.YieldCaches.UniTaskYield);
                var parentOBJ = bubbleObjectParent.gameObject;
                parentOBJ.SetActive(false);
                parentOBJ.SetActive(true);
                await UniTask.Delay(TimeSpan.FromSeconds(lifeTImeSec));

                await tmp.DoColorAsync(new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0), 1, Utils.YieldCaches.UniTaskYield);
                DestroyImmediate(bubbleObject);
            }, true, GameManager.instance.destroyCancellationToken).Forget();
        }
    }*/
    public class ViewerInfo
    {
        public string name;
        public string score;

        public int subscribeMonth;
        /*public GameObject chatBubbleObjects;
        public List<ChatInfo> chatInfos;*/

        public Image profileImage;
        public Texture2D profileTexture;

        public GameObject nameTagObject;
        public TMP_Text nameTagTMP;
        public Image nameTagBackground;

        public GameObject playerBoard;
        public Transform playerBoardTrans;
        public TMP_Text playerBoardTMP;

        public bool isEnable;

        public CancellationTokenSource UpdateCTS;
        public ViewerInfo(string displayName, int subscribeMonth = 0)
        {
            this.name = displayName;
            this.subscribeMonth = subscribeMonth;
            this.score = stringZero;

            UpdatePlayerBoard();
            UpdateNameTag().Forget();
        }
        public void UpdatePlayerBoard()
        {
            if (playerBoard == null)
            {
                Debug.Log("리더보드 객체 없음");
                playerBoard = Instantiate(instance.playerBoardPrefab, instance.leaderBodardTrans);
                playerBoardTrans = playerBoard.transform;
                playerBoardTMP = playerBoard.GetComponentInChildren<TMP_Text>();
                profileImage = playerBoardTrans.GetChild(0).GetChild(0).GetComponent<Image>();
            }

            if (profileTexture != null)
            {
                profileImage.sprite = Sprite.Create(profileTexture, new Rect(0, 0, profileTexture.width, profileTexture.height), new Vector2(0.5f, 0.5f));
            }
            var rank = playerBoardTrans.GetSiblingIndex() + 1;
            this.playerBoardTMP.text = $"{rank}. {(rank == 1 ? stringKingEmoji : string.Empty)}{name}\nScore:{this.score}";
        }
        public async UniTask UpdateNameTag()
        {
            if (UpdateCTS != null && !UpdateCTS.IsCancellationRequested)
            {
                UpdateCTS?.Cancel();
                //UpdateCTS?.Dispose();
                await UniTask.Yield();
            }
            if (nameTagObject == null)
            {
                this.nameTagObject = Instantiate(instance.nameTag, instance.nameTagUICanvasTransform);
                //chatInfos = new List<ChatInfo>();
                //this.chatBubbleObjects = Instantiate(instance.chatBubbles, instance.chatBubbleUICanvasTransform);
                nameTagTMP = nameTagObject.GetComponentInChildren<TMP_Text>(true);
                nameTagBackground = nameTagObject.GetComponentInParent<Image>(true);
                nameTagTMP.text = subscribeMonth > 0 ? $"{name}\n[{subscribeMonth}Month]" : name;
                //Debug.Log(nicknameColor.ToHexString());
            }
            UpdateCTS = CancellationTokenSource.CreateLinkedTokenSource(nameTagTMP.destroyCancellationToken);
            UniTask.RunOnThreadPool(UpdateNameTagTask, true, UpdateCTS.Token).Forget();
        }
        private async UniTask UpdateNameTagTask()
        {
            await UniTask.SwitchToMainThread();
            isEnable = true;
            nameTagBackground.color = new Color(0, 0, 0, 0.6f);
            nameTagTMP.color = subscribeMonth > 0 ? new Color(1, 0.5f, 0, 1) : Color.white;
            await UniTask.Delay(TimeSpan.FromSeconds(3), true, PlayerLoopTiming.Update, UpdateCTS.Token, true);
            nameTagTMP.DoColorAsync(Color.clear, 3, Utils.YieldCaches.UniTaskYield, UpdateCTS.Token).Forget();
            nameTagBackground.DoColorAsync(Color.clear, 3, Utils.YieldCaches.UniTaskYield, UpdateCTS.Token).OnCompleted(() =>
            {
                isEnable = false;
            }).Forget();

        }
        /*public void OnDestroy(FixedString128Bytes name)
        {
            UniTask.RunOnThreadPool(async () =>
            {
                //GameManager.instance.AddChat($"{name} left the game");
                await UniTask.SwitchToMainThread();
                //chatInfos.Clear();
                Destroy(nameTagObject);
                await Utils.YieldCaches.UniTaskYield;
                GameManager.instance.viewerInfos.Remove(name);
            }, true, GameManager.instance.destroyCancellationToken).Forget();
        }*/
    }
    //캐싱 변수
    public Dictionary<FixedString128Bytes, ViewerInfo> viewerInfos = new Dictionary<FixedString128Bytes, ViewerInfo>();

    public Vector2 onMouseDownPosition;
    public Vector2 onMouseDragPosition;
    public GameObject dragingObject;

    protected void Awake()
    {
        instance = this;
        var tokenInit = destroyCancellationToken;
        mainCam ??= Camera.main;
        mainCamTrans = mainCam.transform;
        originTargetFramerate = Application.targetFrameRate;
        origincaptureFramerate = Time.captureFramerate;
        originVSyncCount = QualitySettings.vSyncCount;
        ES3AutoSaveMgr.Current.Load();
    }
    public async void Start()
    {
        gameManagerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<GameManagerInfoSystem>();
        await Utils.WaitUntil(() => gameManagerSystem.isReady, Utils.YieldCaches.UniTaskYield, destroyCancellationToken);
        QualitySettings.maxQueuedFrames = 4;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
        Profiler.maxUsedMemory = 2000000000;//2GB
        gameManagerSystem.UpdateSetting();
        UpdateLeaderBoard();
        /*UniTask.RunOnThreadPool(async () =>
        {
            float timer = 0;
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (GameManager.instance.diaCount < 10) continue;
                    timer += Time.deltaTime;
                    if (timer > 0.01f)
                    {
                        timer = 0;
                        lock (this)
                        {
                            GameManager.instance.diaCount -= 10;
                            if (GameManager.instance.diaCount == 0)
                                GameManager.instance.diaCount = 0;
                        }
                    }
                }
                finally
                {
                    await UniTask.Yield();
                }
            }
        }, true, destroyCancellationToken).Forget();*/
    }
    public void Update()
    {
        deltaTime = Time.deltaTime;
        targetFrameRate = Application.targetFrameRate;
        captureDeltaTime = Time.captureDeltaTime;
        timeScale = Time.timeScale;
        unscaledDeltaTime = Time.unscaledDeltaTime;
        realTimeScale = deltaTime / unscaledDeltaTime;
        if (mainCam != null)
        {
            if (Input.GetMouseButtonDown(0))
                onMouseDownPosition = mainCam.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButton(0))
                onMouseDragPosition = mainCam.ScreenToWorldPoint(Input.mousePosition);
        }
    }
    public void UpdatePlayerCount()
    {
        GameManager.instance.playerCountTMP.text = $"{GameManager.instance.playerCount} Players";
    }
    public void UpdateLeaderBoard()
    {
        int index = 0;
        foreach (var viewerInfo in viewerInfos.Values.OrderByDescending(viewerInfo => float.Parse(viewerInfo.score)))
        {
            viewerInfo.UpdatePlayerBoard();
            viewerInfo.playerBoardTrans.SetSiblingIndex(index++);
        }
    }
    public void AddChat(FixedString128Bytes text)
    {
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.SwitchToMainThread();
            chatCount++;
            if (chatCount > MaxChatCount)
            {
                GameObject.DestroyImmediate(GameManager.instance.chatLineTrans.GetChild(0));
                chatCount = MaxChatCount;
            }
            var chatObject = Instantiate(GameManager.instance.chatPrefab, GameManager.instance.chatLineTrans);
            chatObject.GetComponentInChildren<TMP_Text>().text = text.ToString().Replace(Utils.stringSwitchLine, Utils.stringSpace);
            await UniTask.Delay(TimeSpan.FromSeconds(5));
            chatObject.GetComponent<CanvasGroup>().DOFade(0, 3).OnComplete(() =>
            {
                chatCount--;
                GameObject.DestroyImmediate(chatObject);
            });
        }).Forget();
    }
    public void OnDestroy()
    {
        ES3AutoSaveMgr.managers.Clear();
    }
}
