using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

/// <summary>
/// TikTok LIVE チャット連携マネージャー。
/// Node.jsプロキシサーバー (tiktok-proxy/server.js) からWebSocket経由でイベントを受信する。
/// </summary>
public class TikTokChatManager : MonoBehaviour
{
    [Header("Connection")]
    public string tiktokUsername = "";
    public string proxyHost = "localhost";
    public int proxyPort = 21213;

    [Header("Status")]
    public bool isConnected = false;
    public bool isConnecting = false;
    public int totalCommands = 0;
    public string lastError = "";

    // ─── Events ──────────────────────────────────────────────
    public event Action<string, string> OnChatMessage;              // (viewerName, message)
    public event Action<string, string, int, int, string> OnGift;   // (viewerId, viewerName, tier, coinAmount, displayText)
    public event Action<string, string> OnNewTeamMember;            // (viewerId, viewerName)
    public event Action<string, string, int> OnTeamMemberDetected;  // (viewerId, viewerName, teamLevel)
    public event Action<string, string> OnNewSubscriber;            // (viewerId, viewerName)
    public event Action<string, string> OnSubscriberDetected;       // (viewerId, viewerName)
    public event Action<int, int> OnLikeMilestone;                  // (milestoneIndex, likeCount)
    public event Action OnStreamConnected;
    public event Action<string, string> OnFollow;                   // (viewerId, viewerName)
    public event Action<string, string> OnShare;                    // (viewerId, viewerName)

    // ─── Private ─────────────────────────────────────────────
    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private Queue<string> messageQueue = new Queue<string>();
    private readonly object queueLock = new object();
    private Process proxyProcess;

    private Dictionary<string, float> viewerCooldowns = new Dictionary<string, float>();
    private int baseLikeCount = 0;
    private int currentLikeCount = 0;
    private int currentLikeMilestoneIndex = -1;
    private HashSet<string> detectedTeamMembers = new HashSet<string>();
    private HashSet<string> detectedSubscribers = new HashSet<string>();

    // ─── Gift Batching（連続ギフトをまとめて処理）───────────
    private class PendingGift
    {
        public string viewerId;
        public string viewerName;
        public int totalDiamonds;
        public string lastGiftName;
        public float lastGiftTime;
    }
    private Dictionary<string, PendingGift> pendingGifts = new Dictionary<string, PendingGift>();
    private const float GiftBatchWindow = 2f; // 秒: この間に追加ギフトが無ければ確定

    // ─── Auto-Reconnect ────────────────────────────────────
    private bool shouldBeConnected = false; // ユーザーが接続を要求した状態か
    private float reconnectTimer = 0f;
    private const float ReconnectDelay = 5f;
    private int reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 50; // 大幅に増加（長時間配信対応）
    private float connectingTimeout = 0f; // isConnecting固着防止タイマー

    // ─── Lifecycle ───────────────────────────────────────────

    void Update()
    {
        // Process messages from WebSocket thread on main thread
        ProcessMessageQueue();

        // 蓄積ギフトの確定処理
        FlushPendingGifts();

        // Cooldown cleanup
        CleanupCooldowns();

        // isConnecting固着防止（30秒でタイムアウト）
        if (isConnecting)
        {
            connectingTimeout += Time.deltaTime;
            if (connectingTimeout > 30f)
            {
                Debug.LogWarning("[TikTokChat] isConnecting stuck, resetting...");
                isConnecting = false;
                connectingTimeout = 0f;
            }
        }
        else
        {
            connectingTimeout = 0f;
        }

        // 自動再接続
        if (shouldBeConnected && !isConnected && !isConnecting && reconnectAttempts < MaxReconnectAttempts)
        {
            reconnectTimer -= Time.deltaTime;
            if (reconnectTimer <= 0f)
            {
                reconnectAttempts++;
                reconnectTimer = Mathf.Min(ReconnectDelay * reconnectAttempts, 30f); // 最大30秒間隔
                Debug.Log($"[TikTokChat] Auto-reconnect attempt {reconnectAttempts}/{MaxReconnectAttempts}...");
                StartCoroutine(ReconnectCoroutine());
            }
        }
    }

    void OnDestroy()
    {
        Disconnect();
    }

    // ─── Connection ──────────────────────────────────────────

    public void ConnectToStream(string username)
    {
        tiktokUsername = username.Trim().TrimStart('@');
        if (string.IsNullOrEmpty(tiktokUsername))
        {
            lastError = "TikTokユーザーネームをにゅうりょくしてください";
            return;
        }

        lastError = "";
        shouldBeConnected = true;
        reconnectAttempts = 0;
        reconnectTimer = 0f;
        StartCoroutine(ConnectCoroutine());
    }

    IEnumerator ConnectCoroutine()
    {
        isConnecting = true;
        isConnected = false;

        Disconnect(); // Close existing connection

        // プロキシexeを自動起動
        if (!LaunchProxy())
        {
            isConnecting = false;
            yield break;
        }

        // プロキシ起動待ち
        yield return new WaitForSeconds(2f);

        cts = new CancellationTokenSource();
        ws = new ClientWebSocket();

        string url = $"ws://{proxyHost}:{proxyPort}";
        Debug.Log($"[TikTokChat] Connecting to proxy: {url}");

        // 接続リトライ（プロキシ起動に時間がかかる場合）
        bool connected = false;
        for (int retry = 0; retry < 5; retry++)
        {
            if (ws.State != WebSocketState.None)
            {
                try { ws.Dispose(); } catch { }
                ws = new ClientWebSocket();
            }

            var connectTask = ws.ConnectAsync(new Uri(url), cts.Token);
            while (!connectTask.IsCompleted)
                yield return null;

            if (!connectTask.IsFaulted && ws.State == WebSocketState.Open)
            {
                connected = true;
                break;
            }

            Debug.Log($"[TikTokChat] Retry {retry + 1}/5...");
            yield return new WaitForSeconds(1f);
        }

        if (!connected)
        {
            lastError = "プロキシにせつぞくできません";
            isConnecting = false;
            Debug.LogWarning("[TikTokChat] Connection failed after retries");
            KillProxy();
            yield break;
        }

        Debug.Log("[TikTokChat] Connected to proxy server");

        // Start receive loop on background thread
        _ = ReceiveLoop(cts.Token);

        // ユーザー名をプロキシに送信してTikTok接続開始
        string cmd = $"{{\"command\":\"connect\",\"username\":\"{tiktokUsername}\"}}";
        var sendTask = ws.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd)),
            WebSocketMessageType.Text, true, cts.Token);
        while (!sendTask.IsCompleted)
            yield return null;

        Debug.Log($"[TikTokChat] Sent connect command for @{tiktokUsername}");

        isConnected = true;
        isConnecting = false;

        OnStreamConnected?.Invoke();
    }

    public void Disconnect()
    {
        shouldBeConnected = false;
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
        if (ws != null)
        {
            try { ws.Dispose(); } catch { }
            ws = null;
        }
        isConnected = false;
        isConnecting = false;
        KillProxy();
    }

    IEnumerator ReconnectCoroutine()
    {
        isConnecting = true;

        // WebSocketだけ再接続（プロキシはまだ起動中の可能性あり）
        if (cts != null) { cts.Cancel(); cts.Dispose(); }
        if (ws != null) { try { ws.Dispose(); } catch { } }

        cts = new CancellationTokenSource();
        ws = new ClientWebSocket();

        string url = $"ws://{proxyHost}:{proxyPort}";
        bool connected = false;
        for (int retry = 0; retry < 3; retry++)
        {
            if (ws.State != WebSocketState.None)
            {
                try { ws.Dispose(); } catch { }
                ws = new ClientWebSocket();
            }

            var connectTask = ws.ConnectAsync(new Uri(url), cts.Token);
            while (!connectTask.IsCompleted)
                yield return null;

            if (!connectTask.IsFaulted && ws.State == WebSocketState.Open)
            {
                connected = true;
                break;
            }
            yield return new WaitForSeconds(1f);
        }

        if (!connected)
        {
            // プロキシが死んでいるかも → 再起動して再試行
            Debug.Log("[TikTokChat] Proxy may have died, restarting...");
            if (!LaunchProxy())
            {
                isConnecting = false;
                yield break;
            }
            yield return new WaitForSeconds(2f);

            for (int retry = 0; retry < 3; retry++)
            {
                if (ws.State != WebSocketState.None)
                {
                    try { ws.Dispose(); } catch { }
                    ws = new ClientWebSocket();
                }
                var ct2 = ws.ConnectAsync(new Uri(url), cts.Token);
                while (!ct2.IsCompleted) yield return null;
                if (!ct2.IsFaulted && ws.State == WebSocketState.Open) { connected = true; break; }
                yield return new WaitForSeconds(1f);
            }
        }

        if (!connected)
        {
            lastError = "さいせつぞくしっぱい";
            isConnecting = false;
            yield break;
        }

        // Receive loop start
        _ = ReceiveLoop(cts.Token);

        // Send connect command
        string cmd = $"{{\"command\":\"connect\",\"username\":\"{tiktokUsername}\"}}";
        var sendTask = ws.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd)),
            WebSocketMessageType.Text, true, cts.Token);
        while (!sendTask.IsCompleted) yield return null;

        isConnected = true;
        isConnecting = false;
        reconnectAttempts = 0;
        lastError = "";
        Debug.Log($"[TikTokChat] Reconnected to @{tiktokUsername}");
    }

    // ─── Proxy Process Management ─────────────────────────────

    string GetProxyExePath()
    {
#if UNITY_EDITOR
        // Editor: tiktok-proxy/dist/tiktok-proxy.exe (プロジェクトルートからの相対パス)
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        string editorPath = System.IO.Path.Combine(projectRoot, "..", "tiktok-proxy", "dist", "tiktok-proxy.exe");
        if (System.IO.File.Exists(editorPath)) return System.IO.Path.GetFullPath(editorPath);
        // フォールバック: node実行
        return null;
#else
        // Build: ゲームexeと同じフォルダ
        string gameDir = System.IO.Path.GetDirectoryName(Application.dataPath);
        string exePath = System.IO.Path.Combine(gameDir, "tiktok-proxy.exe");
        if (System.IO.File.Exists(exePath)) return exePath;
        return null;
#endif
    }

    bool LaunchProxy()
    {
        KillProxy(); // 既存プロセスを終了

        string exePath = GetProxyExePath();

#if UNITY_EDITOR
        // Editor: exeがなければnode直接実行を試みる
        if (exePath == null)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            string serverJs = System.IO.Path.Combine(projectRoot, "..", "tiktok-proxy", "server.js");
            if (!System.IO.File.Exists(serverJs))
            {
                lastError = "tiktok-proxy/server.js がみつかりません";
                Debug.LogError($"[TikTokChat] server.js not found: {serverJs}");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = $"\"{System.IO.Path.GetFullPath(serverJs)}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                proxyProcess = Process.Start(psi);
                Debug.Log($"[TikTokChat] Proxy started via node (PID: {proxyProcess.Id})");
                return true;
            }
            catch (Exception ex)
            {
                lastError = "Node.js がみつかりません。npm install してください";
                Debug.LogError($"[TikTokChat] Failed to start node: {ex.Message}");
                return false;
            }
        }
#endif

        if (exePath == null)
        {
            lastError = "tiktok-proxy.exe がみつかりません";
            Debug.LogError("[TikTokChat] Proxy exe not found");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            proxyProcess = Process.Start(psi);
            Debug.Log($"[TikTokChat] Proxy started (PID: {proxyProcess.Id})");
            return true;
        }
        catch (Exception ex)
        {
            lastError = $"プロキシのきどうにしっぱい: {ex.Message}";
            Debug.LogError($"[TikTokChat] Failed to start proxy: {ex.Message}");
            return false;
        }
    }

    void KillProxy()
    {
        if (proxyProcess != null)
        {
            try
            {
                if (!proxyProcess.HasExited)
                {
                    proxyProcess.Kill();
                    Debug.Log($"[TikTokChat] Proxy process killed (PID: {proxyProcess.Id})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TikTokChat] Failed to kill proxy: {ex.Message}");
            }
            proxyProcess.Dispose();
            proxyProcess = null;
        }
    }

    // ─── WebSocket Receive ───────────────────────────────────

    async Task ReceiveLoop(CancellationToken token)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        try
        {
            while (ws != null && ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    string msg = sb.ToString();
                    sb.Clear();
                    lock (queueLock)
                    {
                        messageQueue.Enqueue(msg);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TikTokChat] Receive error: {ex.Message}");
        }

        // Mark disconnected on main thread via queue
        lock (queueLock)
        {
            messageQueue.Enqueue("{\"event\":\"_disconnected\"}");
        }
    }

    void ProcessMessageQueue()
    {
        const int maxPerFrame = 20;
        const int maxQueueSize = 200; // これ以上溜まったら古いものを捨てる
        int processed = 0;
        lock (queueLock)
        {
            // キュー溢れ防止: 古いメッセージを破棄
            if (messageQueue.Count > maxQueueSize)
            {
                int drop = messageQueue.Count - maxQueueSize;
                for (int i = 0; i < drop; i++) messageQueue.Dequeue();
                Debug.LogWarning($"[TikTokChat] Queue overflow: dropped {drop} old messages");
            }

            while (messageQueue.Count > 0 && processed < maxPerFrame)
            {
                string msg = messageQueue.Dequeue();
                try { ProcessMessage(msg); }
                catch (Exception ex) { Debug.LogWarning($"[TikTokChat] Parse error: {ex.Message}"); }
                processed++;
            }
        }
    }

    // ─── Message Processing ──────────────────────────────────

    void ProcessMessage(string json)
    {
        // "event" はC#予約語なので手動抽出してからJsonUtility
        string eventType = ExtractEventType(json);
        if (string.IsNullOrEmpty(eventType)) return;

        // "event" → "eventType" に置換してJsonUtilityで読み込み
        string fixedJson = json.Replace("\"event\":", "\"eventType\":");
        var msg = JsonUtility.FromJson<TikTokMessage>(fixedJson);
        if (msg == null) return;

        // JsonUtilityがprofilePictureUrlを取りこぼす場合のフォールバック
        if (msg.user != null && string.IsNullOrEmpty(msg.user.profilePictureUrl))
        {
            string extracted = ExtractJsonString(json, "profilePictureUrl");
            if (!string.IsNullOrEmpty(extracted))
                msg.user.profilePictureUrl = extracted;
        }

        switch (eventType)
        {
            case "connected":
                Debug.Log($"[TikTokChat] TikTok LIVE connected: roomId={msg.roomId}");
                isConnected = true;
                reconnectAttempts = 0;
                break;

            case "chat":
                if (!isConnected) { isConnected = true; reconnectAttempts = 0; }
                ProcessChat(msg);
                break;

            case "gift":
                if (!isConnected) { isConnected = true; reconnectAttempts = 0; }
                ProcessGift(msg);
                break;

            case "like":
                ProcessLike(msg);
                break;

            case "subscribe":
                ProcessSubscribe(msg);
                break;

            case "follow":
                ProcessFollow(msg);
                break;

            case "share":
                ProcessShare(msg);
                break;

            case "member":
                DetectTeamFromBadges(msg);
                break;

            case "streamEnd":
                Debug.Log("[TikTokChat] Stream ended");
                isConnected = false;
                lastError = "配信が終了しました";
                break;

            case "_disconnected":
                isConnected = false;
                lastError = "プロキシとの接続が切断されました";
                Debug.LogWarning("[TikTokChat] Disconnected from proxy, will auto-reconnect");
                reconnectAttempts = 0; // リセットして再接続を許可
                reconnectTimer = ReconnectDelay;
                break;

            case "error":
                lastError = msg.message ?? "Unknown error";
                Debug.LogWarning($"[TikTokChat] Error: {lastError}");
                // エラーで接続が切れた場合は再接続を許可
                if (!isConnected && shouldBeConnected)
                {
                    reconnectAttempts = 0;
                    reconnectTimer = ReconnectDelay;
                }
                break;
        }
    }

    void ProcessChat(TikTokMessage msg)
    {
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);
        string comment = msg.comment ?? "";

        // チーム/サブスクバッジ検出
        DetectTeamFromBadges(msg);
        DetectSubscriberFromBadges(msg);

        // プロフィール画像保存＋即時ダウンロード開始
        string picUrl = msg.user != null ? msg.user.profilePictureUrl : null;
        if (!string.IsNullOrEmpty(picUrl))
        {
            Debug.Log($"[TikTokChat] Profile URL for {viewerName}: {picUrl}");
            ViewerStats.Instance?.SetProfileImage(viewerId, viewerName, picUrl);
            if (UIManager.Instance != null)
                UIManager.Instance.RequestProfileImage(viewerName, picUrl);
        }
        else
        {
            Debug.Log($"[TikTokChat] No profile URL for {viewerName} (user={msg.user != null}, picUrl='{picUrl}')");
        }

        OnChatMessage?.Invoke(viewerName, comment);

        // スピーチバブル
        GameManager.Instance?.ShowViewerSpeech(viewerName, comment);

        // コマンド処理: スタンス変更（クールダウンなし）
        if (GameManager.Instance != null && GameManager.Instance.TryCommandFromChat(comment, viewerName, viewerId))
        {
            totalCommands++;
            return;
        }

        // コマンド処理: ユニット召喚（クールダウンあり）
        if (GameManager.Instance != null)
        {
            // クールダウンチェック
            if (viewerCooldowns.ContainsKey(viewerId) && viewerCooldowns[viewerId] > Time.time)
                return;

            if (GameManager.Instance.TryAddUnitFromChat(comment, viewerName, viewerId))
            {
                totalCommands++;
                float cooldown = GetCooldownForViewer(viewerId);
                viewerCooldowns[viewerId] = Time.time + cooldown;
            }
        }
    }

    void ProcessGift(TikTokMessage msg)
    {
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);
        int totalDiamonds = msg.totalDiamondCount > 0 ? msg.totalDiamondCount
            : (msg.diamondCount * Mathf.Max(msg.repeatCount, 1));
        string giftName = msg.giftName ?? "Gift";

        // プロフィール画像保存＋即時ダウンロード開始
        if (msg.user != null && !string.IsNullOrEmpty(msg.user.profilePictureUrl))
        {
            ViewerStats.Instance?.SetProfileImage(viewerId, viewerName, msg.user.profilePictureUrl);
            if (UIManager.Instance != null)
                UIManager.Instance.RequestProfileImage(viewerName, msg.user.profilePictureUrl);
        }

        // バッジ検出
        DetectTeamFromBadges(msg);
        DetectSubscriberFromBadges(msg);

        // バッチ蓄積（連続ギフトをまとめて処理）
        if (!pendingGifts.ContainsKey(viewerId))
            pendingGifts[viewerId] = new PendingGift { viewerId = viewerId, viewerName = viewerName };

        var pg = pendingGifts[viewerId];
        pg.totalDiamonds += totalDiamonds;
        pg.lastGiftTime = Time.time;
        pg.lastGiftName = giftName;
        pg.viewerName = viewerName;

        Debug.Log($"[TikTokChat] Gift queued: {viewerName} +{totalDiamonds} (pending: {pg.totalDiamonds} diamonds)");
    }

    /// <summary>蓄積ギフトを確定してイベント発火（GiftBatchWindow秒間追加なし→確定）</summary>
    void FlushPendingGifts()
    {
        if (pendingGifts.Count == 0) return;

        var toFlush = new List<string>();
        foreach (var kv in pendingGifts)
        {
            if (Time.time - kv.Value.lastGiftTime >= GiftBatchWindow)
                toFlush.Add(kv.Key);
        }

        foreach (var key in toFlush)
        {
            var pg = pendingGifts[key];

            // ティア判定（累計コイン数ベース）
            int tier = 0;
            for (int i = GameConfig.TikTokGiftTierMinCoins.Length - 1; i >= 0; i--)
            {
                if (pg.totalDiamonds >= GameConfig.TikTokGiftTierMinCoins[i])
                {
                    tier = i;
                    break;
                }
            }

            string displayText = $"{pg.lastGiftName} ({pg.totalDiamonds} coins)";
            OnGift?.Invoke(pg.viewerId, pg.viewerName, tier, pg.totalDiamonds, displayText);
            Debug.Log($"[TikTokChat] Gift flushed: {pg.viewerName} → {displayText} (Tier {tier}: {GameConfig.TikTokGiftTierNames[tier]})");

            pendingGifts.Remove(key);
        }
    }

    void ProcessLike(TikTokMessage msg)
    {
        int totalLikes = msg.totalLikeCount;
        if (baseLikeCount == 0)
            baseLikeCount = totalLikes - msg.likeCount;
        currentLikeCount = totalLikes;

        int diff = totalLikes - baseLikeCount;

        // マイルストーン判定
        for (int i = GameConfig.TikTokLikeMilestones.Length - 1; i >= 0; i--)
        {
            if (diff >= GameConfig.TikTokLikeMilestones[i] && i > currentLikeMilestoneIndex)
            {
                currentLikeMilestoneIndex = i;
                OnLikeMilestone?.Invoke(i, diff);
                Debug.Log($"[TikTokChat] Like milestone! +{diff} ({GameConfig.TikTokLikeMilestoneNames[i]})");
                break;
            }
        }
    }

    void ProcessSubscribe(TikTokMessage msg)
    {
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);

        if (!detectedSubscribers.Contains(viewerId))
        {
            detectedSubscribers.Add(viewerId);
            OnNewSubscriber?.Invoke(viewerId, viewerName);
            Debug.Log($"[TikTokChat] New subscriber: {viewerName}");
        }
    }

    void ProcessFollow(TikTokMessage msg)
    {
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);
        OnFollow?.Invoke(viewerId, viewerName);
    }

    void ProcessShare(TikTokMessage msg)
    {
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);
        OnShare?.Invoke(viewerId, viewerName);
    }

    // ─── Badge Detection ─────────────────────────────────────

    void DetectTeamFromBadges(TikTokMessage msg)
    {
        if (msg.user == null || msg.user.badges == null) return;
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);

        foreach (var badge in msg.user.badges)
        {
            if (badge != null && badge.type == "fan_club")
            {
                int level = Mathf.Max(badge.level, 1);
                if (!detectedTeamMembers.Contains(viewerId))
                {
                    detectedTeamMembers.Add(viewerId);
                    OnNewTeamMember?.Invoke(viewerId, viewerName);
                }
                OnTeamMemberDetected?.Invoke(viewerId, viewerName, level);
                return;
            }
        }
    }

    void DetectSubscriberFromBadges(TikTokMessage msg)
    {
        if (msg.user == null || msg.user.badges == null) return;
        string viewerName = GetDisplayName(msg);
        string viewerId = GetUserId(msg);

        foreach (var badge in msg.user.badges)
        {
            if (badge != null && badge.type == "subscriber")
            {
                if (!detectedSubscribers.Contains(viewerId))
                {
                    detectedSubscribers.Add(viewerId);
                    OnSubscriberDetected?.Invoke(viewerId, viewerName);
                }
                return;
            }
        }
    }

    // ─── Cooldown ────────────────────────────────────────────

    float GetCooldownForViewer(string viewerId)
    {
        float baseCd = GameConfig.ViewerCooldown;

        if (ViewerStats.Instance != null)
        {
            // サブスクメンバー: 最短クールダウン
            if (ViewerStats.Instance.IsSubscriber(viewerId))
                return GameConfig.SubscriptionCooldown;

            // チームメンバー: レベル別クールダウン
            int teamLevel = ViewerStats.Instance.GetTeamLevel(viewerId);
            if (teamLevel > 0)
            {
                int levelTier = ViewerStats.GetTeamLevelTier(teamLevel);
                return GameConfig.TeamCooldownSec[levelTier];
            }
        }

        return baseCd;
    }

    void CleanupCooldowns()
    {
        var expired = new List<string>();
        foreach (var kv in viewerCooldowns)
            if (kv.Value <= Time.time) expired.Add(kv.Key);
        foreach (var k in expired)
            viewerCooldowns.Remove(k);
    }

    // ─── Helpers ─────────────────────────────────────────────

    static string ExtractEventType(string json)
    {
        // 簡易的にJSON文字列から "event":"xxx" を抽出
        int idx = json.IndexOf("\"event\"");
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return null;
        int quote1 = json.IndexOf('"', colon);
        if (quote1 < 0) return null;
        int quote2 = json.IndexOf('"', quote1 + 1);
        if (quote2 < 0) return null;
        return json.Substring(quote1 + 1, quote2 - quote1 - 1);
    }

    /// <summary>生JSONからキー名で文字列値を手動抽出（JsonUtilityフォールバック用）</summary>
    static string ExtractJsonString(string json, string key)
    {
        string search = $"\"{key}\":";
        int idx = json.IndexOf(search);
        if (idx < 0) return null;
        int valStart = idx + search.Length;
        // スペースをスキップ
        while (valStart < json.Length && json[valStart] == ' ') valStart++;
        if (valStart >= json.Length) return null;
        char c = json[valStart];
        if (c == '"')
        {
            // 文字列値: "xxx"
            int q2 = json.IndexOf('"', valStart + 1);
            if (q2 < 0) return null;
            return json.Substring(valStart + 1, q2 - valStart - 1);
        }
        if (c == '[')
        {
            // 配列の場合: 最初の文字列要素を返す ["url1","url2"]
            int q1 = json.IndexOf('"', valStart);
            if (q1 < 0) return null;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }
        return null;
    }

    string GetDisplayName(TikTokMessage msg)
    {
        if (msg.user != null)
        {
            if (!string.IsNullOrEmpty(msg.user.nickname)) return msg.user.nickname;
            if (!string.IsNullOrEmpty(msg.user.uniqueId)) return msg.user.uniqueId;
        }
        return "Anonymous";
    }

    string GetUserId(TikTokMessage msg)
    {
        if (msg.user != null && !string.IsNullOrEmpty(msg.user.userId))
            return msg.user.userId;
        return GetDisplayName(msg);
    }

    /// <summary>いいねの差分カウント（HUD表示用）</summary>
    public int GetLikeDiff()
    {
        return currentLikeCount - baseLikeCount;
    }

    /// <summary>現在のいいねマイルストーンインデックス</summary>
    public int GetCurrentLikeMilestoneIndex()
    {
        return currentLikeMilestoneIndex;
    }

    /// <summary>次のいいねマイルストーンまでの残数</summary>
    public int GetLikesUntilNextMilestone()
    {
        int diff = GetLikeDiff();
        int nextIdx = currentLikeMilestoneIndex + 1;
        if (nextIdx >= GameConfig.TikTokLikeMilestones.Length) return 0;
        return GameConfig.TikTokLikeMilestones[nextIdx] - diff;
    }
}

// ─── JSON Data Structures ────────────────────────────────

[Serializable]
public class TikTokMessage
{
    public string eventType; // JSON "event" → 置換後 "eventType"
    public string roomId;
    public string message; // error message
    public string comment; // chat message

    // Gift fields
    public int giftId;
    public string giftName;
    public int diamondCount;
    public int repeatCount;
    public int totalDiamondCount;
    public bool repeatEnd;
    public int giftType;

    // Like fields
    public int likeCount;
    public int totalLikeCount;

    // Room user
    public int viewerCount;

    // User info
    public TikTokUser user;
}

[Serializable]
public class TikTokUser
{
    public string uniqueId;
    public string userId;
    public string nickname;
    public string profilePictureUrl;
    public TikTokBadge[] badges;
}

[Serializable]
public class TikTokBadge
{
    public string type; // "fan_club", "subscriber", etc.
    public int level;
    public string name;
}
