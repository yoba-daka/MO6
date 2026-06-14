(function () {
    "use strict";

    const app = document.getElementById("membershipsApp");
    if (!app || typeof window.jQuery === "undefined") {
        return;
    }

    const $ = window.jQuery;

    const urls = {
        load: app.getAttribute("data-load-url") || "",
        downloadFiltered: app.getAttribute("data-download-filtered-url") || "",
        downloadSelected: app.getAttribute("data-download-selected-url") || "",
        updateMembership: app.getAttribute("data-update-membership-url") || "",
        quickAction: app.getAttribute("data-quick-action-url") || "",
        cancelDd: app.getAttribute("data-cancel-dd-url") || "",
        quickView: app.getAttribute("data-member-quick-view-url") || "",
        history: app.getAttribute("data-member-history-url") || "",
        create: app.getAttribute("data-create-url") || ""
    };

    const defaults = {
        page: toInt(app.getAttribute("data-current-page"), 1),
        totalPages: toInt(app.getAttribute("data-total-pages"), 1),
        search: app.getAttribute("data-search") || "",
        statusFilter: app.getAttribute("data-status-filter") || "all",
        typeFilter: app.getAttribute("data-type-filter") || "all",
        advancedFilter: app.getAttribute("data-advanced-filter") || "all",
        sortBy: app.getAttribute("data-sort-by") || "Id",
        sortOrder: app.getAttribute("data-sort-order") || "desc"
    };

    const currentState = {
        page: defaults.page,
        totalPages: defaults.totalPages,
        search: defaults.search,
        statusFilter: defaults.statusFilter,
        typeFilter: defaults.typeFilter,
        advancedFilter: defaults.advancedFilter,
        sortBy: defaults.sortBy,
        sortOrder: defaults.sortOrder
    };

    const selectedIds = new Set();
    let activeDrawerMembershipId = null;
    let lastFocusedElement = null;

    const dom = {
        tableContainer: document.getElementById("tableContainer"),
        paginationContainer: document.getElementById("paginationContainer"),
        searchInput: document.getElementById("searchInput"),
        statusFilter: document.getElementById("statusFilter"),
        typeFilter: document.getElementById("typeFilter"),
        advancedFilter: document.getElementById("advancedFilter"),
        kpiCards: document.querySelectorAll(".kpi-card"),
        kpiTotal: document.getElementById("kpiTotal"),
        kpiExpired: document.getElementById("kpiExpired"),
        kpiMonthlyActive: document.getElementById("kpiMonthlyActive"),
        kpiAnnualActive: document.getElementById("kpiAnnualActive"),
        statusBanner: document.getElementById("membershipStatus"),
        resultsSummary: document.getElementById("resultsSummary"),
        selectedCounter: document.getElementById("selectedCounter"),
        downloadFilteredBtn: document.getElementById("downloadFilteredBtn"),
        downloadSelectedBtn: document.getElementById("downloadSelectedBtn"),
        createButton: document.getElementById("createButton"),
        createEmail: document.getElementById("createEmail"),
        createExpiration: document.getElementById("createExpiration"),
        drawer: document.getElementById("memberDrawer"),
        drawerOverlay: document.getElementById("drawerOverlay"),
        drawerCloseBtn: document.getElementById("drawerCloseBtn"),
        drawerSummary: document.getElementById("drawerSummary"),
        drawerHistory: document.getElementById("drawerHistory"),
        drawerTitle: document.getElementById("drawerTitle"),
        drawerTabs: document.querySelectorAll(".drawer-tab")
    };

    function toInt(raw, fallback) {
        const parsed = Number.parseInt(raw, 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function escapeHtml(value) {
        return (value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function extractError(xhr) {
        if (!xhr) {
            return "אירעה שגיאה לא צפויה.";
        }

        if (xhr.responseJSON && xhr.responseJSON.message) {
            return xhr.responseJSON.message;
        }

        if (typeof xhr.responseText === "string" && xhr.responseText.trim().length > 0) {
            return xhr.responseText;
        }

        return "אירעה שגיאה בביצוע הפעולה.";
    }

    function showStatus(message, type) {
        if (!dom.statusBanner) {
            return;
        }

        const normalizedType = type || "info";
        dom.statusBanner.className = "status-banner show " + normalizedType;
        dom.statusBanner.textContent = message;

        if (normalizedType === "success" || normalizedType === "info") {
            window.setTimeout(function () {
                dom.statusBanner.className = "status-banner";
                dom.statusBanner.textContent = "";
            }, 4200);
        }
    }

    function debounce(fn, waitMs) {
        let timer = null;
        return function () {
            const args = arguments;
            window.clearTimeout(timer);
            timer = window.setTimeout(function () {
                fn.apply(null, args);
            }, waitMs);
        };
    }

    function localDisplayToIso(displayValue) {
        const text = (displayValue || "").trim();
        const match = text.match(/^(\d{4})-(\d{2})-(\d{2})\s(\d{2}):(\d{2})$/);
        if (!match) {
            return null;
        }

        return match[1] + "-" + match[2] + "-" + match[3] + "T" + match[4] + ":" + match[5] + ":00";
    }

    function isUmbracoHashRoute() {
        const path = (window.location.pathname || "").toLowerCase();
        const hash = window.location.hash || "";
        return path.startsWith("/umbraco") && hash.startsWith("#/");
    }

    function getRouteQueryParams() {
        if (isUmbracoHashRoute()) {
            const hash = window.location.hash || "";
            const queryIndex = hash.indexOf("?");
            if (queryIndex === -1) {
                return new URLSearchParams();
            }

            return new URLSearchParams(hash.substring(queryIndex + 1));
        }

        return new URLSearchParams(window.location.search);
    }

    function parseStateFromUrl() {
        const params = getRouteQueryParams();
        if (!params.toString()) {
            return;
        }

        currentState.page = toInt(params.get("page"), currentState.page);
        currentState.search = params.get("search") ?? currentState.search;
        currentState.statusFilter = params.get("statusFilter") ?? currentState.statusFilter;
        currentState.typeFilter = params.get("typeFilter") ?? currentState.typeFilter;
        currentState.advancedFilter = params.get("advancedFilter") ?? currentState.advancedFilter;
        currentState.sortBy = params.get("sortBy") ?? currentState.sortBy;
        currentState.sortOrder = params.get("sortOrder") ?? currentState.sortOrder;
    }

    function pushStateToUrl() {
        if (isUmbracoHashRoute()) {
            return;
        }

        const params = new URLSearchParams();

        params.set("page", String(currentState.page));
        params.set("sortBy", currentState.sortBy);
        params.set("sortOrder", currentState.sortOrder);

        if (currentState.search) {
            params.set("search", currentState.search);
        }
        if (currentState.statusFilter && currentState.statusFilter !== "all") {
            params.set("statusFilter", currentState.statusFilter);
        }
        if (currentState.typeFilter && currentState.typeFilter !== "all") {
            params.set("typeFilter", currentState.typeFilter);
        }
        if (currentState.advancedFilter && currentState.advancedFilter !== "all") {
            params.set("advancedFilter", currentState.advancedFilter);
        }

        const nextUrl = window.location.pathname + "?" + params.toString();
        window.history.replaceState({}, "", nextUrl);
    }

    function syncControlsFromState() {
        if (dom.searchInput) {
            dom.searchInput.value = currentState.search;
        }
        if (dom.statusFilter) {
            dom.statusFilter.value = currentState.statusFilter;
        }
        if (dom.typeFilter) {
            dom.typeFilter.value = currentState.typeFilter;
        }
        if (dom.advancedFilter) {
            dom.advancedFilter.value = currentState.advancedFilter;
        }
    }

    function syncStateFromControls() {
        currentState.search = (dom.searchInput?.value || "").trim();
        currentState.statusFilter = dom.statusFilter?.value || "all";
        currentState.typeFilter = dom.typeFilter?.value || "all";
        currentState.advancedFilter = dom.advancedFilter?.value || "all";
    }

    function renderPagination() {
        if (!dom.paginationContainer) {
            return;
        }

        const total = Math.max(1, currentState.totalPages);
        const page = Math.min(Math.max(1, currentState.page), total);
        const maxLinks = 9;
        const half = Math.floor(maxLinks / 2);
        let start = Math.max(1, page - half);
        let end = Math.min(total, start + maxLinks - 1);
        if (end - start + 1 < maxLinks) {
            start = Math.max(1, end - maxLinks + 1);
        }

        let html = "";
        if (page > 1) {
            html += '<li><a href="#" data-page="' + (page - 1) + '">‹</a></li>';
        }

        for (let i = start; i <= end; i += 1) {
            const active = i === page ? " class=\"active\"" : "";
            html += "<li" + active + "><a href=\"#\" data-page=\"" + i + "\">" + i + "</a></li>";
        }

        if (page < total) {
            html += '<li><a href="#" data-page="' + (page + 1) + '">›</a></li>';
        }

        dom.paginationContainer.innerHTML = html;
    }

    function updateSortIndicators() {
        const headers = dom.tableContainer.querySelectorAll(".sortable-header");
        headers.forEach(function (header) {
            header.classList.remove("asc", "desc");
            header.setAttribute("aria-sort", "none");
            if (header.getAttribute("data-sortby") === currentState.sortBy) {
                header.classList.add(currentState.sortOrder);
                header.setAttribute("aria-sort", currentState.sortOrder === "asc" ? "ascending" : "descending");
            }
        });
    }

    function setLoading(isLoading) {
        if (!dom.tableContainer) {
            return;
        }

        dom.tableContainer.style.opacity = isLoading ? "0.45" : "1";
        dom.tableContainer.style.pointerEvents = isLoading ? "none" : "auto";
    }

    function updateResultsSummary() {
        const dataElement = document.getElementById("pagination-data");
        const totalItems = toInt(dataElement?.getAttribute("data-total-items"), 0);
        if (dom.resultsSummary) {
            dom.resultsSummary.textContent = "תוצאות: " + totalItems;
        }
    }

    function updateKpisFromData() {
        const dataElement = document.getElementById("pagination-data");
        if (!dataElement) {
            return;
        }

        if (dom.kpiTotal) {
            dom.kpiTotal.textContent = String(toInt(dataElement.getAttribute("data-kpi-total"), 0));
        }
        if (dom.kpiExpired) {
            dom.kpiExpired.textContent = String(toInt(dataElement.getAttribute("data-kpi-expired"), 0));
        }
        if (dom.kpiMonthlyActive) {
            dom.kpiMonthlyActive.textContent = String(toInt(dataElement.getAttribute("data-kpi-monthly-active"), 0));
        }
        if (dom.kpiAnnualActive) {
            dom.kpiAnnualActive.textContent = String(toInt(dataElement.getAttribute("data-kpi-annual-active"), 0));
        }
    }

    function updateActiveKpiCard() {
        if (!dom.kpiCards || dom.kpiCards.length === 0) {
            return;
        }

        dom.kpiCards.forEach(function (card) {
            const filter = card.getAttribute("data-kpi-filter") || "all";
            const isActive = (filter === "all" && currentState.advancedFilter === "all")
                || (filter !== "all" && filter === currentState.advancedFilter);
            card.classList.toggle("active", isActive);
            card.setAttribute("aria-pressed", isActive ? "true" : "false");
        });
    }

    function updateSelectedCounter() {
        if (dom.selectedCounter) {
            dom.selectedCounter.textContent = "נבחרו: " + selectedIds.size;
        }
        if (dom.downloadSelectedBtn) {
            dom.downloadSelectedBtn.disabled = selectedIds.size === 0;
        }
    }

    function applySelectionToPageRows() {
        const checkboxes = dom.tableContainer.querySelectorAll(".row-select");
        checkboxes.forEach(function (checkbox) {
            const id = toInt(checkbox.getAttribute("data-id"), 0);
            checkbox.checked = selectedIds.has(id);
        });
    }

    function updateHeaderSelectState() {
        const pageRows = dom.tableContainer.querySelectorAll(".row-select");
        const selectAll = document.getElementById("selectAllRows");
        if (!selectAll) {
            return;
        }

        if (pageRows.length === 0) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
            return;
        }

        let checkedCount = 0;
        pageRows.forEach(function (checkbox) {
            if (checkbox.checked) {
                checkedCount += 1;
            }
        });

        selectAll.checked = checkedCount === pageRows.length;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < pageRows.length;
    }

    function initializeDatePickers() {
        if (typeof window.flatpickr !== "function") {
            return;
        }

        document.querySelectorAll(".expiration-edit").forEach(function (input) {
            if (input._flatpickr) {
                input._flatpickr.destroy();
            }

            window.flatpickr(input, {
                dateFormat: "Y-m-d H:i",
                enableTime: true,
                time_24hr: true,
                allowInput: true
            });
        });

        if (dom.createExpiration) {
            if (dom.createExpiration._flatpickr) {
                dom.createExpiration._flatpickr.destroy();
            }

            window.flatpickr(dom.createExpiration, {
                dateFormat: "Y-m-d H:i",
                enableTime: true,
                time_24hr: true,
                allowInput: true
            });
        }
    }

    function setRowStatus(row, key, label) {
        const statusBadge = row.find(".status-badge");
        const currentClasses = (statusBadge.attr("class") || "")
            .split(" ")
            .filter(function (value) { return value && !value.startsWith("status-"); });
        currentClasses.push("status-badge", "status-" + key);
        statusBadge.attr("class", currentClasses.join(" "));
        statusBadge.text(label);
    }

    function setRowBusy(row, isBusy) {
        row.find("button, .phone-edit, .expiration-edit").prop("disabled", isBusy);
    }

    function getOriginalPhone(row) {
        return (row.attr("data-original-phone") || "").trim();
    }

    function getOriginalExpiration(row) {
        return (row.attr("data-original-expiration") || "").trim();
    }

    function setOriginalRowValues(row, phoneValue, expirationValue) {
        row.attr("data-original-phone", phoneValue || "");
        row.attr("data-original-expiration", expirationValue || "");
    }

    function revertInlineField(row, fieldName) {
        if (fieldName === "phone") {
            row.find(".phone-edit").val(getOriginalPhone(row));
            return;
        }

        row.find(".expiration-edit").val(getOriginalExpiration(row));
    }

    function confirmInlineFieldUpdate(row, fieldName) {
        const name = row.attr("data-name") || "מנוי";
        const currentPhone = (row.find(".phone-edit").val() || "").trim();
        const currentExpiration = (row.find(".expiration-edit").val() || "").trim();
        const originalPhone = getOriginalPhone(row);
        const originalExpiration = getOriginalExpiration(row);

        if (fieldName === "phone" && currentPhone === originalPhone) {
            return;
        }

        if (fieldName === "expiration" && currentExpiration === originalExpiration) {
            return;
        }

        const message = fieldName === "phone"
            ? "לעדכן את מספר הטלפון עבור " + name + "?"
            : "לעדכן את תום המנוי עבור " + name + "?";

        if (!window.confirm(message)) {
            revertInlineField(row, fieldName);
            return;
        }

        saveRow(row);
    }

    function confirmQuickAction(row, action) {
        const name = row.attr("data-name") || "מנוי";
        const normalized = (action || "").trim().toLowerCase();

        const message = normalized === "plusmonth"
            ? "להוסיף חודש למנוי " + name + "?"
            : normalized === "plusyear"
                ? "להוסיף שנה למנוי " + name + "?"
                : normalized === "today"
                    ? "לקבוע תום מנוי להיום עבור " + name + "?"
                    : normalized === "activatemonthly"
                        ? "לסמן מנוי חודשי כפעיל עבור " + name + "?"
                        : normalized === "deactivatemonthly"
                            ? "לבטל חיוב חודשי עבור " + name + "?"
                            : "לבצע את הפעולה עבור " + name + "?";

        return window.confirm(message);
    }

    function updateRowFromServer(row, payload) {
        const phoneValue = payload.phone || "";
        row.find(".phone-edit").val(phoneValue);

        const expirationValue = payload.expiration || "";
        row.find(".expiration-edit")
            .attr("data-iso", payload.expirationIso || "")
            .val(expirationValue);
        setOriginalRowValues(row, phoneValue, expirationValue);

        if (typeof payload.isMonthly === "boolean") {
            row.find(".type-badge")
                .removeClass("monthly annual")
                .addClass(payload.isMonthly ? "monthly" : "annual")
                .text(payload.isMonthly ? "חודשי" : "שנתי");
        }

        if (typeof payload.isMonthlyActive === "boolean") {
            row.find(".dd-badge")
                .removeClass("active inactive")
                .addClass(payload.isMonthlyActive ? "active" : "inactive")
                .text(payload.isMonthlyActive ? "כן" : "לא");
        }

        if (payload.statusKey && payload.statusLabel) {
            setRowStatus(row, payload.statusKey, payload.statusLabel);
        }
    }

    function loadMemberships() {
        setLoading(true);

        $.ajax({
            url: urls.load,
            type: "GET",
            data: {
                page: currentState.page,
                search: currentState.search,
                statusFilter: currentState.statusFilter,
                typeFilter: currentState.typeFilter,
                advancedFilter: currentState.advancedFilter,
                sortBy: currentState.sortBy,
                sortOrder: currentState.sortOrder
            }
        })
            .done(function (response) {
                dom.tableContainer.innerHTML = response;
                const pagerData = document.getElementById("pagination-data");
                currentState.totalPages = toInt(pagerData?.getAttribute("data-total-pages"), 1);
                currentState.page = Math.min(Math.max(1, currentState.page), Math.max(1, currentState.totalPages));

                initializeDatePickers();
                renderPagination();
                updateSortIndicators();
                updateResultsSummary();
                updateKpisFromData();
                updateActiveKpiCard();
                applySelectionToPageRows();
                updateHeaderSelectState();
                updateSelectedCounter();
                pushStateToUrl();
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
            })
            .always(function () {
                setLoading(false);
            });
    }

    function exportFiltered() {
        syncStateFromControls();
        const params = new URLSearchParams({
            search: currentState.search,
            statusFilter: currentState.statusFilter,
            typeFilter: currentState.typeFilter,
            advancedFilter: currentState.advancedFilter,
            sortBy: currentState.sortBy,
            sortOrder: currentState.sortOrder
        });
        window.location.href = urls.downloadFiltered + "?" + params.toString();
    }

    function exportSelected() {
        if (selectedIds.size === 0) {
            showStatus("לא נבחרו שורות לייצוא.", "info");
            return;
        }

        const ids = Array.from(selectedIds).join(",");
        window.location.href = urls.downloadSelected + "?ids=" + encodeURIComponent(ids);
    }

    function createMembership() {
        const email = (dom.createEmail?.value || "").trim();
        const expirationText = (dom.createExpiration?.value || "").trim();
        const expirationIso = localDisplayToIso(expirationText);

        if (!email || !expirationIso) {
            showStatus("יש למלא אימייל ותוקף בפורמט תקין.", "error");
            return;
        }

        dom.createButton.disabled = true;
        $.ajax({
            url: urls.create,
            type: "POST",
            data: { email: email, expiration: expirationIso }
        })
            .done(function () {
                showStatus("המנוי נוסף בהצלחה.", "success");
                dom.createEmail.value = "";
                dom.createExpiration.value = "";
                currentState.page = 1;
                loadMemberships();
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
            })
            .always(function () {
                dom.createButton.disabled = false;
            });
    }

    function saveRow(row) {
        const id = toInt(row.attr("data-id"), 0);
        const phone = (row.find(".phone-edit").val() || "").trim();
        const expirationText = (row.find(".expiration-edit").val() || "").trim();
        const expirationIso = localDisplayToIso(expirationText);

        if (!id || !expirationIso) {
            showStatus("פורמט התאריך בשורה אינו תקין.", "error");
            row.find(".expiration-edit").val(getOriginalExpiration(row));
            return;
        }

        setRowBusy(row, true);

        $.ajax({
            url: urls.updateMembership,
            type: "POST",
            data: {
                id: id,
                phone: phone,
                expirationIso: expirationIso
            }
        })
            .done(function (payload) {
                updateRowFromServer(row, payload);
                showStatus("השינויים נשמרו.", "success");
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
                row.find(".phone-edit").val(getOriginalPhone(row));
                row.find(".expiration-edit").val(getOriginalExpiration(row));
            })
            .always(function () {
                setRowBusy(row, false);
            });
    }

    function applyQuickAction(row, action) {
        const id = toInt(row.attr("data-id"), 0);
        if (!id) {
            return;
        }

        if (!confirmQuickAction(row, action)) {
            return;
        }

        const normalizedAction = (action || "").trim().toLowerCase();
        setRowBusy(row, true);
        $.ajax({
            url: urls.quickAction,
            type: "POST",
            data: { id: id, action: action }
        })
            .done(function (payload) {
                updateRowFromServer(row, payload);
                showStatus("הפעולה בוצעה בהצלחה.", "success");
                if (normalizedAction === "activatemonthly" || normalizedAction === "deactivatemonthly") {
                    loadMemberships();
                }
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
            })
            .always(function () {
                setRowBusy(row, false);
            });
    }

    function cancelDirectDebit(row) {
        const id = toInt(row.attr("data-id"), 0);
        if (!id) {
            return;
        }

        const name = row.attr("data-name") || "מנוי";
        if (!window.confirm("לבטל הוראת קבע עבור " + name + "?")) {
            return;
        }

        setRowBusy(row, true);
        $.ajax({
            url: urls.cancelDd,
            type: "POST",
            data: { id: id }
        })
            .done(function () {
                showStatus("הוראת הקבע בוטלה.", "success");
                loadMemberships();
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
            })
            .always(function () {
                setRowBusy(row, false);
            });
    }

    function openDrawer() {
        if (!dom.drawer || !dom.drawerOverlay) {
            return;
        }
        lastFocusedElement = document.activeElement;
        dom.drawer.classList.add("open");
        dom.drawer.setAttribute("aria-hidden", "false");
        dom.drawerOverlay.classList.add("show");
        dom.drawerOverlay.setAttribute("aria-hidden", "false");
        window.setTimeout(function () {
            if (dom.drawerCloseBtn) {
                dom.drawerCloseBtn.focus();
            }
        }, 0);
    }

    function closeDrawer() {
        if (!dom.drawer || !dom.drawerOverlay) {
            return;
        }
        dom.drawer.classList.remove("open");
        dom.drawer.setAttribute("aria-hidden", "true");
        dom.drawerOverlay.classList.remove("show");
        dom.drawerOverlay.setAttribute("aria-hidden", "true");
        if (lastFocusedElement && typeof lastFocusedElement.focus === "function") {
            lastFocusedElement.focus();
        }
    }

    function setDrawerTab(tabName) {
        dom.drawerTabs.forEach(function (tab) {
            const isCurrent = tab.getAttribute("data-tab") === tabName;
            tab.classList.toggle("active", isCurrent);
            tab.setAttribute("aria-selected", isCurrent ? "true" : "false");
            tab.setAttribute("tabindex", isCurrent ? "0" : "-1");
        });

        if (dom.drawerSummary) {
            dom.drawerSummary.classList.toggle("hidden", tabName !== "summary");
            dom.drawerSummary.setAttribute("aria-hidden", tabName === "summary" ? "false" : "true");
        }
        if (dom.drawerHistory) {
            dom.drawerHistory.classList.toggle("hidden", tabName !== "history");
            dom.drawerHistory.setAttribute("aria-hidden", tabName === "history" ? "false" : "true");
        }
    }

    function renderQuickView(payload) {
        if (!dom.drawerSummary) {
            return;
        }

        const recentActions = (payload.recentActions || [])
            .map(function (item) { return "<li>" + escapeHtml(item) + "</li>"; })
            .join("");

        const success = payload.lastSuccess
            ? "<div class=\"drawer-item\"><span class=\"drawer-label\">תשלום אחרון שעבר</span>"
                + "<div>" + escapeHtml(payload.lastSuccess.created) + " | " + escapeHtml(String(payload.lastSuccess.sum ?? "")) + "</div>"
                + "<div>אסמכתא: " + escapeHtml(payload.lastSuccess.asmachta || "") + "</div>"
                + "</div>"
            : "<div class=\"drawer-item\"><span class=\"drawer-label\">תשלום אחרון שעבר</span><div>לא נמצא</div></div>";

        const fail = payload.lastFailure
            ? "<div class=\"drawer-item\"><span class=\"drawer-label\">תשלום אחרון שנכשל</span>"
                + "<div>" + escapeHtml(payload.lastFailure.created) + " | " + escapeHtml(String(payload.lastFailure.sum ?? "")) + "</div>"
                + "<div>סטטוס: " + escapeHtml(payload.lastFailure.status || "") + "</div>"
                + "</div>"
            : "<div class=\"drawer-item\"><span class=\"drawer-label\">תשלום אחרון שנכשל</span><div>לא נמצא</div></div>";

        dom.drawerSummary.innerHTML =
            "<div class=\"drawer-grid\">"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">שם</span><div>" + escapeHtml(payload.name) + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">אימייל</span><div>" + escapeHtml(payload.email) + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">טלפון</span><div>" + escapeHtml(payload.phone) + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">סטטוס</span><div>" + escapeHtml(payload.statusLabel) + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">תוקף</span><div>" + escapeHtml(payload.expiration) + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">סוג מנוי</span><div>" + (payload.isMonthly ? "חודשי" : "שנתי") + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">חיוב חודשי פעיל</span><div>" + (payload.isMonthlyActive ? "כן" : "לא") + "</div></div>"
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">Direct Debit</span><div>" + escapeHtml(String(payload.directDebitId ?? "לא נמצא")) + "</div></div>"
            + success
            + fail
            + "<div class=\"drawer-item\"><span class=\"drawer-label\">פעולות אחרונות</span><ul>" + recentActions + "</ul></div>"
            + "</div>";
    }

    function renderHistory(payload) {
        if (!dom.drawerHistory) {
            return;
        }

        const rows = (payload.items || []).map(function (item) {
            const tokenValue = item.transactionToken || "";
            const txIdValue = item.transactionId == null ? "" : String(item.transactionId);
            return "<div class=\"history-row " + (item.isSuccess ? "success" : "fail") + "\">"
                + "<div><strong>" + escapeHtml(item.created) + "</strong></div>"
                + "<div>סכום: " + escapeHtml(String(item.sum ?? "")) + " | סטטוס: " + escapeHtml(item.status || "") + "</div>"
                + "<div>TransactionId: " + escapeHtml(String(item.transactionId ?? "")) + "</div>"
                + "<div>Token: " + escapeHtml(item.transactionToken || "") + "</div>"
                + "<div>DirectDebitId: " + escapeHtml(String(item.directDebitId ?? "")) + "</div>"
                + "<div>אסמכתא: " + escapeHtml(item.asmachta || "") + "</div>"
                + "<div class=\"history-actions\">"
                + "<button type=\"button\" class=\"btn-inline copy-btn\" data-copy=\"" + escapeHtml(tokenValue) + "\">העתק Token</button>"
                + "<button type=\"button\" class=\"btn-inline copy-btn\" data-copy=\"" + escapeHtml(txIdValue) + "\">העתק TxId</button>"
                + "</div>"
                + "</div>";
        }).join("");

        dom.drawerHistory.innerHTML = rows || "<div class=\"drawer-item\">לא נמצאו עסקאות עבור מנוי זה.</div>";
    }

    function loadQuickView(membershipId, title) {
        activeDrawerMembershipId = membershipId;
        if (dom.drawerTitle) {
            dom.drawerTitle.textContent = "פרטי מנוי - " + (title || membershipId);
        }
        openDrawer();
        setDrawerTab("summary");
        dom.drawerSummary.innerHTML = "<div class=\"drawer-item\">טוען פרטים...</div>";
        dom.drawerHistory.innerHTML = "<div class=\"drawer-item\">בחר לשונית היסטוריה לטעינת עסקאות.</div>";

        $.ajax({
            url: urls.quickView,
            type: "GET",
            data: { id: membershipId }
        })
            .done(function (payload) {
                renderQuickView(payload);
            })
            .fail(function (xhr) {
                dom.drawerSummary.innerHTML = "<div class=\"drawer-item\">" + escapeHtml(extractError(xhr)) + "</div>";
            });
    }

    function loadHistory(membershipId) {
        openDrawer();
        setDrawerTab("history");
        dom.drawerHistory.innerHTML = "<div class=\"drawer-item\">טוען עסקאות...</div>";

        $.ajax({
            url: urls.history,
            type: "GET",
            data: { id: membershipId }
        })
            .done(function (payload) {
                renderHistory(payload);
            })
            .fail(function (xhr) {
                dom.drawerHistory.innerHTML = "<div class=\"drawer-item\">" + escapeHtml(extractError(xhr)) + "</div>";
            });
    }

    function bindEvents() {
        const debouncedSearch = debounce(function () {
            syncStateFromControls();
            currentState.page = 1;
            loadMemberships();
        }, 350);

        if (dom.searchInput) {
            dom.searchInput.addEventListener("input", debouncedSearch);
            dom.searchInput.addEventListener("keydown", function (event) {
                if (event.key === "Enter") {
                    event.preventDefault();
                    syncStateFromControls();
                    currentState.page = 1;
                    loadMemberships();
                }
            });
        }

        if (dom.kpiCards && dom.kpiCards.length > 0) {
            dom.kpiCards.forEach(function (card) {
                card.addEventListener("click", function () {
                    const filter = card.getAttribute("data-kpi-filter") || "all";
                    currentState.advancedFilter = filter;
                    currentState.page = 1;
                    if (dom.advancedFilter) {
                        dom.advancedFilter.value = filter;
                    }
                    loadMemberships();
                });
            });
        }

        [dom.statusFilter, dom.typeFilter, dom.advancedFilter].forEach(function (element) {
            if (!element) {
                return;
            }
            element.addEventListener("change", function () {
                syncStateFromControls();
                currentState.page = 1;
                loadMemberships();
            });
        });

        if (dom.downloadFilteredBtn) {
            dom.downloadFilteredBtn.addEventListener("click", exportFiltered);
        }

        if (dom.downloadSelectedBtn) {
            dom.downloadSelectedBtn.addEventListener("click", exportSelected);
        }

        if (dom.createButton) {
            dom.createButton.addEventListener("click", createMembership);
        }

        if (dom.createEmail) {
            dom.createEmail.addEventListener("keydown", function (event) {
                if (event.key === "Enter") {
                    event.preventDefault();
                    createMembership();
                }
            });
        }

        if (dom.createExpiration) {
            dom.createExpiration.addEventListener("keydown", function (event) {
                if (event.key === "Enter") {
                    event.preventDefault();
                    createMembership();
                }
            });
        }

        if (dom.drawerOverlay) {
            dom.drawerOverlay.addEventListener("click", closeDrawer);
        }

        if (dom.drawerCloseBtn) {
            dom.drawerCloseBtn.addEventListener("click", closeDrawer);
        }

        dom.drawerTabs.forEach(function (tab) {
            tab.addEventListener("click", function () {
                const targetTab = tab.getAttribute("data-tab");
                setDrawerTab(targetTab);
                if (targetTab === "history" && activeDrawerMembershipId) {
                    loadHistory(activeDrawerMembershipId);
                }
            });
        });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && dom.drawer && dom.drawer.classList.contains("open")) {
                closeDrawer();
            }
        });

        $(document).on("click", "#paginationContainer a", function (event) {
            event.preventDefault();
            const page = toInt(this.getAttribute("data-page"), 1);
            if (page === currentState.page) {
                return;
            }
            currentState.page = page;
            loadMemberships();
        });

        $(document).on("click", "#tableContainer .sortable-header", function () {
            const sortBy = this.getAttribute("data-sortby");
            if (!sortBy) {
                return;
            }

            if (currentState.sortBy === sortBy) {
                currentState.sortOrder = currentState.sortOrder === "asc" ? "desc" : "asc";
            } else {
                currentState.sortBy = sortBy;
                currentState.sortOrder = "asc";
            }

            currentState.page = 1;
            loadMemberships();
        });

        $(document).on("keydown", "#tableContainer .sortable-header", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                $(this).trigger("click");
            }
        });

        $(document).on("change", "#tableContainer .row-select", function () {
            const id = toInt(this.getAttribute("data-id"), 0);
            if (!id) {
                return;
            }

            if (this.checked) {
                selectedIds.add(id);
            } else {
                selectedIds.delete(id);
            }

            updateSelectedCounter();
            updateHeaderSelectState();
        });

        $(document).on("change", "#tableContainer #selectAllRows", function () {
            const checked = Boolean(this.checked);
            dom.tableContainer.querySelectorAll(".row-select").forEach(function (checkbox) {
                checkbox.checked = checked;
                const id = toInt(checkbox.getAttribute("data-id"), 0);
                if (!id) {
                    return;
                }
                if (checked) {
                    selectedIds.add(id);
                } else {
                    selectedIds.delete(id);
                }
            });

            updateSelectedCounter();
            updateHeaderSelectState();
        });

        $(document).on("click", "#tableContainer .membership-row", function (event) {
            if ($(event.target).closest("button,input,a,label").length > 0) {
                return;
            }

            const row = $(this);
            const id = toInt(row.attr("data-id"), 0);
            const name = row.attr("data-name") || String(id);
            if (id) {
                loadQuickView(id, name);
            }
        });

        $(document).on("keydown", "#tableContainer .membership-row", function (event) {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }
            if ($(event.target).closest("button,input,a,label").length > 0) {
                return;
            }

            event.preventDefault();
            const row = $(this);
            const id = toInt(row.attr("data-id"), 0);
            const name = row.attr("data-name") || String(id);
            if (id) {
                loadQuickView(id, name);
            }
        });

        $(document).on("change", "#tableContainer .phone-edit", function () {
            confirmInlineFieldUpdate($(this).closest(".membership-row"), "phone");
        });

        $(document).on("change", "#tableContainer .expiration-edit", function () {
            confirmInlineFieldUpdate($(this).closest(".membership-row"), "expiration");
        });

        $(document).on("keydown", "#tableContainer .phone-edit, #tableContainer .expiration-edit", function (event) {
            const row = $(this).closest(".membership-row");
            if (event.key === "Enter") {
                event.preventDefault();
                $(this).trigger("change");
            } else if (event.key === "Escape") {
                event.preventDefault();
                const fieldName = $(this).hasClass("phone-edit") ? "phone" : "expiration";
                revertInlineField(row, fieldName);
            }
        });

        $(document).on("click", "#tableContainer .quick-action-btn", function () {
            const row = $(this).closest(".membership-row");
            const action = $(this).attr("data-action");
            applyQuickAction(row, action);
        });

        $(document).on("click", "#tableContainer .cancel-dd-btn", function () {
            cancelDirectDebit($(this).closest(".membership-row"));
        });

        $(document).on("click", "#tableContainer .btn-history", function () {
            const row = $(this).closest(".membership-row");
            const id = toInt(row.attr("data-id"), 0);
            const name = row.attr("data-name") || String(id);
            activeDrawerMembershipId = id;
            if (dom.drawerTitle) {
                dom.drawerTitle.textContent = "עסקאות - " + name;
            }
            loadHistory(id);
        });

        $(document).on("click", ".copy-btn", function () {
            const value = $(this).attr("data-copy") || "";
            if (!value) {
                showStatus("אין ערך להעתקה.", "info");
                return;
            }

            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(value)
                    .then(function () { showStatus("הועתק ללוח.", "success"); })
                    .catch(function () { showStatus("לא ניתן להעתיק אוטומטית.", "error"); });
                return;
            }

            const temp = document.createElement("textarea");
            temp.value = value;
            document.body.appendChild(temp);
            temp.select();
            document.execCommand("copy");
            document.body.removeChild(temp);
            showStatus("הועתק ללוח.", "success");
        });
    }

    parseStateFromUrl();
    syncControlsFromState();
    bindEvents();
    initializeDatePickers();
    renderPagination();
    updateSortIndicators();
    updateSelectedCounter();
    updateKpisFromData();
    updateActiveKpiCard();
    updateResultsSummary();
    loadMemberships();
})();
