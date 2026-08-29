using System;
using System.Collections;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Runs coroutines on a dedicated, persistent GameObject that MurkysManySaves owns and
    /// controls the lifecycle of itself, rather than borrowing whatever MonoBehaviour happens to
    /// be found in the scene - which might be disabled, mid-destruction during a scene
    /// transition, or simply not exist yet. Internal plumbing, not part of the public API.
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner instance;

        private static CoroutineRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var host = new GameObject("MurkysManySavesRunner");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    instance = host.AddComponent<CoroutineRunner>();
                }
                return instance;
            }
        }

        internal static void RunDelayed(float delaySeconds, Action action)
        {
            Instance.StartCoroutine(DelayedCoroutine(delaySeconds, action));
        }

        private static IEnumerator DelayedCoroutine(float delaySeconds, Action action)
        {
            yield return new WaitForSeconds(delaySeconds);
            action();
        }
    }
}
