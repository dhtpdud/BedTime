using Cysharp.Threading.Tasks;
using OSY;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public GameManagerInfoSystem gameManagerSystem;
    public Camera mainCam;
    public int targetFPS = 60;
    public string nameSpliter = "!:";

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


    public Transform spawnTransform;
    [Serializable]
    public class SteveConfig
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
    public SteveConfig peepoConfig;
    public DonationConfig donationConfig;
    public void SetDefalutLifeTime(string val)
    {
        peepoConfig.defalutLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetAddLifeTime(string val)
    {
        peepoConfig.addLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetMaxLifeTime(string val)
    {
        peepoConfig.maxLifeTime = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetDefaultSize(string val)
    {
        peepoConfig.defaultSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetMinSize(string val)
    {
        peepoConfig.minSize = float.Parse(val);
        gameManagerSystem.UpdateSetting();
    }
    public void SetMaxSize(string val)
    {
        peepoConfig.maxSize = float.Parse(val);
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

    public Dictionary<int, Texture2D> thumbnailsCacheDic = new Dictionary<int, Texture2D>();

    [Header("UI")]
    public Canvas rootCanvas;
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

    /*[Header("GameObject Caches")]
    public GameObject chatBubbles;
    public GameObject chatBubble;
    public GameObject nameTag;*/

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
        public string userName;
        public int subscribeMonth;
        /*public GameObject chatBubbleObjects;
        public List<ChatInfo> chatInfos;
        public GameObject nameTagObject;*/
        public ViewerInfo(string nickName, int subscribeMonth = 0)
        {
            this.userName = nickName;
            /*chatInfos = new List<ChatInfo>();
            this.nameTagObject = Instantiate(instance.nameTag, instance.nameTagUICanvasTransform);
            this.chatBubbleObjects = Instantiate(instance.chatBubbles, instance.chatBubbleUICanvasTransform);
            var tmp = nameTagObject.GetComponentInChildren<TMP_Text>();*/

            if (nickName.Contains(GameManager.instance.nameSpliter))
                nickName = nickName.Split(GameManager.instance.nameSpliter)[1];

            /*tmp.text = subscribeMonth > 0 ? $"{nickName}\n[{subscribeMonth}개월]" : nickName;
            //Debug.Log(nicknameColor.ToHexString());
            tmp.color = subscribeMonth > 0 ? new Color(1, 0.5f, 0) : Color.white;*/
        }
        /*public void OnDestroy()
        {
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.SwitchToMainThread();
                chatInfos.Clear();
                Destroy(nameTagObject);
                await Utils.YieldCaches.UniTaskYield;
                GameManager.instance.viewerInfos.Remove(Animator.StringToHash(nickName));
            }, true, GameManager.instance.destroyCancellationToken).Forget();
        }*/
    }
    //캐싱 변수
    //public Dictionary<int, ViewerInfo> viewerInfos = new Dictionary<int, ViewerInfo>();
    public List<ViewerInfo> viewerInfos = new List<ViewerInfo>();

    /*public struct SpawnOrder
    {
        public int hash;
        public float3 initForce;

        public SpawnOrder(int hash, float3 initForce)
        {
            this.hash = hash;
            this.initForce = initForce;
        }
    }
    public Queue<SpawnOrder> spawnOrderQueue = new Queue<SpawnOrder>();*/

    public Vector2 onMouseDownPosition;
    public Vector2 onMouseDragPosition;
    public GameObject dragingObject;

    protected void Awake()
    {
        instance = this;
        var tokenInit = destroyCancellationToken;
        mainCam ??= Camera.main;
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
    public void OnDestroy()
    {
        ES3AutoSaveMgr.managers.Clear();
    }
}
