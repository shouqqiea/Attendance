document.addEventListener('DOMContentLoaded', function () {
    const sortButton = document.getElementById('sortButton');
    const toggleHistoryButton = document.getElementById('toggleHistory');
    const historySection = document.getElementById('historySection');
    const chevronIcon = toggleHistoryButton.querySelector('.chevron-icon');

    // Sidebar toggle functionality
    const mobileMenuToggle = document.getElementById('mobileMenuToggle');
    const closeMobileMenu = document.getElementById('closeMobileMenu');
    const menuOverlay = document.getElementById('menuOverlay');
    const mobileSidebar = document.getElementById('mobileSidebar');
    const toggleSidebar = document.getElementById('toggleSidebar');
    const desktopSidebar = document.getElementById('desktopSidebar');
    const mainContent = document.getElementById('mainContent');

    // Mobile menu toggle
    mobileMenuToggle.addEventListener('click', function () {
        mobileSidebar.classList.add('open');
        menuOverlay.classList.add('active');
        document.body.style.overflow = 'hidden';
    });

    // Close mobile menu
    closeMobileMenu.addEventListener('click', function () {
        mobileSidebar.classList.remove('open');
        menuOverlay.classList.remove('active');
        document.body.style.overflow = '';
    });

    // Close when clicking overlay
    menuOverlay.addEventListener('click', function () {
        mobileSidebar.classList.remove('open');
        menuOverlay.classList.remove('active');
        document.body.style.overflow = '';
    });

    // Desktop sidebar toggle
    let sidebarCollapsed = false;
    toggleSidebar.addEventListener('click', function () {
        if (sidebarCollapsed) {
            desktopSidebar.classList.remove('collapsed');
            mainContent.classList.remove('md:ml-0');
            mainContent.classList.add('md:ml-64');
            sidebarCollapsed = false;
        } else {
            desktopSidebar.classList.add('collapsed');
            mainContent.classList.remove('md:ml-64');
            mainContent.classList.add('md:ml-0');
            sidebarCollapsed = true;
        }
    });

    // Initialize history section
    if (historySection) {
        historySection.classList.add('hidden');
    }
    
    // Toggle history section
    if (toggleHistoryButton) {
        toggleHistoryButton.addEventListener('click', function () {
            historySection.classList.toggle('hidden');
            chevronIcon.classList.toggle('rotate-180');
        });
    }

    // Initialize rejection reason fields as hidden
    document.querySelectorAll('.rejection-reason').forEach(el => {
        el.style.display = 'none';
    });

    // Initialize leave requests container
    const leaveRequestsContainer = document.getElementById('leaveRequestsContainer');
    if (leaveRequestsContainer) {
        leaveRequestsContainer.innerHTML = '';
    }

    // Sort leave requests initially
    sortLeaveRequests();

    // Add click event for sort button
    if (sortButton) {
        sortButton.addEventListener('click', function () {
            sortLeaveRequests();
            showNotification('Leave requests sorted by priority', 'success');
        });
    }

    // Sort history items by date within each month
    sortHistoryByDate();
});

function sortLeaveRequests() {
    const container = document.getElementById('pendingRequestsContainer');
    const requests = Array.from(container.querySelectorAll('.leave-request'));

    // Sort by priority (emergency -> pending -> approved -> rejected)
    requests.sort((a, b) => {
        const priorityA = parseInt(a.dataset.priority);
        const priorityB = parseInt(b.dataset.priority);
        return priorityA - priorityB;
    });

    // Remove all requests from container
    requests.forEach(request => request.remove());

    // Add the heading back
    const heading = container.querySelector('h3');

    // Clear container and add heading
    container.innerHTML = '';
    container.appendChild(heading);

    // Append sorted requests
    requests.forEach(request => {
        container.appendChild(request);
    });
}

function sortHistoryByDate() {
    // Get all month sections
    const monthSections = document.querySelectorAll('#historySection > div');

    monthSections.forEach(section => {
        const leaveItems = Array.from(section.querySelectorAll('.leave-request'));

        // Sort by date (newest first)
        leaveItems.sort((a, b) => {
            const dateA = new Date(a.dataset.date);
            const dateB = new Date(b.dataset.date);
            return dateB - dateA;
        });

        // Get the container
        const container = section.querySelector('.month-content');

        // Remove all items
        leaveItems.forEach(item => item.remove());

        // Add sorted items back
        leaveItems.forEach(item => {
            container.appendChild(item);
        });
    });
}

function showRejectionInput(requestId) {
    const rejectionInput = document.getElementById('rejectionReason' + requestId.replace('request', ''));
    if (rejectionInput) {
        rejectionInput.style.display = 'block';
    }

    // Change the reject button to confirm rejection
    const rejectButton = document.querySelector(`#${requestId} button:last-child`);
    if (rejectButton) {
        rejectButton.textContent = 'Confirm Rejection';
        rejectButton.onclick = function () {
            const reason = rejectionInput.querySelector('textarea').value;
            if (reason.trim() === '') {
                showNotification('Please provide a rejection reason', 'error');
                return;
            }
            rejectRequest(requestId, reason);
        };
    }
}

function approveRequest(requestId) {
    const request = document.getElementById(requestId);
    const requestIdNumber = requestId.replace('request', '');
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    fetch('/LeaveManagement/Approve', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify(parseInt(requestIdNumber))
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            const statusBadge = request.querySelector('.status-badge');
            const statusIcon = request.querySelector('.status-icon');

            // Update status badge
            statusBadge.classList.remove('bg-yellow-100', 'text-yellow-800');
            statusBadge.classList.add('bg-green-100', 'text-green-800');
            statusBadge.textContent = 'Approved';

            // Update status icon
            statusIcon.classList.remove('bg-yellow-100');
            statusIcon.classList.add('bg-green-100');
            statusIcon.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-green-600" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" /></svg>';

            // Update border color and background
            request.classList.remove('border-l-yellow-500', 'bg-yellow-50', 'border-l-red-500', 'bg-red-50');
            request.classList.add('border-l-green-500', 'bg-green-50');

            // Update buttons
            const buttons = request.querySelector('.action-buttons');
            buttons.classList.remove('bg-indigo-50');
            buttons.classList.add('bg-gray-100');
            buttons.innerHTML = `
                <button class="flex-1 bg-gray-100 text-gray-400 font-medium py-1.5 px-3 rounded-md text-sm cursor-not-allowed" disabled>
                    Approved
                </button>
            `;

            // Remove emergency badge if exists
            const emergencyBadge = request.querySelector('.emergency-badge');
            if (emergencyBadge) {
                emergencyBadge.remove();
            }

            // Update data attributes for sorting
            request.dataset.status = 'approved';
            request.dataset.priority = '3';

            // Move to history section
            setTimeout(() => {
                moveToHistory(request, 'approved');
            }, 1000);

            // Show notification
            showNotification('Leave request approved successfully', 'success');
        } else {
            showNotification(data.message || 'Failed to approve request', 'error');
        }
    })
    .catch(error => {
        showNotification('An error occurred while approving the request', 'error');
    });
}

function rejectRequest(requestId, reason) {
    const request = document.getElementById(requestId);
    const requestIdNumber = requestId.replace('request', '');
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    fetch('/LeaveManagement/Reject', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify({
            id: parseInt(requestIdNumber),
            rejectionReason: reason
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            const statusBadge = request.querySelector('.status-badge');
            const statusIcon = request.querySelector('.status-icon');

            // Update status badge
            statusBadge.classList.remove('bg-yellow-100', 'text-yellow-800');
            statusBadge.classList.add('bg-red-100', 'text-red-800');
            statusBadge.textContent = 'Rejected';

            // Update status icon
            statusIcon.classList.remove('bg-yellow-100');
            statusIcon.classList.add('bg-red-100');
            statusIcon.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-red-600" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" /></svg>';

            // Update border color and background
            request.classList.remove('border-l-yellow-500', 'bg-yellow-50', 'border-l-green-500', 'bg-green-50');
            request.classList.add('border-l-red-500', 'bg-red-50');

            // Update rejection reason
            const rejectionInput = document.getElementById('rejectionReason' + requestIdNumber);
            const rejectionText = rejectionInput.querySelector('textarea').value;

            // Replace the rejection input with static text
            rejectionInput.innerHTML = `
                <p class="text-gray-500 text-sm">Rejection Reason</p>
                <p class="text-red-700">${rejectionText}</p>
            `;

            // Update buttons
            const buttons = request.querySelector('.action-buttons');
            buttons.classList.remove('bg-indigo-50');
            buttons.classList.add('bg-gray-100');
            buttons.innerHTML = `
                <button class="flex-1 bg-gray-100 text-gray-400 font-medium py-1.5 px-3 rounded-md text-sm cursor-not-allowed" disabled>
                    Rejected
                </button>
            `;

            // Remove emergency badge if exists
            const emergencyBadge = request.querySelector('.emergency-badge');
            if (emergencyBadge) {
                emergencyBadge.remove();
            }

            // Update data attributes for sorting
            request.dataset.status = 'rejected';
            request.dataset.priority = '4';

            // Move to history section
            setTimeout(() => {
                moveToHistory(request, 'rejected');
            }, 1000);

            // Show notification
            showNotification('Leave request rejected', 'error');
        } else {
            showNotification(data.message || 'Failed to reject request', 'error');
        }
    })
    .catch(error => {
        showNotification('An error occurred while rejecting the request', 'error');
    });
}

function moveToHistory(request, status) {
    // Get the date from the request
    const dateString = request.dataset.date;
    const date = new Date(dateString);
    const month = date.toLocaleString('default', { month: 'long' });
    const year = date.getFullYear();

    // Find or create month section
    let monthSection = findOrCreateMonthSection(month, year);

    // Get leave details
    const name = request.querySelector('h3').textContent;
    const leaveType = request.querySelector('.grid-cols-2 div:first-child p:last-child, .grid-cols-4 div:first-child p:last-child').textContent;
    const fromDate = request.querySelector('.grid-cols-2 div:nth-child(3) p:last-child, .grid-cols-4 div:nth-child(3) p:last-child').textContent;
    const toDate = request.querySelector('.grid-cols-2 div:nth-child(4) p:last-child, .grid-cols-4 div:nth-child(4) p:last-child').textContent;

    // Format date range
    const fromDateShort = fromDate.split(', ')[0];
    const toDateShort = toDate.split(', ')[0];
    const dateRange = fromDateShort === toDateShort ? fromDateShort : `${fromDateShort}-${toDateShort.split(' ')[1]}`;

    // Create simplified history item
    const historyItem = document.createElement('div');
    historyItem.className = `leave-request leave-item border-l-${status === 'approved' ? 'green' : 'red'}-500 bg-${status === 'approved' ? 'green' : 'red'}-50 border border-gray-200 p-2 rounded-r-md shadow-sm mb-2`;
    historyItem.dataset.status = status;
    historyItem.dataset.date = dateString;

    historyItem.innerHTML = `
        <div class="flex justify-between items-center">
            <div class="flex items-center">
                <div class="status-icon bg-${status === 'approved' ? 'green' : 'red'}-100 mr-2" style="width: 20px; height: 20px;">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-${status === 'approved' ? 'green' : 'red'}-600" viewBox="0 0 20 20" fill="currentColor">
                        ${status === 'approved'
            ? '<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />'
            : '<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />'}
                    </svg>
                </div>
                <div>
                    <h3 class="font-medium text-gray-900 text-sm">${name}</h3>
                    <p class="text-xs text-gray-500">${leaveType}</p>
                </div>
            </div>
            <div class="flex items-center space-x-2">
                <span class="text-xs text-gray-500">${dateRange}</span>
                <span class="status-badge px-1.5 py-0.5 text-xs font-medium rounded-full bg-${status === 'approved' ? 'green' : 'red'}-100 text-${status === 'approved' ? 'green' : 'red'}-800">
                    ${status}
                </span>
            </div>
        </div>
    `;

    // Add to month section
    const itemsContainer = monthSection.querySelector('.month-content');
    itemsContainer.appendChild(historyItem);

    // Update the count
    updateMonthCount(monthSection);

    // Sort items in the month
    sortMonthItems(monthSection);

    // Remove from pending requests
    request.remove();

    // Show history section and expand it
    const historySection = document.getElementById('historySection');
    historySection.classList.remove('hidden');
    const chevronIcon = document.querySelector('.chevron-icon');
    chevronIcon.classList.add('rotate-180');
}

function findOrCreateMonthSection(month, year) {
    const monthYear = `${month} ${year}`;
    const historySection = document.getElementById('historySection');

    // Check if month section already exists
    let monthSection = Array.from(historySection.children).find(section => {
        return section.querySelector('h4').textContent === monthYear;
    });

    // If not, create it
    if (!monthSection) {
        monthSection = document.createElement('div');
        monthSection.className = 'mb-4 month-section';
        monthSection.innerHTML = `
                    <div class="month-header bg-gray-50 py-2 px-4 rounded-t-md border border-gray-200 flex justify-between items-center" onclick="toggleMonthSection(this)">
                        <h4 class="font-medium text-gray-700">${monthYear}</h4>
                        <div class="flex items-center">
                            <span class="text-xs text-gray-500 mr-2">0 leaves</span>
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-gray-500 chevron-icon" viewBox="0 0 20 20" fill="currentColor">
                                <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" />
                            </svg>
                        </div>
                    </div>
                    <div class="month-content space-y-2 pt-2"></div>
                `;

        // Insert in correct order (newest month first)
        const allMonthSections = Array.from(historySection.children);
        let inserted = false;

        for (let i = 0; i < allMonthSections.length; i++) {
            const sectionTitle = allMonthSections[i].querySelector('h4').textContent;
            const [sectionMonth, sectionYear] = sectionTitle.split(' ');

            if (parseInt(year) > parseInt(sectionYear) ||
                (parseInt(year) === parseInt(sectionYear) &&
                    getMonthNumber(month) > getMonthNumber(sectionMonth))) {
                historySection.insertBefore(monthSection, allMonthSections[i]);
                inserted = true;
                break;
            }
        }

        if (!inserted) {
            historySection.appendChild(monthSection);
        }
    }

    return monthSection;
}

function getMonthNumber(monthName) {
    const months = ['January', 'February', 'March', 'April', 'May', 'June',
        'July', 'August', 'September', 'October', 'November', 'December'];
    return months.indexOf(monthName);
}

function updateMonthCount(monthSection) {
    const count = monthSection.querySelectorAll('.leave-request').length;
    const countSpan = monthSection.querySelector('.text-xs');
    countSpan.textContent = `${count} leave${count !== 1 ? 's' : ''}`;
}

function sortMonthItems(monthSection) {
    const container = monthSection.querySelector('.month-content');
    const items = Array.from(container.querySelectorAll('.leave-request'));

    // Sort by date (newest first)
    items.sort((a, b) => {
        const dateA = new Date(a.dataset.date);
        const dateB = new Date(b.dataset.date);
        return dateB - dateA;
    });

    // Remove all items
    items.forEach(item => item.remove());

    // Add sorted items back
    items.forEach(item => {
        container.appendChild(item);
    });
}

function toggleMonthSection(header) {
    const monthSection = header.closest('.month-section');
    const content = monthSection.querySelector('.month-content');
    const chevron = header.querySelector('.chevron-icon');

    content.classList.toggle('hidden');
    chevron.classList.toggle('rotate-180');
}

// Add this new function to handle history section visibility
function toggleHistorySection() {
    const historySection = document.getElementById('historySection');
    const chevronIcon = document.querySelector('.chevron-icon');
    
    if (historySection) {
        historySection.classList.toggle('hidden');
        chevronIcon.classList.toggle('rotate-180');
        
        // Scroll to history section when opened
        if (!historySection.classList.contains('hidden')) {
            setTimeout(() => {
                historySection.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }, 100);
        }
    }
}

// Update the event listener for the toggle history button
document.addEventListener('DOMContentLoaded', function() {
    const toggleHistoryButton = document.getElementById('toggleHistory');
    if (toggleHistoryButton) {
        toggleHistoryButton.addEventListener('click', toggleHistorySection);
    }
    
    // ... rest of the existing DOMContentLoaded code ...
});