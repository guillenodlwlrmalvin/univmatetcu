/**
 * Dashboard Module - Complete updated version matching RequestController
 * and your existing partial views (_RequestDetails, _StaffRequestList, etc.)
 */
const Dashboard = (function () {
    // Private state
    const state = {
        currentRequestId: null,
        currentTab: 'new-requests',
        isStaff: document.body.dataset.isStaff === 'true'
    };

    // DOM Cache with updated IDs to match your HTML
    const dom = {
        requestModal: $('#requestModal'),
        detailsModal: $('#requestDetailsModal'),
        requestForm: $('#requestForm'),
        imageFile: $('#imageFile'),
        imagePreview: $('#imagePreview'),
        uploadLabel: $('#uploadLabel'),
        toastNotification: $('#toastNotification'),
        toastMessage: $('#toastMessage'),
        requestList: $('#requestList'),
        tabLists: {
            'new-requests': $('#new-requests-list'),
            'in-progress': $('#in-progress-list'),
            'completed': $('#completed-list'),
            'all-requests': $('#all-requests-list')
        }
    };

    // Status constants (should match your RequestStatus enum)
    const STATUS = {
        PENDING: 'Pending',
        IN_PROGRESS: 'InProgress',
        COMPLETED: 'Completed'
    };

    // Initialize all dashboard components
    function init() {
        initUserDropdown();
        initModals();
        initFileUpload();
        initTabs();
        initFormSubmission();
        loadInitialData();
        bindGlobalEvents();
    }

    /* ========== CORE FUNCTIONALITY ========== */

    function loadInitialData() {
        if (state.isStaff) {
            loadStaffRequests(state.currentTab);
        } else {
            loadUserRequests();
        }
    }

    function loadUserRequests() {
        showLoading(dom.requestList);

        $.get("/Request/GetUserRequests")
            .done(data => dom.requestList.html(data)) // Uses _UserRequestlist.cshtml
            .fail(() => showError(dom.requestList, "Error loading your requests"))
            .always(() => hideLoading(dom.requestList));
    }

    function loadStaffRequests(tab = 'new-requests') {
        const $tabList = dom.tabLists[tab];
        if (!$tabList) return;

        showLoading($tabList);

        const status = getStatusFromTab(tab);

        $.get("/Request/GetStaffRequests", { status })
            .done(data => $tabList.html(data)) // Uses _StaffRequestList.cshtml
            .fail(() => showError($tabList, "Error loading requests"))
            .always(() => hideLoading($tabList));
    }

    function showRequestDetails(requestId) {
        state.currentRequestId = requestId;
        const $modal = dom.detailsModal;

        showLoading($modal);
        $("#requestDetailsContent, #requestActions").empty();

        $.get(`/Request/GetRequestDetails?id=${requestId}`)
            .done(data => {
                $("#requestDetailsContent").html(data); // Uses _RequestDetails.cshtml

                // Only load actions for staff on pending/in-progress requests
                if (state.isStaff && !data.includes('status-completed')) {
                    $.get("/Request/GetRequestActions", { requestId })
                        .done(actions => $("#requestActions").html(actions)) // Uses _StaffRequestAction.cshtml
                        .fail(() => showToast("Failed to load actions", "error"));
                }

                openModal($modal);
            })
            .fail(() => showToast("Failed to load request details", "error"))
            .always(() => hideLoading($modal));
    }

    function updateRequestStatus(requestId, status, $button) {
        $button.html('<i class="bi bi-arrow-repeat spinner"></i>').prop("disabled", true);

        return $.post("/Request/UpdateRequestStatus", {
            requestId,
            status,
            __RequestVerificationToken: getAntiForgeryToken()
        })
            .done(response => {
                if (response.success) {
                    handleStatusUpdateSuccess(status);
                } else {
                    showToast(response.message || "Update failed", "error");
                }
            })
            .fail(() => showToast("Error updating status", "error"))
            .always(() => {
                $button.html(getButtonText(status)).prop("disabled", false);
            });
    }

    function submitRequestForm(form) {
        const $submitBtn = $("#submitRequest");
        $submitBtn.html('<i class="bi bi-arrow-repeat spinner"></i> Submitting...').prop("disabled", true);

        $.ajax({
            url: "/Request/Create",
            type: "POST",
            data: new FormData(form),
            processData: false,
            contentType: false,
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        })
            .done(response => {
                if (response.success) {
                    handleRequestSubmissionSuccess();
                } else {
                    showToast(response.message || "Submission failed", "error");
                }
            })
            .fail(xhr => showToast(xhr.responseText || "Error occurred", "error"))
            .always(() => {
                $submitBtn.html('<i class="bi bi-send"></i> Submit Request').prop("disabled", false);
            });
    }

    /* ========== VIEW-SPECIFIC HELPERS ========== */

    function getStatusFromTab(tab) {
        switch (tab) {
            case 'new-requests': return STATUS.PENDING;
            case 'in-progress': return STATUS.IN_PROGRESS;
            case 'completed': return STATUS.COMPLETED;
            default: return 'All';
        }
    }

    function getButtonText(status) {
        return status === STATUS.IN_PROGRESS
            ? '<i class="bi bi-check-circle"></i> Start Work'
            : '<i class="bi bi-check-circle"></i> Mark Complete';
    }

    /* ========== DOM MANAGEMENT ========== */

    function initModals() {
        $('#openRequestModal').on("click", () => openModal(dom.requestModal));
        $('.close-modal').on("click", function () {
            const modalId = $(this).data("modal");
            $(`#${modalId}`).hide();
            $("body").css("overflow", "auto");
        });

        $(window).on("click", (event) => {
            if (event.target === dom.requestModal[0]) closeModal(dom.requestModal);
            if (event.target === dom.detailsModal[0]) closeModal(dom.detailsModal);
        });
    }

    function initTabs() {
        $(".tab").on("click", function () {
            const tabId = $(this).data("tab");
            switchTab(tabId);
        });

        // Search with debounce
        let searchTimer;
        $(".search-box input").on("input", function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => {
                const tabId = $(this).attr("id").replace("search-", "");
                filterRequests(tabId);
            }, 300);
        });

        // Status filter
        $(".status-filter button").on("click", function () {
            const filter = $(this).data("filter");
            $(this).siblings().removeClass("active");
            $(this).addClass("active");
            filterRequests(state.currentTab, filter);
        });
    }

    function filterRequests(tabId, filter = 'all') {
        const searchTerm = $(`#search-${tabId}`).val().toLowerCase();
        const $requests = $(`#${tabId}-list .request-item`);

        $requests.each(function () {
            const $request = $(this);
            const requestText = $request.text().toLowerCase();
            const isUrgent = $request.find(".badge-urgent").length > 0;
            const status = getRequestStatus($request);

            const matchesSearch = !searchTerm || requestText.includes(searchTerm);
            const matchesFilter = filter === 'all' ||
                (filter === 'urgent' && isUrgent) ||
                (filter === status.toLowerCase());

            $request.toggle(matchesSearch && matchesFilter);
        });
    }

    function getRequestStatus($request) {
        if ($request.find(".badge-pending").length) return STATUS.PENDING;
        if ($request.find(".badge-in-progress").length) return STATUS.IN_PROGRESS;
        if ($request.find(".badge-completed").length) return STATUS.COMPLETED;
        return '';
    }

    /* ========== UTILITY FUNCTIONS ========== */

    function showToast(message, type = "success") {
        dom.toastNotification
            .removeClass("success error warning")
            .addClass(type)
            .addClass("show");

        dom.toastMessage.text(message);

        setTimeout(() => {
            dom.toastNotification.removeClass("show");
        }, 5000);
    }

    function handleStatusUpdateSuccess(status) {
        showToast(`Request status updated to ${status}`, "success");
        closeModal(dom.detailsModal);

        if (state.isStaff) {
            loadStaffRequests(state.currentTab);
            if (status === STATUS.COMPLETED) loadStaffRequests('completed');
            if (status === STATUS.IN_PROGRESS) loadStaffRequests('in-progress');
        } else {
            loadUserRequests();
        }
    }

    function handleRequestSubmissionSuccess() {
        showToast("Request submitted successfully!", "success");
        closeModal(dom.requestModal);
        state.isStaff ? loadStaffRequests('new-requests') : loadUserRequests();
    }

    // Public API
    return { init };
})();

// Initialize when ready
$(document).ready(() => Dashboard.init());