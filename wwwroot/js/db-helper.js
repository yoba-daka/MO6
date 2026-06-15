// /js/db-helper.js

const offlineContentDB = {
    db: null,
    dbName: 'OfflineContentDB',
    storeName: 'lessons',
    settingsStoreName: 'userSettings', 
    dbVersion: 4, 

    open() {
        return new Promise((resolve, reject) => {
            if (this.db) return resolve(this.db);
            const request = indexedDB.open(this.dbName, this.dbVersion);
            request.onerror = (event) => reject("Error opening DB: " + (event.target.error?.message || event.target.errorCode));
            request.onsuccess = (event) => { this.db = event.target.result; resolve(this.db); };
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this.storeName)) {
                    db.createObjectStore(this.storeName, { keyPath: 'contentName' });
                }
                if (!db.objectStoreNames.contains(this.settingsStoreName)) {
                    db.createObjectStore(this.settingsStoreName, { keyPath: 'id' });
                }
            };
        });
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
        return new Promise((resolve, reject) => {
            const request = store.get('currentUser');
            request.onerror = (event) => reject("Error fetching settings: " + event.target.errorCode);
            request.onsuccess = (event) => resolve(event.target.result);
        });
    },

    async saveMembershipStatus(status) {
        const db = await this.open();
        const transaction = db.transaction(this.settingsStoreName, 'readwrite');
        const done = this._transactionDone(transaction);
        const store = transaction.objectStore(this.settingsStoreName);
        const settings = { id: 'currentUser', ...status };
        store.put(settings);
        return done;
    },

    async clearMembershipStatus() {
        const db = await this.open();
        const transaction = db.transaction(this.settingsStoreName, 'readwrite');
        const done = this._transactionDone(transaction);
        const store = transaction.objectStore(this.settingsStoreName);
        store.delete('currentUser');
        return done;
    },

    async getLesson(contentName) {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readonly');
        const store = transaction.objectStore(this.storeName);
        return new Promise((resolve, reject) => {
            const request = store.get(contentName);
            request.onerror = (event) => reject("Error fetching lesson: " + event.target.errorCode);
            request.onsuccess = (event) => resolve(event.target.result);
        });
    },

    async startDownload(metadata, mediaType, resolution = null) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(metadata.contentName);
            request.onsuccess = (event) => {
                const existingRecord = event.target.result;
                const newRecord = { ...(existingRecord || {}), ...metadata };
                if (!existingRecord) {
                    newRecord.videoStatus = 'none';
                    newRecord.audioStatus = 'none';
                    newRecord.lastPlayed = null;
                    newRecord.videoResolution = null;
                }
                if (mediaType === 'video') {
                    newRecord.videoStatus = 'downloading';
                    newRecord.videoResolution = resolution;
                } else if (mediaType === 'audio') {
                    newRecord.audioStatus = 'downloading';
                }
                store.put(newRecord);
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        });
    },

    async updateMediaStatus(contentName, mediaType, status) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(contentName);
            request.onsuccess = (event) => {
                const record = event.target.result;
                if (record) {
                    if (mediaType === 'video') {
                        record.videoStatus = status;
                        if (status !== 'completed') {
                            record.videoResolution = null;
                        }
                    } else if (mediaType === 'audio') {
                        record.audioStatus = status;
                    }
                    store.put(record);
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        });
    },
    async clearMedia(contentName, mediaType) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction(this.storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error('IndexedDB transaction failed.'));
            transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));

            const store = transaction.objectStore(this.storeName);
            const request = store.get(contentName);
            request.onsuccess = (event) => {
                const record = event.target.result;
                if (record) {
                    if (mediaType === 'video') {
                        record.videoStatus = 'none';
                        record.videoResolution = null;
                    } else if (mediaType === 'audio') {
                        record.audioStatus = 'none';
                    }
                    if (record.videoStatus === 'none' && record.audioStatus === 'none') {
                        store.delete(contentName);
                    } else {
                        store.put(record);
                    }
                }
            };
            request.onerror = () => reject(request.error || new Error('Error fetching lesson.'));
        });
    },
    async saveTimestamp(contentName, time, isVideo) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
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
        });
    },
    async getAllLessons() {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readonly');
        const store = transaction.objectStore(this.storeName);
        return new Promise((resolve, reject) => {
            const request = store.getAll();
            request.onerror = (event) => reject("Error fetching all lessons: " + event.target.errorCode);
            request.onsuccess = (event) => resolve(event.target.result);
        });
    }
};
const offlineVideoDB = offlineContentDB;
