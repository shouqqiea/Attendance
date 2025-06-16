document.addEventListener('DOMContentLoaded', function () {
    // Add CSS for loading spinner if not already added
    if (!document.getElementById('leaveRequestSpinnerStyle')) {
        const style = document.createElement('style');
        style.id = 'leaveRequestSpinnerStyle';
        style.textContent = `
            .spinner {
                display: inline-block;
                width: 1em;
                height: 1em;
                border: 2px solid #ffffff;
                border-radius: 50%;
                border-top-color: transparent;
                animation: spin 1s linear infinite;
                margin-right: 0.5em;
            }

            @keyframes spin {
                to {
                    transform: rotate(360deg);
                }
            }
        `;
        document.head.appendChild(style);
    }

    // Sidebar toggle functionality
    const mobileMenuToggle = document.getElementById('mobileMenuToggle');
    const closeMobileMenu = document.getElementById('closeMobileMenu');
    const menuOverlay = document.getElementById('menuOverlay');
    const mobileSidebar = document.getElementById('mobileSidebar');
    const toggleSidebar = document.getElementById('toggleSidebar');
    const desktopSidebar = document.getElementById('desktopSidebar');
    const mainContent = document.getElementById('mainContent');

    // Mobile menu toggle
    if (mobileMenuToggle) {
        mobileMenuToggle.addEventListener('click', function () {
            mobileSidebar.classList.add('open');
            menuOverlay.classList.add('active');
            document.body.style.overflow = 'hidden';
        });
    }

    // Close mobile menu
    if (closeMobileMenu) {
        closeMobileMenu.addEventListener('click', function () {
            mobileSidebar.classList.remove('open');
            menuOverlay.classList.remove('active');
            document.body.style.overflow = '';
        });
    }

    // Close when clicking overlay
    if (menuOverlay) {
        menuOverlay.addEventListener('click', function () {
            mobileSidebar.classList.remove('open');
            menuOverlay.classList.remove('active');
            document.body.style.overflow = '';
        });
    }

    // Desktop sidebar toggle
    let sidebarCollapsed = false;
    if (toggleSidebar) {
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
    }

    // Form elements
    const leaveForm = document.getElementById('leaveForm');
    const submitButton = document.getElementById('submitButton');
    const cancelButton = document.getElementById('cancelButton');
    const fileInput = document.getElementById('attachment');
    const fileNameDisplay = document.getElementById('fileName');
    const closeFormButton = document.getElementById('closeFormButton');

    // Hide form function
    function hideForm() {
        const formDiv = document.getElementById('leaveRequestForm');
        const toggleButton = document.getElementById('toggleFormButton');
        if (formDiv && toggleButton && leaveForm) {
            formDiv.classList.add('hidden');
            toggleButton.classList.remove('hidden');
            leaveForm.reset();
            if (fileNameDisplay) {
                fileNameDisplay.textContent = 'No file selected';
            }
        }
    }

    // Add event listeners
    if (closeFormButton) {
        closeFormButton.addEventListener('click', hideForm);
    }

    if (cancelButton) {
        cancelButton.addEventListener('click', hideForm);
    }

    // File input handling
    if (fileInput && fileNameDisplay) {
        fileInput.addEventListener('change', function(e) {
            const fileName = e.target.files[0]?.name || 'No file selected';
            fileNameDisplay.textContent = fileName;
        });
    }

    // Form submission handling
    if (leaveForm) {
        leaveForm.addEventListener('submit', async function(e) {
            e.preventDefault();

            // Client-side validation
            const startDate = new Date(document.getElementById('startDate').value);
            const endDate = new Date(document.getElementById('endDate').value);
            const leaveType = document.getElementById('leaveType').value;
            const reason = document.getElementById('reason').value;

            let isValid = true;
            let validationMessages = [];

            if (!leaveType) {
                isValid = false;
                validationMessages.push('Please select a leave type');
            }

            if (isNaN(startDate.getTime())) {
                isValid = false;
                validationMessages.push('Please select a start date');
            }

            if (isNaN(endDate.getTime())) {
                isValid = false;
                validationMessages.push('Please select an end date');
            }

            if (startDate > endDate) {
                isValid = false;
                validationMessages.push('End date must be after start date');
            }

            if (!reason.trim()) {
                isValid = false;
                validationMessages.push('Please provide a reason for your leave request');
            }

            if (!isValid) {
                validationMessages.forEach(message => {
                    showNotification('error', message);
                });
                return;
            }

            // Prepare form data
            const formData = new FormData(leaveForm);
            const originalButtonText = submitButton.innerHTML;
            submitButton.disabled = true;
            submitButton.innerHTML = '<span class="inline-flex items-center"><svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>Submitting...</span>';

            try {
                const response = await fetch('/LeaveManagement/LeaveRequest', {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                });

                const data = await response.json();

                if (data.success) {
                    showNotification('success', data.message);
                    hideForm();
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                } else {
                    if (data.errors && Array.isArray(data.errors)) {
                        data.errors.forEach(error => {
                            showNotification('error', error);
                        });
                    } else {
                        showNotification('error', data.message || 'An error occurred while submitting the request');
                    }
                }
            } catch (error) {
                console.error('Error:', error);
                showNotification('error', 'An error occurred while submitting the request');
            } finally {
                submitButton.disabled = false;
                submitButton.innerHTML = originalButtonText;
            }
        });
    }

    // Cancel request handler
    window.cancelRequest = function(requestId) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        fetch('/LeaveManagement/CancelLeaveRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(requestId)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showNotification('success', data.message);
                setTimeout(() => {
                    window.location.reload();
                }, 1500);
            } else {
                showNotification('error', data.message || 'An error occurred');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('error', 'An error occurred while canceling the request');
        });
    };

    // Notification function
    window.showNotification = function(type, message) {
        const container = document.getElementById('notificationContainer');
        const notification = document.createElement('div');
        notification.className = `p-4 mb-4 rounded-md shadow-lg transform transition-all duration-300 ${type === 'success' ? 'bg-green-50 text-green-800 border border-green-200' : 'bg-red-50 text-red-800 border border-red-200'}`;
        
        notification.innerHTML = `
            <div class="flex items-center">
                <div class="flex-shrink-0">
                    ${type === 'success' 
                        ? '<svg class="h-5 w-5 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>'
                        : '<svg class="h-5 w-5 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>'
                    }
                </div>
                <div class="ml-3">
                    <p class="text-sm font-medium">${message}</p>
                </div>
                <div class="ml-auto pl-3">
                    <div class="-mx-1.5 -my-1.5">
                        <button onclick="this.parentElement.parentElement.parentElement.parentElement.remove()" class="inline-flex rounded-md p-1.5 ${type === 'success' ? 'text-green-500 hover:bg-green-100' : 'text-red-500 hover:bg-red-100'} focus:outline-none">
                            <span class="sr-only">Dismiss</span>
                            <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd"/></svg>
                        </button>
                    </div>
                </div>
            </div>
        `;

        container.appendChild(notification);

        setTimeout(() => {
            notification.classList.add('opacity-0');
            setTimeout(() => {
                notification.remove();
            }, 300);
        }, 5000);
    };

    // New request button
    const newRequestButton = document.getElementById('newRequestButton');
    if (newRequestButton) {
        newRequestButton.addEventListener('click', function () {
            // Scroll to form
            const leaveRequestForm = document.getElementById('leaveRequestForm');
            if (leaveRequestForm) {
                leaveRequestForm.scrollIntoView({ behavior: 'smooth' });
            }

            // Reset form
            if (leaveForm) {
                leaveForm.reset();
                if (fileNameDisplay) {
                    fileNameDisplay.textContent = 'No file selected';
                }
            }
        });
    }
});