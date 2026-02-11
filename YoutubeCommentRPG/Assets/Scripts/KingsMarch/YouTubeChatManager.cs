using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;

/// <summary>
/// YouTube Live Chat連携（OAuth 2.0認証）
///
/// セットアップ:
/// 1. Google Cloud Console → APIとサービス → 認証情報
/// 2. OAuth 2.0 クライアントID作成（デスクトップアプリ）
/// 3. クライアントIDとシークレットをInspectorに入力
/// 4. YouTube Data API v3を有効化
/// 5. ゲーム実行 → 「YouTube認証」ボタンでブラウザが開く → Googleログイン
/// </summary>
public class YouTubeChatManager : MonoBehaviour
{
    [Header("OAuth Settings")]
    [Tooltip("配信中のYouTube動画ID（URLの v= の後ろ）")]
    public string videoId = "";

    // OAuth credentials - 開発者がここに埋め込む
    // Google Cloud Console → OAuth 2.0 クライアントID（デスクトップアプリ）で取得
    private string clientId => OAuthCredentials.CLIENT_ID;
    private string clientSecret => OAuthCredentials.CLIENT_SECRET;

    [Header("Status")]
    public bool isAuthenticated = false;
    public bool isConnected = false;
    public bool isConnecting = false;
    public int totalCommands = 0;

    // OAuth tokens
    private string accessToken = "";
    private string refreshToken = "";
    private float tokenExpireTime = 0f;

    // Chat polling
    private string liveChatId = "";
    private string nextPageToken = "";
    private float pollInterval = 12f;  // デフォルト12秒（API quota節約）
    private float pollTimer = 0f;
    private bool isPolling = false;

    // UI feedback
    public string lastError = "";

    // Local HTTP server for OAuth redirect
    private HttpListener httpListener;
    private Thread listenerThread;
    private string authCode = "";
    private const int REDIRECT_PORT = 8585;
    private const string REDIRECT_URI = "http://localhost:8585/oauth/callback";

    // Viewer cooldown
    private Dictionary<string, float> viewerCooldowns = new Dictionary<string, float>();

    // Like count tracking
    private int lastLikeCount = 0;
    private int currentLikeMilestoneIndex = -1;
    private float likePollTimer = 0f;

    // Events
    public event Action<string, string> OnChatMessage;
    public event Action OnAuthenticated;
    public event Action<string, string, int, int, string> OnSuperChat;  // viewerId, viewerName, tier, jpyAmount, displayAmount
    public event Action<string, string> OnNewMember;                     // viewerId, viewerName
    public event Action<string, string> OnMemberDetected;                // viewerId, viewerName (既存メンバー初検出)
    public event Action<int, int> OnLikeMilestone;               // milestoneIndex, likeCount
    public event Action OnStreamConnected;                        // 配信接続成功

    // Token persistence keys
    private const string PREF_REFRESH_TOKEN = "YouTubeChat_RefreshToken";
    private const string PREF_VIDEO_ID = "YouTubeChat_VideoId";

    void Start()
    {
        // Try to restore saved refresh token
        refreshToken = PlayerPrefs.GetString(PREF_REFRESH_TOKEN, "");
        if (!string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(clientId))
        {
            Debug.Log("[YouTubeChat] 保存済みトークンでリフレッシュ試行...");
            StartCoroutine(RefreshAccessToken());
        }
    }

    void Update()
    {
        // Check if auth code received from browser
        if (!string.IsNullOrEmpty(authCode) && !isAuthenticated)
        {
            string code = authCode;
            authCode = "";
            StartCoroutine(ExchangeAuthCode(code));
        }

        // Token refresh
        if (isAuthenticated && Time.realtimeSinceStartup > tokenExpireTime - 60f)
        {
            if (!string.IsNullOrEmpty(refreshToken))
                StartCoroutine(RefreshAccessToken());
        }

        // Chat polling（準備中・バトル中のみ、リザルト中は停止してquota節約）
        var gm = GameManager.Instance;
        bool shouldPoll = isConnected && !isPolling
            && gm != null && gm.currentPhase != GamePhase.Result;
        if (shouldPoll)
        {
            pollTimer -= Time.deltaTime;
            if (pollTimer <= 0f)
            {
                pollTimer = pollInterval;
                StartCoroutine(FetchChatMessages());
            }
        }

        // Like count polling
        if (isConnected && !string.IsNullOrEmpty(videoId))
        {
            likePollTimer -= Time.deltaTime;
            if (likePollTimer <= 0f)
            {
                likePollTimer = GameConfig.LikePollInterval;
                StartCoroutine(FetchLikeCount());
            }
        }

        // Cleanup expired cooldowns
        var expired = new List<string>();
        foreach (var kvp in viewerCooldowns)
            if (Time.time > kvp.Value) expired.Add(kvp.Key);
        foreach (var k in expired)
            viewerCooldowns.Remove(k);
    }

    void OnDestroy()
    {
        StopHttpListener();
    }

    // ─── OAuth認証開始 ──────────────────────────────────────

    public void StartOAuth()
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            lastError = "OAuthCredentials未設定（開発者設定が必要）";
            Debug.LogError($"[YouTubeChat] {lastError}");
            return;
        }

        lastError = "";
        StartHttpListener();

        string scope = "https://www.googleapis.com/auth/youtube.readonly";
        string authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={clientId}" +
            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            "&access_type=offline" +
            "&prompt=consent";

        Application.OpenURL(authUrl);
        Debug.Log("[YouTubeChat] ブラウザでGoogleログインしてください...");
    }

    // ─── ローカルHTTPサーバー（OAuth redirect受信）────────

    void StartHttpListener()
    {
        StopHttpListener();

        try
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://localhost:{REDIRECT_PORT}/");
            httpListener.Start();

            listenerThread = new Thread(ListenForCallback);
            listenerThread.IsBackground = true;
            listenerThread.Start();
            Debug.Log($"[YouTubeChat] リダイレクトサーバー起動: port {REDIRECT_PORT}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[YouTubeChat] HTTPリスナー起動失敗: {e.Message}");
        }
    }

    void StopHttpListener()
    {
        if (httpListener != null)
        {
            try { httpListener.Stop(); } catch { }
            httpListener = null;
        }
        listenerThread = null;
    }

    void ListenForCallback()
    {
        try
        {
            while (httpListener != null && httpListener.IsListening)
            {
                var context = httpListener.GetContext();
                var query = context.Request.QueryString;
                string code = query["code"];
                string error = query["error"];

                string html;
                if (!string.IsNullOrEmpty(code))
                {
                    authCode = code;
                    html = "<html><body style='font-family:sans-serif;text-align:center;padding:50px;'>" +
                        "<h1>認証成功！</h1><p>このタブを閉じてゲームに戻ってください。</p></body></html>";
                }
                else
                {
                    html = "<html><body style='font-family:sans-serif;text-align:center;padding:50px;'>" +
                        $"<h1>認証失敗</h1><p>{error}</p></body></html>";
                }

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.Close();

                if (!string.IsNullOrEmpty(code)) break;
            }
        }
        catch (Exception) { }
        finally
        {
            StopHttpListener();
        }
    }

    // ─── トークン取得 ───────────────────────────────────────

    IEnumerator ExchangeAuthCode(string code)
    {
        Debug.Log("[YouTubeChat] 認証コードをトークンに交換中...");

        var form = new WWWForm();
        form.AddField("code", code);
        form.AddField("client_id", clientId);
        form.AddField("client_secret", clientSecret);
        form.AddField("redirect_uri", REDIRECT_URI);
        form.AddField("grant_type", "authorization_code");

        using (var req = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[YouTubeChat] トークン取得失敗: {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            var token = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
            accessToken = token.access_token;
            refreshToken = token.refresh_token;
            tokenExpireTime = Time.realtimeSinceStartup + token.expires_in;

            // Save refresh token
            if (!string.IsNullOrEmpty(refreshToken))
                PlayerPrefs.SetString(PREF_REFRESH_TOKEN, refreshToken);
            PlayerPrefs.Save();

            isAuthenticated = true;
            OnAuthenticated?.Invoke();
            Debug.Log("[YouTubeChat] OAuth認証成功！");
            ShowUI("<color=#00FF88>YouTube認証 成功!</color>", 3f, 56);

            // Fetch live chat ID
            if (!string.IsNullOrEmpty(videoId))
                StartCoroutine(FetchLiveChatId());
        }
    }

    IEnumerator RefreshAccessToken()
    {
        var form = new WWWForm();
        form.AddField("client_id", clientId);
        form.AddField("client_secret", clientSecret);
        form.AddField("refresh_token", refreshToken);
        form.AddField("grant_type", "refresh_token");

        using (var req = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[YouTubeChat] トークンリフレッシュ失敗: {req.error}");
                isAuthenticated = false;
                yield break;
            }

            var token = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
            accessToken = token.access_token;
            tokenExpireTime = Time.realtimeSinceStartup + token.expires_in;

            if (!string.IsNullOrEmpty(token.refresh_token))
            {
                refreshToken = token.refresh_token;
                PlayerPrefs.SetString(PREF_REFRESH_TOKEN, refreshToken);
                PlayerPrefs.Save();
            }

            isAuthenticated = true;
            Debug.Log("[YouTubeChat] トークンリフレッシュ成功");

            if (!isConnected && !string.IsNullOrEmpty(videoId))
                StartCoroutine(FetchLiveChatId());
        }
    }

    // ─── Live Chat ID取得 ───────────────────────────────────

    public void ConnectToStream(string newVideoId)
    {
        videoId = newVideoId;
        PlayerPrefs.SetString(PREF_VIDEO_ID, videoId);
        isConnected = false;
        nextPageToken = "";

        if (!isAuthenticated)
        {
            lastError = "先にYouTube認証を行ってください";
            return;
        }

        lastError = "";
        isConnecting = true;
        StartCoroutine(FetchLiveChatId());
    }

    IEnumerator FetchLiveChatId()
    {
        string url = $"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={videoId}";

        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string body = req.downloadHandler.text ?? "";
                string reason = "";
                // YouTube APIエラーレスポンスから理由を抽出
                if (body.Contains("quotaExceeded")) reason = "APIクォータ超過（明日リセット）";
                else if (body.Contains("forbidden")) reason = "API権限不足\nGoogle Cloud ConsoleでYouTube Data API v3を有効化してください";
                else if (body.Contains("unauthorized") || body.Contains("invalid_grant")) reason = "認証切れ\nYouTube再認証してください";
                else reason = req.error;

                lastError = $"動画情報取得失敗: {reason}";
                Debug.LogError($"[YouTubeChat] {lastError}\n{body}");
                ShowUI($"<color=#FF4444>接続失敗</color>\n<size=24>{reason}</size>", 6f, 44);
                isConnecting = false;
                yield break;
            }

            var response = JsonUtility.FromJson<VideoListResponse>(req.downloadHandler.text);
            if (response.items == null || response.items.Length == 0)
            {
                lastError = "動画が見つかりません\nVideo IDが正しいか確認してください";
                Debug.LogError($"[YouTubeChat] {lastError}");
                ShowUI("<color=#FF4444>接続失敗</color>\n<size=28>動画が見つかりません\nVideo IDを確認してください</size>", 4f, 48);
                isConnecting = false;
                yield break;
            }

            liveChatId = response.items[0].liveStreamingDetails.activeLiveChatId;
            if (string.IsNullOrEmpty(liveChatId))
            {
                lastError = "まだ配信が開始されていません";
                Debug.LogError($"[YouTubeChat] {lastError}");
                ShowUI("<color=#FFAA00>まだ配信が開始されていません</color>\n<size=28>YouTube Studioで「ライブ配信を開始」を\n押してから再度接続してください</size>", 5f, 44);
                isConnecting = false;
                yield break;
            }

            isConnecting = false;
            isConnected = true;
            pollTimer = 0f;
            Debug.Log($"[YouTubeChat] チャット接続成功！ LiveChatId={liveChatId}");
            ShowUI("<color=#00FF88>配信に接続しました!</color>", 3f, 56);
            OnStreamConnected?.Invoke();
        }
    }

    // ─── チャットメッセージ取得 ─────────────────────────────

    IEnumerator FetchChatMessages()
    {
        isPolling = true;

        string url = $"https://www.googleapis.com/youtube/v3/liveChat/messages" +
            $"?liveChatId={liveChatId}&part=snippet,authorDetails";
        if (!string.IsNullOrEmpty(nextPageToken))
            url += $"&pageToken={nextPageToken}";

        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[YouTubeChat] メッセージ取得失敗: {req.error}");
                isPolling = false;
                yield break;
            }

            var response = JsonUtility.FromJson<ChatMessageListResponse>(req.downloadHandler.text);
            nextPageToken = response.nextPageToken;

            if (response.pollingIntervalMillis > 0)
                pollInterval = response.pollingIntervalMillis / 1000f;

            if (response.items != null)
            {
                foreach (var item in response.items)
                {
                    string viewerName = item.authorDetails.displayName;
                    if (viewerName.StartsWith("@")) viewerName = viewerName.Substring(1);
                    string viewerId = !string.IsNullOrEmpty(item.authorDetails.channelId)
                        ? item.authorDetails.channelId : viewerName;
                    string message = item.snippet.displayMessage ?? "";
                    string msgType = item.snippet.type ?? "textMessageEvent";

                    // プロフィール画像URLを保存＋即時ダウンロード開始
                    if (!string.IsNullOrEmpty(item.authorDetails.profileImageUrl))
                    {
                        if (ViewerStats.Instance != null)
                            ViewerStats.Instance.SetProfileImage(viewerId, viewerName, item.authorDetails.profileImageUrl);
                        if (UIManager.Instance != null)
                            UIManager.Instance.RequestProfileImage(viewerName, item.authorDetails.profileImageUrl);
                    }

                    // メンバーシップ検出（isChatSponsor = true の人）
                    if (item.authorDetails.isChatSponsor)
                        DetectMember(viewerId, viewerName);

                    // メッセージタイプ別処理
                    switch (msgType)
                    {
                        case "superChatEvent":
                        case "superStickerEvent":
                            ProcessSuperChat(viewerId, viewerName, item.snippet.superChatDetails);
                            break;
                        case "newSponsorEvent":
                            ProcessNewSponsor(viewerId, viewerName);
                            break;
                        case "memberMilestoneChatEvent":
                            DetectMember(viewerId, viewerName);
                            break;
                    }

                    // 通常メッセージ処理（全タイプ共通）
                    ProcessChatMessage(viewerId, viewerName, message);
                }
            }
        }

        isPolling = false;
    }

    // ─── メッセージ処理 ──────────────────────────────────────

    void ProcessChatMessage(string viewerId, string viewerName, string message)
    {
        OnChatMessage?.Invoke(viewerName, message);

        var gm = GameManager.Instance;
        if (gm == null) return;

        // 吹き出し表示（全コメント）
        gm.ShowViewerSpeech(viewerName, message);

        // スタンス変更コマンド（クールダウンなし）
        if (gm.TryCommandFromChat(message, viewerName, viewerId))
        {
            totalCommands++;
            return;
        }

        // 召喚コマンド（クールダウンあり、メンバーは短縮）
        if (viewerCooldowns.ContainsKey(viewerId))
            return;

        if (gm.TryAddUnitFromChat(message, viewerName, viewerId))
        {
            totalCommands++;
            float cooldown = GameConfig.ViewerCooldown;
            // メンバーはクールダウン短縮
            if (detectedMembers.Contains(viewerId))
                cooldown *= GameConfig.MemberCooldownMult;
            viewerCooldowns[viewerId] = Time.time + cooldown;
        }
    }

    // ─── Super Chat / Membership / Like処理 ─────────────────

    private HashSet<string> detectedMembers = new HashSet<string>();

    void ProcessSuperChat(string viewerId, string viewerName, SuperChatDetails details)
    {
        if (details == null) return;

        int jpyAmount = ConvertToJPY(details.amountMicros, details.currency);
        int tier = GetGameTier(jpyAmount);
        string displayAmount = details.amountDisplayString ?? $"¥{jpyAmount}";

        Debug.Log($"[YouTubeChat] スーパーチャット! {viewerName}: {displayAmount} (tier={tier})");
        OnSuperChat?.Invoke(viewerId, viewerName, tier, jpyAmount, displayAmount);
    }

    void ProcessNewSponsor(string viewerId, string viewerName)
    {
        Debug.Log($"[YouTubeChat] 新規メンバー! {viewerName}");
        if (!detectedMembers.Contains(viewerId))
        {
            detectedMembers.Add(viewerId);
            OnNewMember?.Invoke(viewerId, viewerName);
        }
    }

    void DetectMember(string viewerId, string viewerName)
    {
        if (detectedMembers.Contains(viewerId)) return;
        detectedMembers.Add(viewerId);
        Debug.Log($"[YouTubeChat] メンバー検出: {viewerName}");
        OnMemberDetected?.Invoke(viewerId, viewerName);
    }

    int ConvertToJPY(string amountMicros, string currency)
    {
        if (!long.TryParse(amountMicros, out long micros)) return 200; // fallback
        float amount = micros / 1000000f;
        switch (currency)
        {
            case "JPY": return Mathf.RoundToInt(amount);
            case "USD": return Mathf.RoundToInt(amount * 150f);
            case "EUR": return Mathf.RoundToInt(amount * 165f);
            case "GBP": return Mathf.RoundToInt(amount * 190f);
            case "KRW": return Mathf.RoundToInt(amount * 0.11f);
            case "TWD": return Mathf.RoundToInt(amount * 4.7f);
            case "HKD": return Mathf.RoundToInt(amount * 19f);
            case "CAD": return Mathf.RoundToInt(amount * 110f);
            case "AUD": return Mathf.RoundToInt(amount * 100f);
            default:    return Mathf.RoundToInt(amount * 150f);
        }
    }

    int GetGameTier(int jpyAmount)
    {
        for (int i = GameConfig.SuperChatTierMinJPY.Length - 1; i >= 0; i--)
        {
            if (jpyAmount >= GameConfig.SuperChatTierMinJPY[i]) return i;
        }
        return 0; // 最低ティア
    }

    // ─── いいね数ポーリング ─────────────────────────────────

    IEnumerator FetchLikeCount()
    {
        string url = $"https://www.googleapis.com/youtube/v3/videos?part=statistics&id={videoId}";

        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) yield break;

            var response = JsonUtility.FromJson<VideoStatisticsResponse>(req.downloadHandler.text);
            if (response.items == null || response.items.Length == 0) yield break;

            if (int.TryParse(response.items[0].statistics.likeCount, out int likes))
            {
                lastLikeCount = likes;
                CheckLikeMilestones(likes);
            }
        }
    }

    void CheckLikeMilestones(int likeCount)
    {
        for (int i = GameConfig.LikeMilestones.Length - 1; i >= 0; i--)
        {
            if (likeCount >= GameConfig.LikeMilestones[i] && i > currentLikeMilestoneIndex)
            {
                currentLikeMilestoneIndex = i;
                Debug.Log($"[YouTubeChat] いいねマイルストーン達成! {likeCount} likes (milestone {i})");
                OnLikeMilestone?.Invoke(i, likeCount);
                break;
            }
        }
    }

    public int GetCurrentLikeCount() => lastLikeCount;

    // ─── UI Helper ──────────────────────────────────────────

    void ShowUI(string text, float duration, int fontSize)
    {
        var ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.ShowAnnouncement(text, duration, fontSize);
    }

    // ─── JSON Models ─────────────────────────────────────────

    [Serializable] class TokenResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public string token_type;
    }

    [Serializable] class VideoListResponse
    {
        public VideoItem[] items;
    }
    [Serializable] class VideoItem
    {
        public LiveStreamingDetails liveStreamingDetails;
    }
    [Serializable] class LiveStreamingDetails
    {
        public string activeLiveChatId;
    }

    [Serializable] class ChatMessageListResponse
    {
        public string nextPageToken;
        public int pollingIntervalMillis;
        public ChatMessageItem[] items;
    }
    [Serializable] class ChatMessageItem
    {
        public ChatSnippet snippet;
        public AuthorDetails authorDetails;
    }
    [Serializable] class ChatSnippet
    {
        public string type;
        public string displayMessage;
        public SuperChatDetails superChatDetails;
    }
    [Serializable] class SuperChatDetails
    {
        public string amountMicros;
        public string currency;
        public string amountDisplayString;
        public int tier;
    }
    [Serializable] class AuthorDetails
    {
        public string channelId;
        public string displayName;
        public string profileImageUrl;
        public bool isChatSponsor;
    }

    // Video statistics (for like count)
    [Serializable] class VideoStatisticsResponse
    {
        public VideoStatItem[] items;
    }
    [Serializable] class VideoStatItem
    {
        public VideoStatistics statistics;
    }
    [Serializable] class VideoStatistics
    {
        public string likeCount;
    }
}
