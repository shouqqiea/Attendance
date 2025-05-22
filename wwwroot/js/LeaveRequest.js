document.addEventListener('DOMContentLoaded', function () {
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

    // File upload handling
    const fileInput = document.getElementById('attachment');
    const fileNameDisplay = document.getElementById('fileName');

    fileInput.addEventListener('change', function () {
        if (this.files.length > 0) {
            fileNameDisplay.textContent = this.files[0].name;
        } else {
            fileNameDisplay.textContent = 'No file selected';
        }
    });

    // Form submission
    const leaveForm = document.getElementById('leaveForm');

    leaveForm.addEventListener('submit', function (e) {
        e.preventDefault();

        // Validate form
        const leaveType = document.getElementById('leaveType').value;
        const startDate = document.getElementById('startDate').value;
        const endDate = document.getElementById('endDate').value;
        const reason = document.getElementById('reason').value;

        if (!leaveType || !startDate || !endDate || !reason) {
            showNotification('Please fill in all required fields', 'error');
            return;
        }

        // Check if start date is before end date
        if (new Date(startDate) > new Date(endDate)) {
            showNotification('Start date cannot be after end date', 'error');
            return;
        }

        // Submit form (in a real app, this would be an AJAX request)
        showNotification('Leave request submitted successfully!', 'success');
        leaveForm.reset();
        fileNameDisplay.textContent = 'No file selected';
    });

    // Cancel button
    const cancelButton = document.getElementById('cancelButton');

    cancelButton.addEventListener('click', function () {
        leaveForm.reset();
        fileNameDisplay.textContent = 'No file selected';
        showNotification('Form has been reset', 'info');
    });

    // New request button
    const newRequestButton = document.getElementById('newRequestButton');
    if (newRequestButton) {
        newRequestButton.addEventListener('click', function () {
            // Scroll to form
            document.getElementById('leaveRequestForm').scrollIntoView({ behavior: 'smooth' });

            // Reset form
            leaveForm.reset();
            fileNameDisplay.textContent = 'No file selected';
        });
    }
});

function cancelRequest(requestId) {
    const request = document.getElementById(requestId);

    // Show confirmation dialog
    if (confirm('Are you sure you want to cancel this leave request?')) {
        // Update status badge
        const statusBadge = request.querySelector('.status-badge');
        statusBadge.classList.remove('bg-yellow-100', 'text-yellow-800');
        statusBadge.classList.add('bg-gray-100', 'text-gray-800');
        statusBadge.textContent = 'Cancelled';

        // Update status icon
        const statusIcon = request.querySelector('.status-icon');
        statusIcon.classList.remove('bg-yellow-100');
        statusIcon.classList.add('bg-gray-100');
        statusIcon.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-gray-600" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" /></svg>';

        // Update border color and background
        request.classList.remove('border-l-yellow-500', 'bg-yellow-50');
        request.classList.add('border-l-gray-500', 'bg-gray-50');

        // Update button
        const button = request.querySelector('button');
        button.classList.remove('bg-red-500', 'hover:bg-red-600');
        button.classList.add('bg-gray-300', 'text-gray-600', 'cursor-not-allowed');
        button.disabled = true;
        button.textContent = 'Cancelled';
        button.onclick = null;

        // Show notification
        showNotification('Leave request cancelled successfully', 'success');
    }
}