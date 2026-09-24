using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Registry;
using UnityEngine;

// Lives on the cache manager so navigation does not stop background downloads.
public sealed class WikiImageRequestQueue : MonoBehaviour
{
    const int MaxConcurrent = 3;
    const float TimeoutSeconds = 30f;

    sealed class Request
    {
        internal string Bucket, Url, Key;
        internal long Priority;
        internal int Failures;
        internal float Started;
        internal Task<Texture2D> Download;
        internal readonly TaskCompletionSource<Texture2D> Completion = new TaskCompletionSource<Texture2D>();
    }

    readonly List<Request> requests = new List<Request>();
    RegistryManager manager;
    long priority;

    public Task<Texture2D> GetTextureAsync(RegistryManager cache, string bucket, string url)
    {
        manager = cache;
        string key = bucket + "/" + RegistryManager.GetUrlTextureUID(url);
        var texture = manager.GetItemByUID(key)?.asset as Texture2D;
        if (texture != null) return Task.FromResult(texture);
        foreach (var request in requests)
        {
            if (request.Key != key) continue;
            request.Priority = ++priority;
            return request.Completion.Task;
        }
        var added = new Request { Bucket = bucket, Url = url, Key = key, Priority = ++priority };
        requests.Add(added);
        return added.Completion.Task;
    }

    void Update()
    {
        // Settle finished downloads before considering preemption: a finished cache
        // entry must never be removed just to free a network slot.
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            var request = requests[i];
            if (request.Download == null) continue;
            Exception error = null;
            Texture2D texture = null;
            if (request.Download.IsCompleted)
            {
                try { texture = request.Download.GetAwaiter().GetResult(); }
                catch (Exception failure) { error = failure; }
                if (texture == null && error == null)
                    error = new InvalidOperationException("The response did not contain a texture.");
            }
            else if (Time.realtimeSinceStartup - request.Started >= TimeoutSeconds)
            {
                CancelDownload(request);
                error = new TimeoutException("Image download exceeded 30 seconds.");
            }
            else continue;

            request.Download = null;
            if (error != null && ++request.Failures < 2) continue;
            requests.RemoveAt(i);
            if (error != null) request.Completion.TrySetException(error);
            else request.Completion.TrySetResult(texture);
        }

        while (true)
        {
            Request next = null, oldest = null;
            int active = 0;
            foreach (var request in requests)
            {
                if (request.Download == null)
                {
                    if (next == null || request.Priority > next.Priority) next = request;
                }
                else
                {
                    active++;
                    if (oldest == null || request.Priority < oldest.Priority) oldest = request;
                }
            }
            if (next == null) return;
            if (active >= MaxConcurrent)
            {
                if (next.Priority <= oldest.Priority) return;
                // Requeue the oldest download; keep its callers waiting on the same
                // completion while the latest selection gets an immediate slot.
                CancelDownload(oldest);
                oldest.Download = null;
            }
            next.Started = Time.realtimeSinceStartup;
            try { next.Download = manager.GetTextureFromUrlAsync(next.Bucket, next.Url); }
            catch (Exception error)
            {
                requests.Remove(next);
                next.Completion.TrySetException(error);
            }
        }
    }

    void CancelDownload(Request request)
    {
        if (manager != null) manager.RemoveOverride(request.Key);
        // Observe cancellation/failure before discarding the underlying task.
        if (request.Download != null && request.Download.IsFaulted)
            _ = request.Download.Exception;
    }

    void OnDestroy()
    {
        var pending = requests.ToArray();
        requests.Clear();
        foreach (var request in pending)
        {
            if (request.Download != null && !request.Download.IsCompleted) CancelDownload(request);
            request.Completion.TrySetCanceled();
        }
    }
}
