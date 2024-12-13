using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class DotweenUnitask
{
    #region  ★Ease 관련 코드
    //https://easings.net/ko
    public enum EaseType
    {
        Linear,
        easeInSine, easeOutSine, easeInOutSine,
        easeInQuad, easeOutQuad, easeInOutQuad,
        easeInCubic, easeOutCubic, easeInOutCubic,
        easeInQuart, easeOutQuart, easeInOutQuart,
        easeInQuint, easeOutQuint, easeInOutQuint,
        easeInExpo, easeOutExpo, easeInOutExpo,
        easeInCirc, easeOutCirc, easeInOutCirc,
        easeInBack, easeOutBack, easeInOutBack,
        easeInElastic, easeOutElastic, easeInOutElastic,
        easeInBounce, easeOutBounce, easeInOutBounce,
    }
    const float c1 = 1.70158f;
    const float c2 = c1 * 1.525f;
    const float c3 = c1 + 1f;
    const float c4 = (2f * Mathf.PI) / 3f;
    const float c5 = (2f * Mathf.PI) / 4.5f;
    /// <summary>
    /// x(진척도)를 대입하면, easeType의 움직임에서 해당 진척도에서 해당하는 값을 반환합니다.
    /// </summary>
    /// <param name="x"> 0 (움직임의 시작)에서 1 (움직임의 끝) 사이의 움직임 진척도 </param>
    public static float Easing(float x, EaseType easeType)
    {
        switch (easeType)
        {
            case EaseType.easeInSine: return 1 - Mathf.Cos((x * Mathf.PI) / 2);
            case EaseType.easeOutSine: return Mathf.Sin((x * Mathf.PI) / 2);
            case EaseType.easeInOutSine: return -(Mathf.Cos(Mathf.PI * x) - 1) / 2;

            case EaseType.easeInQuad: return x * x;
            case EaseType.easeOutQuad: return 1 - (1 - x) * (1 - x);
            case EaseType.easeInOutQuad: return x < 0.5 ? 2 * x * x : 1 - Mathf.Pow(-2 * x + 2, 2) / 2;

            case EaseType.easeInCubic: return x * x * x;
            case EaseType.easeOutCubic: return 1 - Mathf.Pow(1 - x, 3);
            case EaseType.easeInOutCubic: return x < 0.5 ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;

            case EaseType.easeInQuart: return x * x * x * x;
            case EaseType.easeOutQuart: return 1 - Mathf.Pow(1 - x, 4);
            case EaseType.easeInOutQuart: return x < 0.5 ? 8 * x * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 4) / 2;

            case EaseType.easeInQuint: return x * x * x * x * x;
            case EaseType.easeOutQuint: return 1 - Mathf.Pow(1 - x, 5);
            case EaseType.easeInOutQuint: return x < 0.5 ? 16 * x * x * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 5) / 2;

            case EaseType.easeInExpo: return x == 0 ? 0 : Mathf.Pow(2, 10 * x - 10);
            case EaseType.easeOutExpo: return x == 1 ? 1 : 1 - Mathf.Pow(2, -10 * x);
            case EaseType.easeInOutExpo:
                return x == 0 ? 0
                    : x == 1 ? 1
                    : x < 0.5 ? Mathf.Pow(2, 20 * x - 10) / 2
                    : (2 - Mathf.Pow(2, -20 * x + 10)) / 2;

            case EaseType.easeInCirc: return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
            case EaseType.easeOutCirc: return Mathf.Sqrt(1 - Mathf.Pow(x - 1, 2));
            case EaseType.easeInOutCirc:
                return x < 0.5 ? (1 - Mathf.Sqrt(1 - Mathf.Pow(2 * x, 2))) / 2
                    : (Mathf.Sqrt(1 - Mathf.Pow(-2 * x + 2, 2)) + 1) / 2;

            case EaseType.easeInBack: return c3 * x * x * x - c1 * x * x;
            case EaseType.easeOutBack: return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
            case EaseType.easeInOutBack:
                return x < 0.5 ? (Mathf.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2)) / 2
                    : (Mathf.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2;

            case EaseType.easeInElastic:
                return x == 0 ? 0
                    : x == 1 ? 1
                    : -Mathf.Pow(2, 10 * x - 10) * Mathf.Sin((x * 10 - 10.75f) * c4);
            case EaseType.easeOutElastic:
                return x == 0 ? 0
                    : x == 1 ? 1
                    : Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10 - 0.75f) * c4) + 1;
            case EaseType.easeInOutElastic:
                return x == 0 ? 0
                    : x == 1 ? 1
                    : x < 0.5 ? -(Mathf.Pow(2, 20 * x - 10) * Mathf.Sin((20f * x - 11.125f) * c5)) / 2
                    : (Mathf.Pow(2, -20 * x + 10) * Mathf.Sin((20f * x - 11.125f) * c5)) / 2 + 1;

            case EaseType.easeInBounce: return 1 - EaseOutBounce(1 - x);
            case EaseType.easeOutBounce: return EaseOutBounce(x);
            case EaseType.easeInOutBounce:
                return x < 0.5 ? (1 - EaseOutBounce(1 - 2 * x)) / 2
                    : (1 + EaseOutBounce(2 * x - 1)) / 2;
        }
        return x;
    }
    static float EaseOutBounce(float x)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (x < 1 / d1)
            return n1 * x * x;
        else if (x < 2 / d1)
            return n1 * (x -= 1.5f / d1) * x + 0.75f;
        else if (x < 2.5 / d1)
            return n1 * (x -= 2.25f / d1) * x + 0.9375f;
        else
            return n1 * (x -= 2.625f / d1) * x + 0.984375f;
    }
    #endregion

    public static async UniTask OnCompleted(this UniTask task, Action action)
    {
        await task;
        action.Invoke();
    }
    public static async UniTask OnComplete<T>(this UniTask<T> task, Action<T> action)
    {
        var result = await task;
        action.Invoke(result);
    }
    public static async UniTask DoColorAsync(this Graphic target, Color byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.color = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        var applyValue = target.color;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 4; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.color = applyValue;
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        for (int i = 0; i < 4; i++)
            applyValue[i] = byValue[i];
        target.color = applyValue;
    }
    public static async UniTask DoMoveAsync(this RectTransform target, Vector2 byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.anchoredPosition = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        Vector2 applyValue = target.anchoredPosition;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 2; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.anchoredPosition = applyValue;
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        for (int i = 0; i < 2; i++)
            applyValue[i] = byValue[i];
        target.anchoredPosition = applyValue;
    }
    public static async UniTask DoMoveAsync(this Transform target, Vector3 byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear, bool completeOnCancel = false)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.position = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        Vector3 applyValue = target.position;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 3; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.position = applyValue;
            await yieldAwaitable;
        }
        if (!completeOnCancel && token.IsCancellationRequested) return;
        for (int i = 0; i < 3; i++)
            applyValue[i] = byValue[i];
        target.position = applyValue;
    }
    public static async UniTask DoScaleAsync(this Transform target, Vector3 byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.localScale = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        Vector3 applyValue = target.localScale;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 3; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.localScale = applyValue;
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        for (int i = 0; i < 3; i++)
            applyValue[i] = byValue[i];
        target.localScale = applyValue;
    }
    public static async UniTask DoScaleAsync(this Transform target, Vector3 from, Vector3 byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        target.localScale = from;
        if (second == 0)
        {
            target.localScale = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        Vector3 applyValue = target.localScale;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 3; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.localScale = applyValue;
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        for (int i = 0; i < 3; i++)
            applyValue[i] = byValue[i];
        target.localScale = applyValue;
    }
    public static async UniTask DoSizeAsync(this RectTransform target, Vector2 byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.sizeDelta = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        Vector3 applyValue = target.sizeDelta;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 2; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.sizeDelta = applyValue;
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        for (int i = 0; i < 2; i++)
            applyValue[i] = byValue[i];
        target.sizeDelta = applyValue;
    }
    public static async UniTask DoRoateAsync(this Transform target, Vector3 byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.Rotate(byValue, Space.World);
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        Vector3 applyValue = target.eulerAngles;
        var originVal = applyValue;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                for (int i = 0; i < 3; i++)
                {
                    applyValue[i] = Mathf.LerpUnclamped(originVal[i], byValue[i], EasingVal);
                }
            target.Rotate(applyValue, Space.World);
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        for (int i = 0; i < 3; i++)
            applyValue[i] = byValue[i];
        target.Rotate(applyValue, Space.World);
    }
    public static async UniTask DoFadeAsync(this CanvasGroup target, float byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target.alpha = byValue;
            return;
        }
        var originVal = target.alpha;
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                target.alpha = Mathf.LerpUnclamped(originVal, byValue, EasingVal);
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        target.alpha = byValue;
    }
    public static async UniTask DoValueAsync(this NativeArray<float> target, float byValue, float second, YieldAwaitable yieldAwaitable, CancellationToken token = default, EaseType easeType = EaseType.Linear)
    {
        if (target == null) return;
        if (second == 0)
        {
            target[0] = byValue;
            return;
        }
        token = token == default ? GameManager.instance.destroyCancellationToken : token;
        var originVal = target[0];
        for (float time = 0; !token.IsCancellationRequested && time < second; time += GameManager.instance.deltaTime)
        {
            //await TelecastUtils.WaitUntil(() => GameManager.Instance.deltaTime != 0 && GameManager.Instance.timeScale != 0, TelecastUtils.YieldCaches.UniTaskYield);
            float progressVal = time / second;
            float EasingVal = Easing(progressVal, easeType);
            if (EasingVal > 0)
                target[0] = Mathf.LerpUnclamped(originVal, byValue, EasingVal);
            await yieldAwaitable;
        }
        if (token.IsCancellationRequested) return;
        target[0] = byValue;
    }
}
