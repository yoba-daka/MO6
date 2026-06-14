/**
 * Service Worker for the PWA 
 */
importScripts('/js/db-helper.js');

const APP_VERSION = '20260602-1';
const APP_SHELL_CACHE_PREFIX = 'app-shell-cache-';
const APP_SHELL_CACHE_NAME = `${APP_SHELL_CACHE_PREFIX}${APP_VERSION}`;
const APP_SHELL_URLS = [
    '/',
    '/offline.html',
    '/offline-lesson.html',
    '/js/db-helper.js',
    '/css/material-kit.css?v=2.0.6',
    '/css/plyr.css',
    '/js/core/jquery.min.js',
    '/js/core/popper.min.js',
    '/js/core/bootstrap-material-design.min.js',
    '/js/plyr.js',
    '/js/hls.js',
    'https://maxcdn.bootstrapcdn.com/font-awesome/latest/css/font-awesome.min.css',
    'https://fonts.googleapis.com/css?family=Heebo:400,500,700|Material+Icons'
];
const broadcast = new BroadcastChannel('download_channel');
const cancellationMap = new Map();
const progressMap = new Map();

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(APP_SHELL_CACHE_NAME)
            .then(cache => {
                return cache.addAll(APP_SHELL_URLS);
            })
            .catch(error => {
            })
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        Promise.all([
            caches.keys()
                .then(keys => Promise.all(
                    keys
                        .filter(key => key.startsWith(APP_SHELL_CACHE_PREFIX) && key !== APP_SHELL_CACHE_NAME)
                        .map(key => caches.delete(key))
                )),
            clients.claim()
        ])
    );
});

function stripSasToken(url) {
    const urlObj = new URL(url);
    if (urlObj.pathname.endsWith('.m3u8') || urlObj.pathname.endsWith('.ts') || urlObj.pathname.endsWith('.mp3')) {
        return urlObj.origin + urlObj.pathname;
    }
    return url;
}

async function createRangedResponse(request, response) {
    const rangeHeader = request.headers.get('range');
    if (!rangeHeader || response.status === 206) {
        return response;
    }

    const match = /bytes=(\d*)-(\d*)/i.exec(rangeHeader);
    if (!match) {
        return response;
    }

    const sourceBuffer = await response.arrayBuffer();
    const totalLength = sourceBuffer.byteLength;

    let start = match[1] ? parseInt(match[1], 10) : 0;
    let end = match[2] ? parseInt(match[2], 10) : totalLength - 1;

    if (Number.isNaN(start)) start = 0;
    if (Number.isNaN(end) || end >= totalLength) end = totalLength - 1;

    if (start < 0 || start >= totalLength || end < start) {
        return new Response(null, {
            status: 416,
            statusText: 'Range Not Satisfiable',
            headers: {
                'Content-Range': `bytes */${totalLength}`,
                'Accept-Ranges': 'bytes'
            }
        });
    }

    const slicedBuffer = sourceBuffer.slice(start, end + 1);
    const headers = new Headers(response.headers);
    headers.set('Accept-Ranges', 'bytes');
    headers.set('Content-Length', String(slicedBuffer.byteLength));
    headers.set('Content-Range', `bytes ${start}-${end}/${totalLength}`);

    return new Response(slicedBuffer, {
        status: 206,
        statusText: 'Partial Content',
        headers
    });
}

async function cacheWithoutSas(cache, urlWithSas) {
    const response = await fetch(urlWithSas);
    if (!response.ok) {
        throw new Error(`Failed to fetch ${urlWithSas}: ${response.status}`);
    }

    const urlWithoutSas = stripSasToken(urlWithSas);
    console.log(`[SW] Caching without SAS: ${urlWithoutSas}`);

    await cache.put(urlWithoutSas, response.clone());
    return response;
}

self.addEventListener('message', event => {
    const { type, payload } = event.data;
    if (!type || !payload) return;

    if (type === 'CANCEL_DOWNLOAD') {
        cancellationMap.set(payload.contentName, true);
        return;
    }

    if (type === 'QUERY_DOWNLOAD_STATUS') {
        if (progressMap.has(payload.contentName)) {
            const progress = progressMap.get(payload.contentName);
            event.source.postMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...payload, progress } });
        }
        return;
    }

    if (type === 'CACHE_MEDIA') {
        event.waitUntil(handleMediaCache(payload));
    }

    if (type === 'DELETE_MEDIA_CACHE') {
        event.waitUntil(handleMediaDelete(payload));
    }
});

async function handleMediaCache(payload) {
    const { urls, metadata } = payload;
    const { contentName, mediaType, resolution } = metadata;
    const cacheName = `${mediaType}-cache-${contentName}`;

    cancellationMap.set(contentName, false);
    progressMap.set(contentName, 0);

    try {
        await offlineContentDB.startDownload(metadata, mediaType, resolution);
        broadcast.postMessage({ type: 'DOWNLOAD_STARTED', payload: metadata });

        const cache = await caches.open(cacheName);

        if (mediaType === 'audio' && urls.length === 1) {
            await handleSingleFileDownload(urls[0], cache, metadata);
        } else {
            const totalFiles = urls.length;
            for (let i = 0; i < totalFiles; i++) {
                if (cancellationMap.get(contentName) === true) {
                    throw new Error('Download cancelled by user');
                }

                await cacheWithoutSas(cache, urls[i]);

                const progress = Math.round(((i + 1) / totalFiles) * 100);
                if (progress > progressMap.get(contentName)) {
                    progressMap.set(contentName, progress);
                    broadcast.postMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress } });
                }
            }
        }

        await offlineContentDB.updateMediaStatus(contentName, mediaType, 'completed');
        broadcast.postMessage({ type: 'DOWNLOAD_COMPLETE', payload: metadata });
    } catch (error) {
        console.error('[SW] Download error:', error);
        await caches.delete(cacheName);
        await offlineContentDB.updateMediaStatus(contentName, mediaType, 'none');
        const messageType = error.message === 'Download cancelled by user' ? 'DOWNLOAD_CANCELLED' : 'DOWNLOAD_FAILED';
        broadcast.postMessage({ type: messageType, payload: metadata });
    } finally {
        progressMap.delete(contentName);
        cancellationMap.delete(contentName);
    }
}

async function handleMediaDelete(payload) {
    const { contentName, mediaType } = payload;
    const cacheName = `${mediaType}-cache-${contentName}`;
    await caches.delete(cacheName);
    await offlineContentDB.clearMedia(contentName, mediaType);
    broadcast.postMessage({ type: 'DELETE_MEDIA_CACHE_COMPLETE', payload });
}

async function handleSingleFileDownload(url, cache, metadata) {
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Server responded with status: ${response.status}`);
    }

    const totalSize = parseInt(response.headers.get('Content-Length'), 10);
    let loaded = 0;

    const reader = response.body.getReader();
    const chunks = [];

    while (true) {
        if (cancellationMap.get(metadata.contentName) === true) {
            await reader.cancel();
            throw new Error('Download cancelled by user');
        }

        const { done, value } = await reader.read();
        if (done) break;

        chunks.push(value);
        loaded += value.length;

        if (totalSize) {
            const progress = Math.round((loaded / totalSize) * 100);
            if (progress > (progressMap.get(metadata.contentName) || 0)) {
                progressMap.set(metadata.contentName, progress);
                broadcast.postMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress } });
            }
        }
    }

    // Combine chunks and create response
    const fullResponse = new Response(new Blob(chunks), {
        headers: response.headers
    });

    // Cache without SAS token
    const urlWithoutSas = stripSasToken(url);
    await cache.put(urlWithoutSas, fullResponse);
}

// --- 5. Network Interception and Offline Fallback ---
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    if (url.origin === self.location.origin && url.pathname.startsWith('/sb/')) {
        event.respondWith(fetch(event.request));
        return;
    }

    // Handle navigation requests
    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request).catch(async () => {

                // Handle offline lesson player
                if (url.pathname === '/offline-lesson.html') {
                    return caches.match('/offline-lesson.html');
                }

                // Handle lesson URLs
                const lessonMatch = url.pathname.match(/\/שיעור-([a-zA-Z0-9-]+)\/?/);
                if (lessonMatch && lessonMatch[1]) {
                    const contentName = lessonMatch[1];
                    const lessonData = await offlineContentDB.getLesson(contentName);

                    if (lessonData && (lessonData.videoStatus === 'completed' || lessonData.audioStatus === 'completed')) {
                        console.log(`[SW] Offline lesson "${contentName}" found. Redirecting to offline player.`);
                        return Response.redirect(`/offline-lesson.html?contentName=${contentName}`, 302);
                    }
                }

                // Default offline page
                return caches.match('/offline.html') || new Response("You are offline.", {
                    status: 503,
                    statusText: "Service Unavailable"
                });
            })
        );
        return;
    }

    if (url.pathname.endsWith('.m3u8') || url.pathname.endsWith('.ts') || url.pathname.endsWith('.mp3')) {
        event.respondWith(
            (async () => {
                const isRangeRequest = event.request.headers.has('range');
                let cachedResponse = await caches.match(event.request);

                if (!cachedResponse) {
                    const urlWithoutSas = stripSasToken(event.request.url);

                    const cacheNames = await caches.keys();
                    for (const cacheName of cacheNames) {
                        const cache = await caches.open(cacheName);
                        cachedResponse = await cache.match(urlWithoutSas);
                        if (cachedResponse) {
                            break;
                        }
                    }
                }

                if (cachedResponse) {
                    return isRangeRequest
                        ? createRangedResponse(event.request, cachedResponse)
                        : cachedResponse;
                }

                try {
                    console.log('[SW] Not cached, fetching from network:', event.request.url);
                    return await fetch(event.request);
                } catch (error) {
                    console.error('[SW] Media not found in cache and network fetch failed:', event.request.url, error);
                    return new Response('Media not available offline', {
                        status: 404,
                        statusText: 'Not Found'
                    });
                }
            })()
        );
        return;
    }

    event.respondWith(
        caches.match(event.request).then(cachedResponse => {
            return cachedResponse || fetch(event.request);
        })
    );
});
