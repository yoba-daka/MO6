/**
 * Service Worker for the PWA 
 */
importScripts('/js/db-helper.js');

const APP_VERSION = '20260614-2';
const APP_SHELL_CACHE_PREFIX = 'app-shell-cache-';
const APP_SHELL_CACHE_NAME = `${APP_SHELL_CACHE_PREFIX}${APP_VERSION}`;
const APP_SHELL_LOCAL_URLS = [
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
    '/js/hls.js'
];
const APP_SHELL_OPTIONAL_URLS = [
    'https://maxcdn.bootstrapcdn.com/font-awesome/latest/css/font-awesome.min.css',
    'https://fonts.googleapis.com/css?family=Heebo:400,500,700|Material+Icons'
];
const broadcast = 'BroadcastChannel' in self ? new BroadcastChannel('download_channel') : null;
const cancellationMap = new Map();
const abortControllerMap = new Map();
const progressMap = new Map();
let downloadMessageSequence = 0;
const CACHEABLE_MEDIA_EXTENSIONS = ['.m3u8', '.ts', '.m4s', '.mp4', '.aac', '.m4a', '.mp3', '.vtt', '.webvtt', '.key'];

function getDownloadKey(contentName, mediaType) {
    return `${contentName || ''}::${mediaType || 'unknown'}`;
}

function getPayloadDownloadKeys(payload) {
    if (payload?.mediaType) {
        return [getDownloadKey(payload.contentName, payload.mediaType)];
    }

    return ['video', 'audio', 'unknown'].map(mediaType => getDownloadKey(payload?.contentName, mediaType));
}

async function postDownloadMessage(message) {
    const messageWithId = {
        ...message,
        messageId: `${APP_VERSION}:${Date.now()}:${++downloadMessageSequence}`
    };

    if (broadcast) {
        broadcast.postMessage(messageWithId);
    }

    const windowClients = await clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of windowClients) {
        client.postMessage(messageWithId);
    }
}

function throwIfDownloadCancelled(downloadKey) {
    if (cancellationMap.get(downloadKey) === true) {
        throw new Error('Download cancelled by user');
    }
}

function isDownloadCancelledError(error) {
    return error?.message === 'Download cancelled by user' || error?.name === 'AbortError';
}

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(APP_SHELL_CACHE_NAME)
            .then(async cache => {
                await cache.addAll(APP_SHELL_LOCAL_URLS);
                await Promise.all(APP_SHELL_OPTIONAL_URLS.map(url =>
                    cache.add(url).catch(error => {
                        console.warn('[SW] Optional app-shell asset was not cached:', url, error);
                    })
                ));
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
    const urlObj = new URL(url, self.location.origin);
    if (isCacheableMediaPath(urlObj.pathname)) {
        return urlObj.origin + urlObj.pathname;
    }
    return url;
}

function isCacheableMediaPath(pathname) {
    return CACHEABLE_MEDIA_EXTENSIONS.some(extension => pathname.endsWith(extension));
}

async function createRangedResponse(request, response) {
    const rangeHeader = request.headers.get('range');
    if (!rangeHeader || response.status === 206) {
        return response;
    }

    const match = /^bytes=(\d*)-(\d*)$/i.exec(rangeHeader);
    if (!match) {
        return response;
    }

    const startValue = match[1];
    const endValue = match[2];
    if (!startValue && !endValue) {
        return response;
    }

    const sourceBlob = await response.blob();
    const totalLength = sourceBlob.size;
    if (totalLength <= 0) {
        return new Response(null, {
            status: 416,
            statusText: 'Range Not Satisfiable',
            headers: {
                'Content-Range': `bytes */${totalLength}`,
                'Accept-Ranges': 'bytes'
            }
        });
    }

    let start;
    let end;

    if (!startValue) {
        const suffixLength = parseInt(endValue, 10);
        if (Number.isNaN(suffixLength) || suffixLength <= 0) {
            start = totalLength;
            end = totalLength - 1;
        } else {
            start = Math.max(totalLength - suffixLength, 0);
            end = totalLength - 1;
        }
    } else {
        start = parseInt(startValue, 10);
        end = endValue ? parseInt(endValue, 10) : totalLength - 1;
        if (Number.isNaN(end) || end >= totalLength) end = totalLength - 1;
    }

    if (Number.isNaN(start) || Number.isNaN(end) || start < 0 || start >= totalLength || end < start) {
        return new Response(null, {
            status: 416,
            statusText: 'Range Not Satisfiable',
            headers: {
                'Content-Range': `bytes */${totalLength}`,
                'Accept-Ranges': 'bytes'
            }
        });
    }

    const slicedBlob = sourceBlob.slice(start, end + 1);
    const headers = new Headers(response.headers);
    headers.set('Accept-Ranges', 'bytes');
    headers.set('Content-Length', String(slicedBlob.size));
    headers.set('Content-Range', `bytes ${start}-${end}/${totalLength}`);

    return new Response(slicedBlob, {
        status: 206,
        statusText: 'Partial Content',
        headers
    });
}

async function cacheWithoutSas(cache, urlWithSas, downloadKey, signal) {
    throwIfDownloadCancelled(downloadKey);

    const response = await fetch(urlWithSas, signal ? { signal } : undefined);
    if (!response.ok) {
        throw new Error(`Failed to fetch ${urlWithSas}: ${response.status}`);
    }

    throwIfDownloadCancelled(downloadKey);

    const urlWithoutSas = stripSasToken(urlWithSas);
    console.log(`[SW] Caching without SAS: ${urlWithoutSas}`);

    await cache.put(urlWithoutSas, response.clone());
    throwIfDownloadCancelled(downloadKey);
    return response;
}

self.addEventListener('message', event => {
    const { type, payload } = event.data || {};
    if (!type || !payload) return;

    if (type === 'CANCEL_DOWNLOAD') {
        for (const downloadKey of getPayloadDownloadKeys(payload)) {
            cancellationMap.set(downloadKey, true);
            abortControllerMap.get(downloadKey)?.abort();
        }
        return;
    }

    if (type === 'QUERY_DOWNLOAD_STATUS') {
        const downloadKeys = getPayloadDownloadKeys(payload);
        for (const downloadKey of downloadKeys) {
            if (progressMap.has(downloadKey)) {
                const progress = progressMap.get(downloadKey);
                event.source?.postMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...payload, progress } });
                break;
            }
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
    const downloadKey = getDownloadKey(contentName, mediaType);

    cancellationMap.set(downloadKey, false);
    progressMap.set(downloadKey, 0);
    const abortController = new AbortController();
    abortControllerMap.set(downloadKey, abortController);

    try {
        await offlineContentDB.startDownload(metadata, mediaType, resolution);
        await postDownloadMessage({ type: 'DOWNLOAD_STARTED', payload: metadata });

        const cache = await caches.open(cacheName);

        if (mediaType === 'audio' && urls.length === 1) {
            await handleSingleFileDownload(urls[0], cache, metadata, abortController.signal, downloadKey);
        } else {
            const totalFiles = urls.length;
            for (let i = 0; i < totalFiles; i++) {
                throwIfDownloadCancelled(downloadKey);

                await cacheWithoutSas(cache, urls[i], downloadKey, abortController.signal);
                throwIfDownloadCancelled(downloadKey);

                const progress = Math.round(((i + 1) / totalFiles) * 100);
                if (progress > progressMap.get(downloadKey)) {
                    progressMap.set(downloadKey, progress);
                    await postDownloadMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress } });
                }
            }
        }

        await offlineContentDB.updateMediaStatus(contentName, mediaType, 'completed');
        await postDownloadMessage({ type: 'DOWNLOAD_COMPLETE', payload: metadata });
    } catch (error) {
        console.error('[SW] Download error:', error);
        await caches.delete(cacheName);
        await offlineContentDB.updateMediaStatus(contentName, mediaType, 'none');
        const messageType = isDownloadCancelledError(error) ? 'DOWNLOAD_CANCELLED' : 'DOWNLOAD_FAILED';
        await postDownloadMessage({ type: messageType, payload: metadata });
    } finally {
        progressMap.delete(downloadKey);
        cancellationMap.delete(downloadKey);
        abortControllerMap.delete(downloadKey);
    }
}

async function handleMediaDelete(payload) {
    const { contentName, mediaType } = payload;
    const cacheName = `${mediaType}-cache-${contentName}`;
    await caches.delete(cacheName);
    await offlineContentDB.clearMedia(contentName, mediaType);
    await postDownloadMessage({ type: 'DELETE_MEDIA_CACHE_COMPLETE', payload });
}

async function handleSingleFileDownload(url, cache, metadata, signal, downloadKey) {
    throwIfDownloadCancelled(downloadKey);

    const response = await fetch(url, signal ? { signal } : undefined);
    if (!response.ok) {
        throw new Error(`Server responded with status: ${response.status}`);
    }

    throwIfDownloadCancelled(downloadKey);

    const urlWithoutSas = stripSasToken(url);
    const totalSize = parseInt(response.headers.get('Content-Length'), 10);
    const reader = response.body?.getReader();

    if (!reader) {
        await cache.put(urlWithoutSas, response.clone());
        throwIfDownloadCancelled(downloadKey);
        progressMap.set(downloadKey, 100);
        await postDownloadMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress: 100 } });
        return;
    }

    let loaded = 0;
    const chunks = [];

    try {
        while (true) {
            throwIfDownloadCancelled(downloadKey);

            const { done, value } = await reader.read();
            if (done) break;
            if (!value) continue;

            chunks.push(value);
            loaded += value.byteLength || value.length || 0;

            if (totalSize > 0) {
                const progress = Math.min(99, Math.round((loaded / totalSize) * 100));
                if (progress > (progressMap.get(downloadKey) || 0)) {
                    progressMap.set(downloadKey, progress);
                    await postDownloadMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress } });
                }
            }
        }
    } catch (error) {
        try {
            await reader.cancel();
        } catch {
        }

        throw error;
    }

    throwIfDownloadCancelled(downloadKey);

    const headers = new Headers(response.headers);
    const fullResponse = new Response(new Blob(chunks), {
        status: response.status,
        statusText: response.statusText,
        headers
    });

    await cache.put(urlWithoutSas, fullResponse);
    throwIfDownloadCancelled(downloadKey);

    if ((progressMap.get(downloadKey) || 0) < 100) {
        progressMap.set(downloadKey, 100);
        await postDownloadMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress: 100 } });
    }
}

// --- 5. Network Interception and Offline Fallback ---
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    if (event.request.method !== 'GET') {
        event.respondWith(fetch(event.request));
        return;
    }

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
                const decodedPathname = decodeURIComponent(url.pathname);
                const lessonMatch = decodedPathname.match(/\/שיעור-([^/?#]+)\/?/);
                if (lessonMatch && lessonMatch[1]) {
                    const contentName = lessonMatch[1];
                    const lessonData = await offlineContentDB.getLesson(contentName);

                    if (lessonData && (lessonData.videoStatus === 'completed' || lessonData.audioStatus === 'completed')) {
                        console.log(`[SW] Offline lesson "${contentName}" found. Redirecting to offline player.`);
                        return Response.redirect(`/offline-lesson.html?contentName=${encodeURIComponent(contentName)}`, 302);
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

    if (isCacheableMediaPath(url.pathname)) {
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
