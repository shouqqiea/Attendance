document.addEventListener('DOMContentLoaded', function () {
    // Set current date
    const currentDateElement = document.getElementById('currentDate');
    if (currentDateElement) {
        const now = new Date();
        const dateOptions = { month: 'long', day: 'numeric', year: 'numeric' };
        const dateString = now.toLocaleDateString('en-US', dateOptions);
        currentDateElement.textContent = dateString;
    }

    // Set current time and update it every second
    const currentTimeElement = document.getElementById('currentTime');
    if (currentTimeElement) {
        function updateTime() {
            const now = new Date();
            const timeOptions = { hour: 'numeric', minute: 'numeric', second: 'numeric', hour12: true };
            const timeString = now.toLocaleTimeString('en-US', timeOptions);
            currentTimeElement.textContent = timeString;
        }

        updateTime();
        setInterval(updateTime, 1000); // Update every second for real-time display
    }

    // User dropdown toggle
    const userMenuButton = document.getElementById('userMenuButton');
    const userDropdown = document.getElementById('userDropdown');
    if (userMenuButton && userDropdown) {
        userMenuButton.addEventListener('click', function () {
            userDropdown.classList.toggle('active');
        });
    }

    // Sort options toggle
    const sortButton = document.getElementById('sortButton');
    const sortOptions = document.getElementById('sortOptions');
    if (sortButton && sortOptions) {
        sortButton.addEventListener('click', function () {
            sortOptions.classList.toggle('active');
        });

        // Close dropdowns when clicking outside
        document.addEventListener('click', function (event) {
            if (!sortButton.contains(event.target) && !sortOptions.contains(event.target)) {
                sortOptions.classList.remove('active');
            }
        });
    }

    // Search toggle
    const searchToggle = document.getElementById('searchToggle');
    const searchContainer = document.getElementById('searchContainer');
    if (searchToggle && searchContainer) {
        searchToggle.addEventListener('click', function () {
            searchContainer.classList.toggle('active');
            if (searchContainer.classList.contains('active')) {
                searchContainer.querySelector('input').focus();
            }
        });
    }

    // Close user dropdown when clicking outside
    if (userMenuButton && userDropdown) {
        document.addEventListener('click', function (event) {
            if (!userMenuButton.contains(event.target) && !userDropdown.contains(event.target)) {
                userDropdown.classList.remove('active');
            }
        });
    }
});