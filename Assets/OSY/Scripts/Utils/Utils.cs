using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace OSY
{
    public class ManageableQueue<T>
    {
        public Queue<T> queue;
        public bool isLock;
        public ManageableQueue()
        {
            queue = new Queue<T>();
            isLock = false;
        }
    }
    public struct FrameInfo
    {
        public object data;
        public string path;
        public FrameInfo(object framedata, string path)
        {
            this.data = framedata;
            this.path = path;
        }
    }
    [Serializable]
    public struct RandomRange
    {
        public float minValue;
        public float maxValue;
        public RandomRange(float minValue, float maxValue)
        {
            this.minValue = minValue;
            this.maxValue = maxValue;
        }
        public float GetValue() => minValue >= maxValue ? minValue : Utils.GetRandom(minValue, maxValue);
    }
    /*public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null && !_instance.IsDestroyed())
                {
                    var instances = FindObjectsByType(typeof(T), FindObjectsSortMode.None);
                    if (instances == null || instances.Length == 0) return null;
                    _instance = (T)instances[0];
                    if (instances.Length > 0)
                        for (int i = 1; i < instances.Length; i++)
                        {
                            Destroy(instances[i]);
                        }
                }
                return _instance;
            }
        }
        protected virtual void Awake()
        {
            var tokenCreate = destroyCancellationToken;
        }
    }*/
    public static class Utils
    {
        public static Dictionary<int, string> hashMemory = new Dictionary<int, string>();
        public static readonly string stringSpace = " ";
        public static readonly string stringSwitchLine = "\n";
        public static async UniTask<Texture2D> CachingTextureTask(string url)
        {
            if (url == null || url == string.Empty || url == "\r\n")
                return null;
            int urlHash = Animator.StringToHash(url);
            if (GameManager.instance.TexturesCacheDic.ContainsKey(urlHash))
                return GameManager.instance.TexturesCacheDic[urlHash];
            string protocol = url.Substring(0, 4);
            if (!protocol.Equals("http") && !protocol.Equals("blob"))
                url = $"file:///{url}";
            //url = url.Replace("http://", "https://");
            //Debug.LogWarning(url);
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                try
                {
                    await request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        //Debug.Log(request.error);
                        return null;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                    return null;
                }
                GameManager.instance.TexturesCacheDic.TryAdd(urlHash, ((DownloadHandlerTexture)request.downloadHandler).texture);
                return ((DownloadHandlerTexture)request.downloadHandler).texture;
            }
        }
        public static async UniTask WaitUntilRecord(Stopwatch stopwatch, NativeArray<float> lastRecordTIme, string taskName, Func<bool> waitCondition, CancellationToken token, bool isDebug = false)
        {
            if (isDebug)
            {
                Debug.Log($"{taskName} 작업 시간 측정 시작");
                if (!stopwatch.IsRunning)
                    stopwatch.Start();
            }
            await WaitUntil(waitCondition, YieldCaches.UniTaskYield, token);
            if (isDebug)
            {
                stopwatch.Stop();
                lock (stopwatch)
                    Debug.Log($"{taskName} 작업 소비 시간: " + (lastRecordTIme[0] = stopwatch.ElapsedMilliseconds - lastRecordTIme[0]) / 1000f);
            }
        }
        public static async UniTask<bool> WaitUntil(TimeSpan timeout, Func<bool> func, YieldAwaitable yield, bool ignoreTimeScale = false, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            bool isTimeout = false;
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.Delay(timeout, ignoreTimeScale);
                isTimeout = true;
            }).Forget();
            while (!func.Invoke() && !token.IsCancellationRequested && !isTimeout)
            {
                await yield;
            }
            return !isTimeout;
        }
        public static async UniTask WaitWhile(TimeSpan timeout, Func<bool> func, YieldAwaitable yield, bool ignoreTimeScale = false, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            bool isTimeout = false;
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.Delay(timeout, ignoreTimeScale);
                isTimeout = true;
            }).Forget();
            while (func.Invoke() && !token.IsCancellationRequested && !isTimeout)
            {
                await yield;
            }
        }
        public static async UniTask WaitUntil(Func<bool> func, YieldAwaitable yield, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            while (!token.IsCancellationRequested && !func.Invoke())
            {
                await yield;
            }

        }
        public static async UniTask WaitWhile(Func<bool> func, YieldAwaitable yield, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            while (func.Invoke() && !token.IsCancellationRequested)
            {
                await yield;
            }
        }
        public static async UniTask WaitUntil(Func<bool> func, Func<UniTask> waitDelay, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            while (!func.Invoke() && !token.IsCancellationRequested)
            {
                await waitDelay();
            }
        }
        public static async UniTask WaitWhile(Func<bool> func, UniTask waitDelay, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            while (func.Invoke() && !token.IsCancellationRequested)
            {
                await waitDelay;
            }
        }
        public static void KeepAlive(params object[] items) => GC.KeepAlive(items);

        public static float GetRandom(float minimum, float maximum)
        {
            System.Random random = new System.Random();
            return (float)(random.NextDouble() * (maximum - minimum) + minimum);
        }
        public static Vector2 GetRandomPosition_Vector2(RectTransform rectTransform)
        {
            return rectTransform.anchoredPosition + new Vector2(GetRandom(-rectTransform.rect.width / 2, rectTransform.rect.width / 2), GetRandom(-rectTransform.rect.height / 2, rectTransform.rect.height / 2));
        }
        public static float2 GetRandomPosition_Float2(RectTransform rectTransform)
        {
            return (float2)rectTransform.anchoredPosition + new float2(GetRandom(-rectTransform.rect.width / 2, rectTransform.rect.width / 2), GetRandom(-rectTransform.rect.height / 2, rectTransform.rect.height / 2));
        }
        public static int GetRandom(int minimum, int maximum)
        {
            System.Random random = new System.Random();
            return random.Next(minimum, maximum);
        }
        public class YieldCaches
        {
            private static WaitForEndOfFrame waitForEndOfFrame;
            public static WaitForEndOfFrame WaitForEndOfFrame
            {
                get
                {
                    if (waitForEndOfFrame == null)
                        return waitForEndOfFrame = new WaitForEndOfFrame();
                    return waitForEndOfFrame;
                }
            }
            private static WaitForSeconds waitFor1sec;
            public static WaitForSeconds WaitFor1sec
            {
                get
                {
                    if (waitFor1sec == null)
                        return waitFor1sec = new WaitForSeconds(1);
                    return waitFor1sec;
                }
            }
            private static WaitForSecondsRealtime waitFor1secReal;
            public static WaitForSecondsRealtime WaitFor1secReal
            {
                get
                {
                    if (waitFor1secReal == null)
                        return waitFor1secReal = new WaitForSecondsRealtime(1);
                    return waitFor1secReal;
                }
            }
            private static WaitForSecondsRealtime waitFor100millisecReal;
            public static WaitForSecondsRealtime WaitFor100millisecReal
            {
                get
                {
                    if (waitFor100millisecReal == null)
                        return waitFor100millisecReal = new WaitForSecondsRealtime(0.1f);
                    return waitFor100millisecReal;
                }
            }
            public static YieldAwaitable UniTaskYield = UniTask.Yield();
        }

        public static async UniTask<AudioClip> GetAudioFile(string url, AudioType audioType)
        {
            if (url.Equals(null) || url.Equals(string.Empty)) return null;
            if (!url.Substring(0, 4).Equals("http"))
                url = $"file://{url}";
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                await request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.LogError(request.error);
                    return null;
                }
                var clip = DownloadHandlerAudioClip.GetContent(request);
                await WaitUntil(() => clip.loadState == AudioDataLoadState.Loaded, YieldCaches.UniTaskYield);
                return clip;
            }
            /*using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Download the WAV file
                    byte[] wavData = await client.GetByteArrayAsync(url);

                    return OpenWavParser.ByteArrayToAudioClip(wavData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error downloading and saving WAV file: {ex.Message}");
                    return null;
                }
            }*/
            // 결과: Unable to open DLL! Dynamic linking is not supported in WebAssembly builds due to limitations to performance and code size. Please statically link in the needed libraries.

            /*using (WebClient client = new WebClient())
            {
                try
                {
                    // Download the WAV file
                    var wavData = await client.DownloadDataTaskAsync(url);
                    return OpenWavParser.ByteArrayToAudioClip(wavData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading and saving WAV file: {ex.Message}");
                    return null;
                }
            }*/
            //결과: DownloadDataTaskAsync에서 멈춤

            /*WebRequest request = WebRequest.Create(url);
            using (WebResponse response = request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Download the WAV file into a memory stream
                responseStream.CopyTo(memoryStream);

                // Convert memory stream to byte array
                byte[] wavData = memoryStream.ToArray();
                return OpenWavParser.ByteArrayToAudioClip(wavData);
            }*/
            //결과: 또 멈춤

            /*using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                await request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.LogError(request.error);
                    return null;
                }
                return OpenWavParser.ByteArrayToAudioClip(request.downloadHandler.data);
            }*/
            // 결과: Uncaught TypeError: Failed to execute 'copyToChannel' on 'AudioBuffer': The provided Float32Array value must not be shared.
        }
        public static string ConvertToUtf8(string value)
        {
            // Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(value);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

            // Return UTF8 bytes as ANSI string
            return Encoding.Default.GetString(utf8Bytes);
        }

        public static string EncryptString(string plainText, DateTime key)
        {
            using (Aes aesAlg = Aes.Create())
            {
                Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(key.ToString("yyyy-MM-dd HH:mm:ss"), Encoding.UTF8.GetBytes("SaltValue"));
                aesAlg.Key = keyDerivation.GetBytes(aesAlg.KeySize / 8);
                aesAlg.IV = new byte[16]; // Initialization Vector (IV) - You may want to generate a random IV for added security

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }

                    byte[] encryptedBytes = msEncrypt.ToArray();
                    string encryptedText = Convert.ToBase64String(encryptedBytes);

                    // Ensure the encrypted text is at least 10 characters long
                    while (encryptedText.Length < 10)
                    {
                        encryptedText += "0"; // You can choose a different padding character if needed
                    }

                    return encryptedText;
                }
            }
        }
        public static async UniTask DelayCall(float sec, Action action, bool ignoreTimeScale, CancellationToken token = default)
        {
            token = token == default ? GameManager.instance.destroyCancellationToken : token;
            await UniTask.WaitForSeconds(sec, ignoreTimeScale, PlayerLoopTiming.Update, token);
            action.Invoke();
        }
        public static Vector3 Vector3FromAngle(float degrees, float magnitude)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(magnitude * Mathf.Cos(radians), magnitude * Mathf.Sin(radians), 0);
        }
        public static Unity.Physics.Material GetMaterial(RigidBody rigidBody, ColliderKey colliderKey)
        {
            Unity.Physics.Material material;
            unsafe
            {
                Unity.Physics.Collider* colliderPointer = (Unity.Physics.Collider*)rigidBody.Collider.GetUnsafePtr();
                ChildCollider childCollider;
                colliderPointer->GetLeaf(colliderKey, out childCollider);
                ConvexCollider* childColliderPointer = (ConvexCollider*)childCollider.Collider;
                material = childColliderPointer->Material;
            }
            return material;
        }
        public static void SetMaterial(RigidBody rigidBody, Unity.Physics.Material material, ColliderKey colliderKey)
        {
            unsafe
            {
                Unity.Physics.Collider* colliderPointer = (Unity.Physics.Collider*)rigidBody.Collider.GetUnsafePtr();
                ChildCollider childCollider;
                colliderPointer->GetLeaf(colliderKey, out childCollider);
                ConvexCollider* childColliderPointer = (ConvexCollider*)childCollider.Collider;
                childColliderPointer->Material = material;
            }
        }
        public static void ToFloat3(this float2 target, ref float3 convert)
        {
            convert.x = target.x;
            convert.y = target.y;
        }
        public static void ToFloat2(this float3 target, ref float2 convert)
        {
            convert.x = target.x;
            convert.y = target.y;
        }
        public static float3 ToFloat3(this float2 target)
        {
            return math.float3(target.x, target.y, 0);
        }
        public static float2 ToFloat2(this float3 target)
        {
            return math.float2(target.x, target.y);
        }
        public static float3 ToFloat3(this Vector2 target)
        {
            return math.float3(target.x, target.y, 0);
        }
        public static float3 ToFloat3(this Vector3 target)
        {
            return math.float3(target.x, target.y, target.z);
        }
        public static float2 ToFloat2(this Vector3 target)
        {
            return math.float2(target.x, target.y);
        }

        public static unsafe float3* ToFloat3Unsafe(this float2 target)
        {
            float3 result = math.float3(target.x, target.y, 0);
            return &result;
        }
        public static unsafe float2* ToFloat2Unsafe(this float3 target)
        {
            float2 result = math.float2(target.x, target.y);
            return &result;
        }
        public static unsafe float3* ToFloat3Unsafe(this Vector2 target)
        {
            float3 result = math.float3(target.x, target.y, 0);
            return &result;
        }
        public static unsafe float2* ToFloat2Unsafe(this Vector3 target)
        {
            float2 result = math.float2(target.x, target.y);
            return &result;
        }

        public static string GetRandomHexNumber(int digits) // string 기준으로 최대 길이 (1byte = string 두글자)
        {
            System.Random random = new System.Random();
            byte[] buffer = new byte[digits / 2];
            random.NextBytes(buffer);
            string result = String.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
            if (digits % 2 == 0)
                return result;
            return result + random.Next(16).ToString("X");
        }

        public static async UniTask<JObject> GetJObject(string url, CancellationToken token, bool isKeepTry = true)
        {
            if (!url.Substring(0, 4).Equals("http"))
                url = $"file://{url}";

            UnityWebRequest request = UnityWebRequest.Get(url);
            for(float timer = 0; timer < 10; timer ++)
            {
                try
                {
                    await request.SendWebRequest();
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    if (!isKeepTry)
                        return null;
                    await UniTask.Delay(TimeSpan.FromSeconds(1f), false, PlayerLoopTiming.Update, token);
                    request = UnityWebRequest.Get(url);
                }
            }
            var result = request.downloadHandler.text;
            //Debug.Log(result);
            return new JObject(JObject.Parse(result));
        }
        public static async UniTask<string> GetJsonString(string url, CancellationToken token, bool isKeepTry = true)
        {
            if (!url.Substring(0, 4).Equals("http"))
                url = $"file://{url}";

            UnityWebRequest request = UnityWebRequest.Get(url);
            for (float timer = 0; timer < 10; timer ++)
            {
                try
                {
                    await request.SendWebRequest();
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    if (!isKeepTry)
                        return null;
                    await UniTask.Delay(TimeSpan.FromSeconds(1f), true, PlayerLoopTiming.Update, token);
                    request = UnityWebRequest.Get(url);
                }
            }
            var result = request.downloadHandler.text;
            //Debug.Log(result);
            return result;
        }
        public static unsafe float ToFloat(this FixedString64Bytes fixedString)
        {
            // FixedString의 Raw Data 접근
            var utf8Bytes = fixedString.GetUnsafePtr();
            int length = fixedString.Length;

            float result = 0f;
            bool isNegative = false;
            bool isFractional = false;
            float fractionalDivisor = 1f;

            for (int i = 0; i < length; i++)
            {
                byte b = utf8Bytes[i];

                if (b == '-') // 음수 처리
                {
                    isNegative = true;
                }
                else if (b == '.') // 소수점 처리
                {
                    isFractional = true;
                }
                else if (b >= '0' && b <= '9') // 숫자 처리
                {
                    if (isFractional)
                    {
                        fractionalDivisor *= 10f;
                        result += (b - '0') / fractionalDivisor;
                    }
                    else
                    {
                        result = result * 10f + (b - '0');
                    }
                }
                else
                {
                    Debug.LogError($"Invalid character in FixedString: {b}");
                    return 0f; // 잘못된 값일 경우 0 반환
                }
            }

            return isNegative ? -result : result;
        }
    }
}