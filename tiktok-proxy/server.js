/**
 * TikTok LIVE Proxy Server for King's March
 *
 * Usage:
 *   Mode A (CLI):  node server.js <tiktokUsername>
 *   Mode B (WS):   node server.js   (or tiktok-proxy.exe)
 *     → Unity sends {"command":"connect","username":"xxx"} via WebSocket
 *
 * Connects to TikTok LIVE via tiktok-live-connector and forwards events
 * to Unity via WebSocket (port 21213).
 *
 * Gift batching: rapid gifts from the same user are accumulated for 3 seconds
 * before being forwarded as a single event to prevent Unity from freezing.
 */

const { WebSocketServer } = require('ws');
const { TikTokLiveConnection } = require('tiktok-live-connector');

const PROXY_PORT = 21213;
const GIFT_BATCH_SEC = 3; // seconds to accumulate gifts before flushing
const cliUsername = process.argv[2];

// --- WebSocket Server (Unity connects here) ---
const wss = new WebSocketServer({ port: PROXY_PORT });
const clients = new Set();

let connection = null; // TikTok connection (created on connect)

wss.on('connection', (ws) => {
    console.log('[Proxy] Unity client connected');
    clients.add(ws);

    // Listen for commands from Unity
    ws.on('message', (raw) => {
        try {
            const msg = JSON.parse(raw.toString());
            if (msg.command === 'connect' && msg.username) {
                connectToTikTok(msg.username.replace(/^@/, ''));
            }
        } catch (e) {
            console.error('[Proxy] Invalid message:', e.message);
        }
    });

    ws.on('close', () => {
        clients.delete(ws);
        console.log('[Proxy] Unity client disconnected');
    });
    ws.on('error', (err) => {
        console.error('[Proxy] WebSocket error:', err.message);
        clients.delete(ws);
    });
});

function broadcast(data) {
    const json = JSON.stringify(data);
    for (const ws of clients) {
        if (ws.readyState === 1) { // OPEN
            ws.send(json);
        }
    }
}

// --- Gift Batching ---
const pendingGifts = new Map(); // uniqueId → { user, totalDiamonds, giftName, giftId, lastTime }

// Flush accumulated gifts every second (sends if GIFT_BATCH_SEC elapsed since last gift)
setInterval(() => {
    const now = Date.now();
    for (const [key, pg] of pendingGifts) {
        if (now - pg.lastTime >= GIFT_BATCH_SEC * 1000) {
            broadcast({
                event: 'gift',
                user: pg.user,
                giftId: pg.giftId,
                giftName: pg.giftName,
                diamondCount: pg.totalDiamonds,
                repeatCount: 1,
                totalDiamondCount: pg.totalDiamonds,
                repeatEnd: true,
                giftType: 0,
            });
            console.log(`[Gift Flush] ${pg.user.uniqueId}: ${pg.giftName} total=${pg.totalDiamonds} diamonds`);
            pendingGifts.delete(key);
        }
    }
}, 1000);

// --- Like Throttling ---
let lastLikeBroadcast = 0;
let pendingLike = null;
const LIKE_THROTTLE_MS = 5000; // max 1 like event per 5 seconds

// --- TikTok LIVE Connection ---

function extractUser(data) {
    const u = data.user || data;
    // profilePictureUrl can be a string or an array in v2
    let pic = u.profilePictureUrl || u.avatarUrl || '';
    if (Array.isArray(pic)) pic = pic[0] || '';
    return {
        uniqueId: u.uniqueId || data.uniqueId || '',
        userId: u.userId || data.userId || '',
        nickname: u.nickname || u.uniqueId || '',
        profilePictureUrl: pic,
        badges: u.badges || [],
    };
}

function connectToTikTok(username) {
    if (!username) {
        broadcast({ event: 'error', message: 'Username is empty' });
        return;
    }

    // Disconnect previous connection if any
    if (connection) {
        try { connection.disconnect(); } catch (e) {}
        connection = null;
    }

    // Clear pending gifts from previous connection
    pendingGifts.clear();

    console.log(`[Proxy] Connecting to TikTok LIVE: @${username}`);

    connection = new TikTokLiveConnection(username, {
        enableExtendedGiftInfo: true,
        processInitialData: false,
    });

    connection.connect().then((state) => {
        console.log(`[Proxy] Connected to roomId ${state.roomId}`);
        broadcast({
            event: 'connected',
            roomId: state.roomId,
            username: username,
        });
    }).catch((err) => {
        console.error('[Proxy] Failed to connect:', err.message);
        broadcast({ event: 'error', message: err.message });
    });

    // --- Event Forwarding ---

    // Chat (comment) — forwarded immediately
    connection.on('chat', (data) => {
        const user = extractUser(data);
        console.log(`[Chat] ${user.uniqueId}: ${data.comment}`);
        broadcast({
            event: 'chat',
            user: user,
            comment: data.comment || '',
        });
    });

    // Gift — accumulated per-user, flushed after GIFT_BATCH_SEC of inactivity
    connection.on('gift', (data) => {
        // For streak gifts (giftType === 1), only process when streak ends
        if (data.giftType === 1 && !data.repeatEnd) return;

        const user = extractUser(data);
        const diamondCount = data.diamondCount
            || (data.extendedGiftInfo && data.extendedGiftInfo.diamond_count)
            || (data.extendedGiftInfo && data.extendedGiftInfo.diamondCount)
            || (data.gift && data.gift.diamond_count)
            || 1;
        const repeatCount = data.repeatCount || 1;
        const totalDiamonds = diamondCount * repeatCount;
        const giftName = (data.extendedGiftInfo && data.extendedGiftInfo.name)
            || data.giftName || data.describe || `Gift#${data.giftId}`;

        const key = user.userId || user.uniqueId || 'unknown';
        if (!pendingGifts.has(key)) {
            pendingGifts.set(key, {
                user,
                totalDiamonds: 0,
                giftName,
                giftId: data.giftId || 0,
                lastTime: Date.now(),
            });
        }
        const pg = pendingGifts.get(key);
        pg.totalDiamonds += totalDiamonds;
        pg.lastTime = Date.now();
        pg.giftName = giftName;
        pg.user = user; // update user info

        console.log(`[Gift] ${user.uniqueId} +${totalDiamonds} diamonds (pending: ${pg.totalDiamonds})`);
    });

    // Like — throttled to max 1 event per 5 seconds
    connection.on('like', (data) => {
        const user = extractUser(data);
        pendingLike = {
            event: 'like',
            user: user,
            likeCount: data.likeCount || 0,
            totalLikeCount: data.totalLikeCount || 0,
        };
        const now = Date.now();
        if (now - lastLikeBroadcast >= LIKE_THROTTLE_MS) {
            broadcast(pendingLike);
            lastLikeBroadcast = now;
            pendingLike = null;
            console.log(`[Like] ${user.uniqueId} +${data.likeCount} (total: ${data.totalLikeCount})`);
        }
    });

    // Flush pending like periodically
    setInterval(() => {
        if (pendingLike && Date.now() - lastLikeBroadcast >= LIKE_THROTTLE_MS) {
            broadcast(pendingLike);
            lastLikeBroadcast = Date.now();
            console.log(`[Like Flush] total: ${pendingLike.totalLikeCount}`);
            pendingLike = null;
        }
    }, 2000);

    // Subscribe
    connection.on('subscribe', (data) => {
        const user = extractUser(data);
        console.log(`[Subscribe] ${user.uniqueId} subscribed!`);
        broadcast({
            event: 'subscribe',
            user: user,
        });
    });

    // Follow
    connection.on('follow', (data) => {
        const user = extractUser(data);
        console.log(`[Follow] ${user.uniqueId} followed`);
        broadcast({
            event: 'follow',
            user: user,
        });
    });

    // Share
    connection.on('share', (data) => {
        const user = extractUser(data);
        console.log(`[Share] ${user.uniqueId} shared`);
        broadcast({
            event: 'share',
            user: user,
        });
    });

    // Member (viewer joins live)
    connection.on('member', (data) => {
        const user = extractUser(data);
        broadcast({
            event: 'member',
            user: user,
        });
    });

    // Room user count
    connection.on('roomUser', (data) => {
        broadcast({
            event: 'roomUser',
            viewerCount: data.viewerCount || 0,
        });
    });

    // Stream end
    connection.on('streamEnd', () => {
        console.log('[Proxy] Stream ended');
        broadcast({ event: 'streamEnd' });
    });

    // Disconnected
    connection.on('disconnected', () => {
        console.log('[Proxy] Disconnected from TikTok');
        broadcast({ event: 'disconnected' });
    });

    // Error
    connection.on('error', (err) => {
        console.error('[Proxy] TikTok error:', err.message);
        broadcast({ event: 'error', message: err.message });
    });
}

// --- Startup ---
console.log(`[Proxy] WebSocket server on ws://localhost:${PROXY_PORT}`);

if (cliUsername) {
    connectToTikTok(cliUsername);
} else {
    console.log('[Proxy] Waiting for connect command from Unity...');
}
