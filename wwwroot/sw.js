/**
 * Service Worker for the PWA 
 */
importScripts('/js/db-helper.js');

const APP_VERSION = '20260615-22';
const APP_SHELL_CACHE_PREFIX = 'app-shell-cache-';
const APP_SHELL_CACHE_NAME = `${APP_SHELL_CACHE_PREFIX}${APP_VERSION}`;
const APP_SHELL_LOCAL_URLS = [
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
const offlineMediaClientIds = new Set();
const persistedMediaPreferenceMap = new Map();
let downloadMessageSequence = 0;
const CACHEABLE_MEDIA_EXTENSIONS = ['.m3u8', '.ts', '.m4s', '.mp4', '.aac', '.m4a', '.mp3', '.vtt', '.webvtt', '.key'];
const STALE_DOWNLOAD_GRACE_MS = 30000;
const HLS_DOWNLOAD_CONCURRENCY = 4;
const PERSISTED_MEDIA_PREFERENCE_TTL_MS = 15000;

function getDownloadKey(contentName, mediaType) {
    return `${contentName || ''}::${mediaType || 'unknown'}`;
}

function getPayloadDownloadKeys(payload) {
    if (payload?.mediaType) {
        return [getDownloadKey(payload.contentName, payload.mediaType)];
    }

    return ['video', 'audio', 'unknown'].map(mediaType => getDownloadKey(payload?.contentName, mediaType));
}

function getPayloadMediaTypes(payload) {
    if (payload?.mediaType) {
        return [payload.mediaType];
    }

    return ['video', 'audio'];
}

function getMediaCacheName(contentName, mediaType) {
    return `${mediaType}-cache-${contentName}`;
}

function getMediaStatus(record, mediaType) {
    if (!record) return 'none';
    return mediaType === 'video'
        ? (record.videoStatus || 'none')
        : (record.audioStatus || 'none');
}

function getMediaPreferenceCacheKey(contentName, mediaType) {
    return `${contentName || ''}::${mediaType || 'unknown'}`;
}

function setPersistedMediaPreference(contentName, mediaType, value) {
    if (!contentName || !mediaType) return;
    persistedMediaPreferenceMap.set(getMediaPreferenceCacheKey(contentName, mediaType), {
        value: !!value,
        expiresAt: Date.now() + PERSISTED_MEDIA_PREFERENCE_TTL_MS
    });
}

function clearPersistedMediaPreference(contentName, mediaType = null) {
    if (!contentName) return;

    if (mediaType) {
        persistedMediaPreferenceMap.delete(getMediaPreferenceCacheKey(contentName, mediaType));
        return;
    }

    ['video', 'audio'].forEach(type => {
        persistedMediaPreferenceMap.delete(getMediaPreferenceCacheKey(contentName, type));
    });
}

function getMediaDownloadStartedAt(record, mediaType) {
    return Math.max(
        Number(record?.[`${mediaType}DownloadStartedAt`] || 0),
        Number(record?.[`${mediaType}StatusUpdatedAt`] || 0)
    );
}

function isStaleDownloadingRecord(record, mediaType) {
    if (getMediaStatus(record, mediaType) !== 'downloading') {
        return false;
    }

    const startedAt = getMediaDownloadStartedAt(record, mediaType);
    return !startedAt || Date.now() - startedAt > STALE_DOWNLOAD_GRACE_MS;
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

async function postDownloadProgress(downloadKey, metadata, progress) {
    const normalizedProgress = Math.max(0, Math.min(100, Math.round(progress)));
    if (normalizedProgress <= (progressMap.get(downloadKey) || 0)) {
        return;
    }

    progressMap.set(downloadKey, normalizedProgress);
    await postDownloadMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...metadata, progress: normalizedProgress } });
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
            normalizeOfflineMediaRecords(),
            clients.claim()
        ])
    );
});

async function deleteMediaCache(contentName, mediaType) {
    try {
        await caches.delete(getMediaCacheName(contentName, mediaType));
    } catch (error) {
        console.warn('[SW] Failed to delete media cache:', contentName, mediaType, error);
    }
}

async function cleanupRemovedMedia(removedMedia) {
    const removals = Array.isArray(removedMedia) ? removedMedia : [];
    await Promise.all(removals.map(item => {
        if (!item?.contentName || !item?.mediaType) return null;
        setPersistedMediaPreference(item.contentName, item.mediaType, false);
        return deleteMediaCache(item.contentName, item.mediaType);
    }));
}

async function normalizeOfflineMediaRecords() {
    if (typeof offlineContentDB?.normalizeAllLessons !== 'function') {
        return;
    }

    try {
        const normalized = await offlineContentDB.normalizeAllLessons();
        await cleanupRemovedMedia(normalized?.removedMedia);
    } catch (error) {
        console.warn('[SW] Offline media normalization failed:', error);
    }
}

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

function getAudioAppCacheKey(urlObj) {
    const contentName = getMediaContentName(urlObj);
    if (!contentName) {
        return null;
    }

    const fileName = urlObj.pathname.split('/').pop() || '';
    if (!/^(?:audio|audio-short)\.mp3$/i.test(fileName)) {
        return null;
    }

    return `${self.location.origin}/media/audio/${encodeURIComponent(contentName)}/${encodeURIComponent(fileName)}`;
}

function getMediaCacheStorageKey(url) {
    const urlObj = new URL(url, self.location.origin);
    return getAudioAppCacheKey(urlObj) || stripSasToken(url);
}

function getMediaCacheLookupKeys(url) {
    const urlObj = new URL(url, self.location.origin);
    return [...new Set([
        stripSasToken(url),
        getAudioAppCacheKey(urlObj)
    ].filter(Boolean))];
}

function safeDecode(value) {
    try {
        return decodeURIComponent(value);
    } catch {
        return value;
    }
}

function getMediaContentName(urlObj) {
    const appMediaMatch = /^\/media\/(?:hls|audio)\/([^/]+)/i.exec(urlObj.pathname);
    if (appMediaMatch) {
        return safeDecode(appMediaMatch[1]);
    }

    if (urlObj.hostname.endsWith('.blob.core.windows.net')) {
        const firstPathPart = urlObj.pathname.split('/').filter(Boolean)[0];
        return firstPathPart ? safeDecode(firstPathPart) : null;
    }

    return null;
}

function getPathExtension(pathname) {
    const fileName = pathname.split('/').pop() || '';
    const dotIndex = fileName.lastIndexOf('.');
    return dotIndex >= 0 ? fileName.slice(dotIndex).toLowerCase() : '';
}

function getMediaCacheNames(urlObj) {
    const contentName = getMediaContentName(urlObj);
    if (!contentName) {
        return [];
    }

    const extension = getPathExtension(urlObj.pathname);
    const isAudioPath = /^\/media\/audio\//i.test(urlObj.pathname) || extension === '.mp3';
    const cacheNames = [];

    if (isAudioPath) {
        cacheNames.push(`audio-cache-${contentName}`);
    }

    if (!isAudioPath) {
        cacheNames.push(`video-cache-${contentName}`);
    }

    return [...new Set(cacheNames)];
}

function getMediaTypeFromUrl(urlObj) {
    const extension = getPathExtension(urlObj.pathname);
    if (/^\/media\/audio\//i.test(urlObj.pathname) || extension === '.mp3') {
        return 'audio';
    }

    return getMediaContentName(urlObj) && isCacheableMediaPath(urlObj.pathname)
        ? 'video'
        : null;
}

async function shouldPreferPersistedCachedMedia(urlObj) {
    const contentName = getMediaContentName(urlObj);
    const mediaType = getMediaTypeFromUrl(urlObj);
    if (!contentName || !mediaType || typeof offlineContentDB?.getLesson !== 'function') {
        return false;
    }

    const cacheKey = getMediaPreferenceCacheKey(contentName, mediaType);
    const cachedPreference = persistedMediaPreferenceMap.get(cacheKey);
    if (cachedPreference && cachedPreference.expiresAt > Date.now()) {
        return cachedPreference.value;
    }

    try {
        const record = await offlineContentDB.getLesson(contentName);
        const shouldPreferCache = getMediaStatus(record, mediaType) === 'completed';
        setPersistedMediaPreference(contentName, mediaType, shouldPreferCache);
        return shouldPreferCache;
    } catch (error) {
        console.warn('[SW] Failed to read persisted media preference:', contentName, mediaType, error);
        persistedMediaPreferenceMap.delete(cacheKey);
        return false;
    }
}

async function matchCachedMediaResponse(request, urlObj) {
    const lookupKeys = getMediaCacheLookupKeys(request.url);
    const cacheNames = getMediaCacheNames(urlObj);

    for (const cacheName of cacheNames) {
        if (!await caches.has(cacheName)) {
            continue;
        }

        const cache = await caches.open(cacheName);
        for (const cacheKey of lookupKeys) {
            const cachedResponse = await cache.match(cacheKey);
            if (cachedResponse) {
                return cachedResponse;
            }
        }
    }

    return null;
}

function hasOfflineLessonReferrer(request) {
    if (!request.referrer) {
        return false;
    }

    try {
        return new URL(request.referrer).pathname === '/offline-lesson.html';
    } catch {
        return false;
    }
}

function shouldHandleCachedMediaRequest(event, urlObj) {
    if (event.clientId && offlineMediaClientIds.has(event.clientId)) {
        return true;
    }

    if (hasOfflineLessonReferrer(event.request)) {
        return true;
    }

    return false;
}

function clearOfflineMediaClient(event) {
    const clientId = event.clientId || event.resultingClientId;
    if (clientId) {
        offlineMediaClientIds.delete(clientId);
    }
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

    const headerLength = parseInt(response.headers.get('Content-Length'), 10);
    if (Number.isFinite(headerLength) && headerLength > 0 && response.body && typeof ReadableStream !== 'undefined') {
        return createStreamingRangedResponse(response, match, headerLength);
    }

    return createBlobRangedResponse(response, match);
}

function getSatisfiableRange(match, totalLength) {
    const startValue = match[1];
    const endValue = match[2];
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
        return null;
    }

    return { start, end };
}

async function createStreamingRangedResponse(response, match, totalLength) {
    const range = getSatisfiableRange(match, totalLength);
    if (!range) {
        return new Response(null, {
            status: 416,
            statusText: 'Range Not Satisfiable',
            headers: {
                'Content-Range': `bytes */${totalLength}`,
                'Accept-Ranges': 'bytes'
            }
        });
    }

    const { start, end } = range;
    const length = end - start + 1;
    const reader = response.body.getReader();
    let bytesSeen = 0;

    const stream = new ReadableStream({
        async start(controller) {
            try {
                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    if (!value) continue;

                    const chunkStart = bytesSeen;
                    const chunkEnd = bytesSeen + value.byteLength - 1;
                    bytesSeen += value.byteLength;

                    if (chunkEnd < start) continue;
                    if (chunkStart > end) break;

                    const sliceStart = Math.max(0, start - chunkStart);
                    const sliceEnd = Math.min(value.byteLength, end - chunkStart + 1);
                    controller.enqueue(value.slice(sliceStart, sliceEnd));

                    if (chunkEnd >= end) break;
                }

                controller.close();
            } catch (error) {
                controller.error(error);
            } finally {
                try {
                    await reader.cancel();
                } catch {
                }
            }
        }
    });

    const headers = new Headers(response.headers);
    headers.set('Accept-Ranges', 'bytes');
    headers.set('Content-Length', String(length));
    headers.set('Content-Range', `bytes ${start}-${end}/${totalLength}`);

    return new Response(stream, {
        status: 206,
        statusText: 'Partial Content',
        headers
    });
}

async function createBlobRangedResponse(response, match) {
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

    const range = getSatisfiableRange(match, totalLength);
    if (!range) {
        return new Response(null, {
            status: 416,
            statusText: 'Range Not Satisfiable',
            headers: {
                'Content-Range': `bytes */${totalLength}`,
                'Accept-Ranges': 'bytes'
            }
        });
    }

    const { start, end } = range;
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

    const cacheKey = getMediaCacheStorageKey(urlWithSas);
    console.log(`[SW] Caching media: ${cacheKey}`);

    await cache.put(cacheKey, response);
    throwIfDownloadCancelled(downloadKey);
}

async function cacheMediaUrlsConcurrently(urls, cache, metadata, downloadKey, signal) {
    const totalFiles = urls.length;
    if (!totalFiles) return;

    let nextIndex = 0;
    let completedFiles = 0;
    let stopped = false;
    let firstError = null;
    const workerCount = Math.min(HLS_DOWNLOAD_CONCURRENCY, totalFiles);

    const cacheNextUrl = async () => {
        while (!stopped) {
            const currentIndex = nextIndex++;
            if (currentIndex >= totalFiles) {
                return;
            }

            try {
                throwIfDownloadCancelled(downloadKey);
                await cacheWithoutSas(cache, urls[currentIndex], downloadKey, signal);
                throwIfDownloadCancelled(downloadKey);

                completedFiles += 1;
                const progress = Math.round((completedFiles / totalFiles) * 100);
                await postDownloadProgress(downloadKey, metadata, progress);
            } catch (error) {
                stopped = true;
                firstError = firstError || error;
                return;
            }
        }
    };

    await Promise.all(Array.from({ length: workerCount }, () => cacheNextUrl()));

    if (firstError) {
        throw firstError;
    }
}

async function cacheLessonImage(cache, metadata, signal, downloadKey) {
    const imageUrl = metadata?.imageUrl;
    if (!imageUrl) return;

    try {
        throwIfDownloadCancelled(downloadKey);

        const imageCacheKey = new URL(imageUrl, self.location.origin).href;
        const response = await fetch(imageCacheKey, signal ? { signal } : undefined);
        if (!response.ok) {
            throw new Error(`Failed to fetch lesson image ${imageCacheKey}: ${response.status}`);
        }

        throwIfDownloadCancelled(downloadKey);
        await cache.put(imageCacheKey, response);
    } catch (error) {
        if (isDownloadCancelledError(error)) {
            throw error;
        }

        console.warn('[SW] Lesson image was not cached for offline use:', imageUrl, error);
    }
}

async function reportSingleFileDownloadProgress(response, metadata, downloadKey, signal) {
    const totalBytes = parseInt(response.headers.get('Content-Length') || '', 10);
    const hasKnownLength = Number.isFinite(totalBytes) && totalBytes > 0;

    await postDownloadProgress(downloadKey, metadata, hasKnownLength ? 1 : 5);

    if (!response.body || typeof response.body.getReader !== 'function') {
        return;
    }

    const reader = response.body.getReader();
    let loadedBytes = 0;
    let lastPostedAt = 0;

    try {
        while (true) {
            throwIfDownloadCancelled(downloadKey);
            if (signal?.aborted) {
                throw new DOMException('Download aborted', 'AbortError');
            }

            const { done, value } = await reader.read();
            if (done) {
                break;
            }

            loadedBytes += value?.byteLength || 0;
            const now = Date.now();
            let progress;

            if (hasKnownLength) {
                progress = Math.min(99, Math.floor((loadedBytes / totalBytes) * 99));
            } else {
                const loadedMegabytes = loadedBytes / (1024 * 1024);
                progress = Math.min(95, 5 + Math.floor(loadedMegabytes * 2));
            }

            if (progress > (progressMap.get(downloadKey) || 0) && (now - lastPostedAt >= 250 || progress >= 99)) {
                lastPostedAt = now;
                await postDownloadProgress(downloadKey, metadata, progress);
            }
        }
    } finally {
        try {
            reader.releaseLock();
        } catch {
        }
    }
}

async function clearInactiveDownload(payload, mediaType, messageType) {
    const contentName = payload?.contentName;
    if (!contentName || !mediaType) return;

    setPersistedMediaPreference(contentName, mediaType, false);
    await offlineContentDB.clearMedia(contentName, mediaType);
    await deleteMediaCache(contentName, mediaType);
    await postDownloadMessage({
        type: messageType,
        payload: { ...payload, mediaType }
    });
}

async function normalizeLessonMedia(contentName, preferredMediaType = null) {
    if (!contentName || typeof offlineContentDB?.normalizeLesson !== 'function') {
        return { record: null, removedMediaTypes: [] };
    }

    const normalized = await offlineContentDB.normalizeLesson(contentName, preferredMediaType);
    await Promise.all((normalized?.removedMediaTypes || [])
        .map(mediaType => {
            setPersistedMediaPreference(contentName, mediaType, false);
            return deleteMediaCache(contentName, mediaType);
        }));
    return normalized || { record: null, removedMediaTypes: [] };
}

function isDownloadedLessonRecord(record) {
    return record?.videoStatus === 'completed' || record?.audioStatus === 'completed';
}

function normalizeNavigationPath(pathname) {
    const decodedPathname = safeDecode(pathname || '/');
    return decodedPathname.replace(/\/+$/, '') || '/';
}

async function findOfflineLessonForNavigation(url) {
    const decodedPathname = safeDecode(url.pathname);
    const lessonMatch = decodedPathname.match(/\/שיעור-([^/?#]+)\/?/);

    if (lessonMatch?.[1]) {
        try {
            const contentName = lessonMatch[1];
            const lessonData = await offlineContentDB.getLesson(contentName);
            if (isDownloadedLessonRecord(lessonData)) {
                return lessonData;
            }
        } catch (error) {
            console.warn('[SW] Failed to read offline lesson by content name:', error);
        }
    }

    try {
        const targetPath = normalizeNavigationPath(url.pathname);
        const lessons = await offlineContentDB.getAllLessons();
        return (Array.isArray(lessons) ? lessons : []).find(lesson => {
            if (!isDownloadedLessonRecord(lesson) || !lesson.url) return false;
            try {
                const lessonUrl = new URL(lesson.url, self.location.origin);
                return normalizeNavigationPath(lessonUrl.pathname) === targetPath;
            } catch {
                return false;
            }
        }) || null;
    } catch (error) {
        console.warn('[SW] Failed to find offline lesson by URL:', error);
        return null;
    }
}

async function handleCancelDownload(payload) {
    let activeDownloadFound = false;
    for (const downloadKey of getPayloadDownloadKeys(payload)) {
        if (progressMap.has(downloadKey) || abortControllerMap.has(downloadKey)) {
            activeDownloadFound = true;
        }
        cancellationMap.set(downloadKey, true);
        abortControllerMap.get(downloadKey)?.abort();
    }

    if (activeDownloadFound) {
        return;
    }

    const mediaTypes = getPayloadMediaTypes(payload);
    const normalized = await normalizeLessonMedia(payload.contentName, payload.mediaType || null);
    const record = normalized?.record || await offlineContentDB.getLesson(payload.contentName);
    const cancellableMediaTypes = mediaTypes.filter(mediaType => getMediaStatus(record, mediaType) === 'downloading');

    await Promise.all(cancellableMediaTypes.map(mediaType =>
        clearInactiveDownload(payload, mediaType, 'DOWNLOAD_CANCELLED')
            .catch(error => console.warn('[SW] Failed to clear inactive cancelled download:', error))
    ));
}

async function handleDownloadStatusQuery(event, payload) {
    let activeProgressFound = false;
    const mediaTypes = getPayloadMediaTypes(payload);

    for (const mediaType of mediaTypes) {
        const downloadKey = getDownloadKey(payload.contentName, mediaType);
        if (progressMap.has(downloadKey)) {
            activeProgressFound = true;
            const progress = progressMap.get(downloadKey);
            event.source?.postMessage({ type: 'DOWNLOAD_PROGRESS', payload: { ...payload, mediaType, progress } });
        }
    }

    if (activeProgressFound) {
        return;
    }

    const normalized = await normalizeLessonMedia(payload.contentName, payload.mediaType || null);
    const record = normalized?.record || await offlineContentDB.getLesson(payload.contentName);
    const staleMediaTypes = mediaTypes.filter(mediaType => isStaleDownloadingRecord(record, mediaType));

    await Promise.all(staleMediaTypes.map(mediaType =>
        clearInactiveDownload(payload, mediaType, 'DOWNLOAD_FAILED')
            .catch(error => console.warn('[SW] Failed to clear stale download:', error))
    ));
}

self.addEventListener('message', event => {
    const { type, payload } = event.data || {};
    if (!type) return;

    if (type === 'SKIP_WAITING') {
        event.waitUntil(self.skipWaiting());
        return;
    }

    if (type === 'ENABLE_OFFLINE_MEDIA') {
        if (event.source?.id) {
            offlineMediaClientIds.add(event.source.id);
        }
        return;
    }

    if (type === 'DISABLE_OFFLINE_MEDIA') {
        if (event.source?.id) {
            offlineMediaClientIds.delete(event.source.id);
        }
        return;
    }

    if (!payload) return;

    if (type === 'CANCEL_DOWNLOAD') {
        event.waitUntil(handleCancelDownload(payload));
        return;
    }

    if (type === 'QUERY_DOWNLOAD_STATUS') {
        event.waitUntil(handleDownloadStatusQuery(event, payload));
        return;
    }

    if (type === 'CACHE_MEDIA') {
        event.waitUntil(handleMediaCache(payload));
        return;
    }

    if (type === 'DELETE_MEDIA_CACHE') {
        event.waitUntil(handleMediaDelete(payload));
        return;
    }
});

async function handleMediaCache(payload) {
    const { urls, metadata } = payload;
    const { contentName, mediaType, resolution } = metadata;
    const cacheName = getMediaCacheName(contentName, mediaType);
    const downloadKey = getDownloadKey(contentName, mediaType);

    clearPersistedMediaPreference(contentName, mediaType);
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
            await cacheMediaUrlsConcurrently(urls, cache, metadata, downloadKey, abortController.signal);
        }

        await cacheLessonImage(cache, metadata, abortController.signal, downloadKey);

        await offlineContentDB.updateMediaStatus(contentName, mediaType, 'completed');
        setPersistedMediaPreference(contentName, mediaType, true);
        await postDownloadMessage({ type: 'DOWNLOAD_COMPLETE', payload: metadata });
    } catch (error) {
        console.error('[SW] Download error:', error);
        await caches.delete(cacheName);
        setPersistedMediaPreference(contentName, mediaType, false);
        try {
            await offlineContentDB.updateMediaStatus(contentName, mediaType, 'none');
        } catch (dbError) {
            console.warn('[SW] Download failed but IndexedDB status reset failed:', dbError);
        }
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

    try {
        setPersistedMediaPreference(contentName, mediaType, false);
        await offlineContentDB.clearMedia(contentName, mediaType);
        await deleteMediaCache(contentName, mediaType);
        await postDownloadMessage({ type: 'DELETE_MEDIA_CACHE_COMPLETE', payload });
    } catch (error) {
        console.error('[SW] Media delete failed:', error);
        await postDownloadMessage({ type: 'DELETE_MEDIA_CACHE_FAILED', payload });
    }
}

async function handleSingleFileDownload(url, cache, metadata, signal, downloadKey) {
    throwIfDownloadCancelled(downloadKey);

    const response = await fetch(url, signal ? { signal } : undefined);
    if (!response.ok) {
        throw new Error(`Server responded with status: ${response.status}`);
    }

    throwIfDownloadCancelled(downloadKey);

    const progressResponse = response.clone();
    const progressTask = reportSingleFileDownloadProgress(progressResponse, metadata, downloadKey, signal)
        .catch(error => {
            if (isDownloadCancelledError(error)) {
                throw error;
            }

            console.warn('[SW] Single file progress reporting failed:', error);
        });

    await Promise.all([
        cache.put(getMediaCacheStorageKey(url), response),
        progressTask
    ]);
    throwIfDownloadCancelled(downloadKey);

    await postDownloadProgress(downloadKey, metadata, 100);
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
        if (url.pathname !== '/offline-lesson.html') {
            clearOfflineMediaClient(event);
        }

        event.respondWith(
            fetch(event.request).catch(async () => {

                // Handle offline lesson player
                if (url.pathname === '/offline-lesson.html') {
                    return caches.match('/offline-lesson.html');
                }

                const offlineLesson = await findOfflineLessonForNavigation(url);
                if (offlineLesson?.contentName) {
                    console.log(`[SW] Offline lesson "${offlineLesson.contentName}" found. Redirecting to offline player.`);
                    return Response.redirect(`/offline-lesson.html?contentName=${encodeURIComponent(offlineLesson.contentName)}`, 302);
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
                const isOffline = self.navigator?.onLine === false;
                const shouldPreferCachedMediaFromClient = shouldHandleCachedMediaRequest(event, url);
                const shouldPreferCachedMediaFromStore = shouldPreferCachedMediaFromClient || !isOffline
                    ? false
                    : await shouldPreferPersistedCachedMedia(url);
                const shouldPreferCachedMedia = shouldPreferCachedMediaFromClient || shouldPreferCachedMediaFromStore;

                if (shouldPreferCachedMediaFromStore && event.clientId) {
                    offlineMediaClientIds.add(event.clientId);
                }

                if (!shouldPreferCachedMedia && !isOffline) {
                    try {
                        return await fetch(event.request);
                    } catch (error) {
                        console.warn('[SW] Media network request failed, trying cache:', event.request.url, error);
                    }
                }

                const cachedResponse = await matchCachedMediaResponse(event.request, url);

                if (cachedResponse) {
                    return isRangeRequest
                        ? createRangedResponse(event.request, cachedResponse)
                        : cachedResponse;
                }

                if (!isOffline) {
                    try {
                        return await fetch(event.request);
                    } catch (error) {
                        console.warn('[SW] Media network fallback failed:', event.request.url, error);
                    }
                }

                console.warn('[SW] Cached media request was not found:', event.request.url);
                return new Response('Media not available offline', {
                    status: 404,
                    statusText: 'Not Found'
                });
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
