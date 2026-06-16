// /js/db-helper.js

const offlineContentDB = {
    db: null,
    dbName: 'OfflineContentDB',
    storeName: 'lessons',
    settingsStoreName: 'userSettings', 
    dbVersion: 4, 
    requestTimeoutMs: 8000,

    _withTimeout(promise, label, timeoutMs = null) {
        let timeoutId;
        const timeoutPromise = new Promise((_, reject) => {
            timeoutId = setTimeout(() => reject(new Error(`${label} timed out`)), timeoutMs || this.requestTimeoutMs);
        });

        return Promise.race([promise, timeoutPromise]).finally(() => clearTimeout(timeoutId));
    },

    _isStoredMediaStatus(status) {
        return status === 'completed' || status === 'downloading';
    },

    _getMediaStatus(record, mediaType) {
        if (!record) return 'none';
        return mediaType === 'video'
            ? (record.videoStatus || 'none')
            : (record.audioStatus || 'none');
    },

    _getMediaUpdatedAt(record, mediaType) {
        if (!record) return 0;
        const statusUpdatedAt = Number(record[`${mediaType}StatusUpdatedAt`] || 0);
        const downloadStartedAt = Number(record[`${mediaType}DownloadStartedAt`] || 0);
        const progressUpdatedAt = record.lastPlayed?.isVideo === (mediaType === 'video')
            ? Number(record.lastPlayed?.updatedAt || 0)
            : 0;
        return Math.max(statusUpdatedAt, downloadStartedAt, progressUpdatedAt);
    },

    _clearMediaFields(record, mediaType) {
        if (mediaType === 'video') {
            record.videoStatus = 'none';
            record.videoResolution = null;
            record.hlsQualityPlaylistUrl = null;
        } else if (mediaType === 'audio') {
            record.audioStatus = 'none';
            record.audioUrl = null;
        }

        record[`${mediaType}StatusUpdatedAt`] = null;
        record[`${mediaType}DownloadStartedAt`] = null;
    },

    _markMediaStatus(record, mediaType, status, resolution = null) {
        const now = Date.now();
        if (mediaType === 'video') {
            record.videoStatus = status;
            if (status === 'downloading') {
                record.videoResolution = resolution;
            } else if (status !== 'completed') {
                record.videoResolution = null;
                record.hlsQualityPlaylistUrl = null;
            }
        } else if (mediaType === 'audio') {
            record.audioStatus = status;
            if (status !== 'completed' && status !== 'downloading') {
                record.audioUrl = null;
            }
        }

        record[`${mediaType}StatusUpdatedAt`] = now;
        record[`${mediaType}DownloadStartedAt`] = status === 'downloading' ? now : null;
    },

    _chooseMediaTypeToKeep(record, preferredMediaType = null) {
        const storedMediaTypes = ['video', 'audio']
            .filter(mediaType => this._isStoredMediaStatus(this._getMediaStatus(record, mediaType)));

        if (storedMediaTypes.length <= 1) {
            return storedMediaTypes[0] || null;
        }

        const statusRank = status => status === 'completed' ? 2 : status === 'downloading' ? 1 : 0;
        storedMediaTypes.sort((left, right) => {
            const statusDiff = statusRank(this._getMediaStatus(record, right)) - statusRank(this._getMediaStatus(record, left));
            if (statusDiff) return statusDiff;

            if (preferredMediaType && left !== right) {
                if (left === preferredMediaType) return -1;
                if (right === preferredMediaType) return 1;
            }

            const updatedDiff = this._getMediaUpdatedAt(record, right) - this._getMediaUpdatedAt(record, left);
            if (updatedDiff) return updatedDiff;

            if (record.lastPlayed?.isVideo === true) return left === 'video' ? -1 : 1;
            if (record.lastPlayed?.isVideo === false) return left === 'audio' ? -1 : 1;

            return left === 'video' ? -1 : 1;
        });

        return storedMediaTypes[0];
    },

    _normalizeRecord(record, preferredMediaType = null) {
        if (!record) return [];

        const storedMediaTypes = ['video', 'audio']
            .filter(mediaType => this._isStoredMediaStatus(this._getMediaStatus(record, mediaType)));

        if (storedMediaTypes.length <= 1) {
            return [];
        }

        const keepMediaType = this._chooseMediaTypeToKeep(record, preferredMediaType);
        const removedMediaTypes = storedMediaTypes.filter(mediaType => mediaType !== keepMediaType);
        removedMediaTypes.forEach(mediaType => this._clearMediaFields(record, mediaType));
        return removedMediaTypes;
    },

    _getMediaCacheName(contentName, mediaType) {
        return `${mediaType}-cache-${contentName}`;
    },

    async cleanupRemovedMedia(removedMedia) {
        if (typeof caches === 'undefined') {
            return;
        }

        const removals = Array.isArray(removedMedia) ? removedMedia : [];
        await Promise.all(removals.map(item => {
            if (!item?.contentName || !item?.mediaType) return null;
            return caches.delete(this._getMediaCacheName(item.contentName, item.mediaType));
        }));
    },

    open() {
        return this._withTimeout(new Promise((resolve, reject) => {
            if (this.db) return resolve(this.db);
            const request = indexedDB.open(this.dbName, this.dbVersion);
            request.onerror = (event) => reject("Error opening DB: " + (event.target.error?.message || event.target.errorCode));
            request.onblocked = () => reject(new Error('IndexedDB open blocked by another tab.'));
            request.onsuccess = (event) => {
                this.db = event.target.result;
                this.db.onversionchange = () => {
                    this.db.close();
                    this.db = null;
                };
                resolve(this.db);
            };
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this.storeName)) {
                    db.createObjectStore(this.storeName, { keyPath: 'contentName' });
                }
                if (!db.objectStoreNames.contains(this.settingsStoreName)) {
                    db.createObjectStore(this.settingsStoreName, { keyPath: 'id' });
                }
            };
        }), 'IndexedDB open');
    },

    _transactionDone(transaction) {
        return new Promise((resolve, reject) => {
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));
        });
    },

    async getMembershipStatus() {
        const db = await this.open();
        const transaction = db.transaction(this.settingsStoreName, 'readonly');
        const store = transaction.objectStore(this.settingsStoreName);
        return this._withTimeout(new Promise((resolve, reject) => {
            const request = store.get('currentUser');
            request.onerror = (event) => reject("Error fetching settings: " + event.target.errorCode);
            request.onsuccess = (event) => resolve(event.target.result);
        }), 'IndexedDB settings read');
    },

    async saveMembershipStatus(status) {
        const db = await this.open();
        const transaction = db.transaction(this.settingsStoreName, 'readwrite');
        const done = this._transactionDone(transaction);
        const store = transaction.objectStore(this.settingsStoreName);
        const settings = { id: 'currentUser', ...status };
        store.put(settings);
        return this._withTimeout(done, 'IndexedDB settings write');
    },

    async clearMembershipStatus() {
        const db = await this.open();
        const transaction = db.transaction(this.settingsStoreName, 'readwrite');
        const done = this._transactionDone(transaction);
        const store = transaction.objectStore(this.settingsStoreName);
        store.delete('currentUser');
        return this._withTimeout(done, 'IndexedDB settings clear');
    },

    async getLesson(contentName) {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readonly');
        const store = transaction.objectStore(this.storeName);
        return this._withTimeout(new Promise((resolve, reject) => {
            const request = store.get(contentName);
            request.onerror = (event) => reject("Error fetching lesson: " + event.target.errorCode);
            request.onsuccess = (event) => resolve(event.target.result);
        }), 'IndexedDB lesson read');
    },

    async startDownload(metadata, mediaType, resolution = null) {
        const db = await this.open();
        return this._withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(metadata.contentName);
            request.onsuccess = (event) => {
                const existingRecord = event.target.result;
                const normalizedExistingRecord = existingRecord ? { ...existingRecord } : null;
                if (normalizedExistingRecord) {
                    this._normalizeRecord(normalizedExistingRecord);
                }

                const otherMediaType = mediaType === 'video' ? 'audio' : 'video';
                const otherStatus = otherMediaType === 'video'
                    ? (normalizedExistingRecord?.videoStatus || 'none')
                    : (normalizedExistingRecord?.audioStatus || 'none');

                if (this._isStoredMediaStatus(otherStatus)) {
                    reject(new Error(`A ${otherMediaType} download already exists for this lesson.`));
                    try {
                        transaction.abort();
                    } catch {
                    }
                    return;
                }

                const newRecord = { ...(normalizedExistingRecord || {}), ...metadata };
                if (!normalizedExistingRecord) {
                    newRecord.videoStatus = 'none';
                    newRecord.audioStatus = 'none';
                    newRecord.lastPlayed = null;
                    newRecord.videoResolution = null;
                }
                this._markMediaStatus(newRecord, mediaType, 'downloading', resolution);
                store.put(newRecord);
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        }), 'IndexedDB start download');
    },

    async updateMediaStatus(contentName, mediaType, status) {
        const db = await this.open();
        return this._withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(contentName);
            request.onsuccess = (event) => {
                const record = event.target.result;
                if (record) {
                    this._markMediaStatus(record, mediaType, status);
                    store.put(record);
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        }), 'IndexedDB media status update');
    },
    async clearMedia(contentName, mediaType) {
        const db = await this.open();
        return this._withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(contentName);
            request.onsuccess = (event) => {
                const record = event.target.result;
                if (record) {
                    this._clearMediaFields(record, mediaType);
                    if (record.videoStatus === 'none' && record.audioStatus === 'none') {
                        store.delete(contentName);
                    } else {
                        store.put(record);
                    }
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        }), 'IndexedDB media clear');
    },
    async normalizeLesson(contentName, preferredMediaType = null) {
        const db = await this.open();
        return this._withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            let result = { record: null, removedMediaTypes: [] };
            transaction.oncomplete = () => resolve(result);
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(contentName);
            request.onsuccess = (event) => {
                const record = event.target.result;
                if (!record) return;

                const normalizedRecord = { ...record };
                const removedMediaTypes = this._normalizeRecord(normalizedRecord, preferredMediaType);
                result = { record: normalizedRecord, removedMediaTypes };

                if (!removedMediaTypes.length) return;

                if (normalizedRecord.videoStatus === 'none' && normalizedRecord.audioStatus === 'none') {
                    store.delete(contentName);
                } else {
                    store.put(normalizedRecord);
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        }), 'IndexedDB media normalize');
    },
    async normalizeAllLessons(preferredMediaType = null) {
        const db = await this.open();
        return this._withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            let result = { records: [], removedMedia: [] };
            transaction.oncomplete = () => resolve(result);
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.getAll();
            request.onsuccess = (event) => {
                const records = Array.isArray(event.target.result) ? event.target.result : [];
                for (const record of records) {
                    const normalizedRecord = { ...record };
                    const removedMediaTypes = this._normalizeRecord(normalizedRecord, preferredMediaType);
                    result.records.push(normalizedRecord);

                    if (!removedMediaTypes.length) continue;

                    removedMediaTypes.forEach(mediaType => {
                        result.removedMedia.push({ contentName: normalizedRecord.contentName, mediaType });
                    });

                    if (normalizedRecord.videoStatus === 'none' && normalizedRecord.audioStatus === 'none') {
                        store.delete(normalizedRecord.contentName);
                    } else {
                        store.put(normalizedRecord);
                    }
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lessons.'));
        }), 'IndexedDB media normalize all');
    },
    async saveTimestamp(contentName, time, isVideo) {
        const db = await this.open();
        return this._withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(contentName);
            request.onsuccess = (event) => {
                const record = event.target.result;
                if (record) {
                    record.lastPlayed = { time, isVideo, updatedAt: Date.now() };
                    store.put(record);
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        }), 'IndexedDB timestamp save');
    },
    async getAllLessons() {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readonly');
        const store = transaction.objectStore(this.storeName);
        return this._withTimeout(new Promise((resolve, reject) => {
            const request = store.getAll();
            request.onerror = (event) => reject("Error fetching all lessons: " + event.target.errorCode);
            request.onsuccess = (event) => resolve(event.target.result);
        }), 'IndexedDB lessons read');
    }
};
const offlineVideoDB = offlineContentDB;
