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
        const store = transaction.objectStore(this.settingsStoreName);
        const settings = { id: 'currentUser', ...status };
        store.put(settings);
        return transaction.complete;
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
        const transaction = db.transaction(this.storeName, 'readwrite');
        const store = transaction.objectStore(this.storeName);
        const existingRecord = await new Promise(resolve => store.get(metadata.contentName).onsuccess = e => resolve(e.target.result));
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
        return transaction.complete;
    },

    async updateMediaStatus(contentName, mediaType, status) {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readwrite');
        const store = transaction.objectStore(this.storeName);
        const record = await new Promise(resolve => store.get(contentName).onsuccess = e => resolve(e.target.result));
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
        return transaction.complete;
    },
    async clearMedia(contentName, mediaType) {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readwrite');
        const store = transaction.objectStore(this.storeName);
        const record = await new Promise(resolve => store.get(contentName).onsuccess = e => resolve(e.target.result));

        if (record) {
            record.lastPlayed = null;
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
        return transaction.complete;
    },
    async saveTimestamp(contentName, time, isVideo) {
        const db = await this.open();
        const transaction = db.transaction(this.storeName, 'readwrite');
        const store = transaction.objectStore(this.storeName);
        const record = await new Promise(resolve => store.get(contentName).onsuccess = e => resolve(e.target.result));
        if (record) {
            record.lastPlayed = { time, isVideo };
            store.put(record);
        }
        return transaction.complete;
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