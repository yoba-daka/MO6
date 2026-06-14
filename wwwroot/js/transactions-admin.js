(function () {
    "use strict";

    const app = document.getElementById("transactionsApp");
    if (!app || typeof window.jQuery === "undefined") {
        return;
    }

    const $ = window.jQuery;

    const urls = {
        load: app.getAttribute("data-load-url") || "",
        refund: app.getAttribute("data-refund-url") || ""
    };

    const currentState = {
        page: toInt(app.getAttribute("data-current-page"), 1),
        totalPages: toInt(app.getAttribute("data-total-pages"), 1),
        search: app.getAttribute("data-search") || "",
        statusFilter: app.getAttribute("data-status-filter") || "all",
        sortBy: app.getAttribute("data-sort-by") || "Created",
        sortOrder: app.getAttribute("data-sort-order") || "desc"
    };

    const dom = {
        tableContainer: document.getElementById("tableContainer"),
        paginationContainer: document.getElementById("paginationContainer"),
        searchInput: document.getElementById("searchInput"),
        statusFilter: document.getElementById("statusFilter"),
        statusBanner: document.getElementById("transactionsStatus"),
        resultsSummary: document.getElementById("resultsSummary")
    };

    function toInt(raw, fallback) {
        const parsed = Number.parseInt(raw, 10);
        return Number.isFinite(parsed) ? parsed : fallback;
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
            }, 3500);
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
    }

    function syncStateFromControls() {
        currentState.search = (dom.searchInput?.value || "").trim();
        currentState.statusFilter = dom.statusFilter?.value || "all";
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

    function updateResultsSummary() {
        const dataElement = document.getElementById("pagination-data");
        const totalItems = toInt(dataElement?.getAttribute("data-total-items"), 0);
        if (dom.resultsSummary) {
            dom.resultsSummary.textContent = "תוצאות: " + totalItems;
        }
    }

    function setLoading(isLoading) {
        if (!dom.tableContainer) {
            return;
        }

        dom.tableContainer.style.opacity = isLoading ? "0.45" : "1";
        dom.tableContainer.style.pointerEvents = isLoading ? "none" : "auto";
    }

    function loadTransactions() {
        setLoading(true);

        $.ajax({
            url: urls.load,
            type: "GET",
            data: {
                page: currentState.page,
                search: currentState.search,
                statusFilter: currentState.statusFilter,
                sortBy: currentState.sortBy,
                sortOrder: currentState.sortOrder
            }
        })
            .done(function (response) {
                dom.tableContainer.innerHTML = response;
                const pagerData = document.getElementById("pagination-data");
                currentState.totalPages = toInt(pagerData?.getAttribute("data-total-pages"), 1);
                currentState.page = Math.min(Math.max(1, currentState.page), Math.max(1, currentState.totalPages));

                renderPagination();
                updateSortIndicators();
                updateResultsSummary();
                pushStateToUrl();
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
            })
            .always(function () {
                setLoading(false);
            });
    }

    function refundTransaction(id) {
        if (!id) {
            return;
        }

        if (!window.confirm("לבטל/לזכות עסקה זו?")) {
            return;
        }

        $.ajax({
            url: urls.refund,
            type: "POST",
            data: { id: id }
        })
            .done(function () {
                showStatus("העסקה זוכתה/בוטלה בהצלחה.", "success");
                loadTransactions();
            })
            .fail(function (xhr) {
                showStatus(extractError(xhr), "error");
            });
    }

    function bindEvents() {
        const debouncedSearch = debounce(function () {
            syncStateFromControls();
            currentState.page = 1;
            loadTransactions();
        }, 350);

        if (dom.searchInput) {
            dom.searchInput.addEventListener("input", debouncedSearch);
            dom.searchInput.addEventListener("keydown", function (event) {
                if (event.key === "Enter") {
                    event.preventDefault();
                    syncStateFromControls();
                    currentState.page = 1;
                    loadTransactions();
                }
            });
        }

        if (dom.statusFilter) {
            dom.statusFilter.addEventListener("change", function () {
                syncStateFromControls();
                currentState.page = 1;
                loadTransactions();
            });
        }

        $(document).on("click", "#paginationContainer a", function (event) {
            event.preventDefault();
            const page = toInt(this.getAttribute("data-page"), 1);
            if (page === currentState.page) {
                return;
            }

            currentState.page = page;
            loadTransactions();
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
            loadTransactions();
        });

        $(document).on("keydown", "#tableContainer .sortable-header", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                $(this).trigger("click");
            }
        });

        $(document).on("click", "#tableContainer .refund-btn", function () {
            refundTransaction(toInt($(this).attr("data-id"), 0));
        });
    }

    parseStateFromUrl();
    syncControlsFromState();
    bindEvents();
    renderPagination();
    updateSortIndicators();
    updateResultsSummary();
    loadTransactions();
})();
