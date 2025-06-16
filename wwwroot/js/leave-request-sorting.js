document.addEventListener('DOMContentLoaded', function() {
    // Group leave requests by year and month
    function groupLeaveRequests() {
        const requests = document.querySelectorAll('.leave-request');
        const container = document.getElementById('leaveRequestsContainer');
        const groupedRequests = {};

        // Clear existing content
        container.innerHTML = '';

        // Group requests by year and month
        requests.forEach(request => {
            const date = new Date(request.dataset.date);
            const year = date.getFullYear();
            const month = date.toLocaleString('default', { month: 'long' });

            if (!groupedRequests[year]) {
                groupedRequests[year] = {};
            }
            if (!groupedRequests[year][month]) {
                groupedRequests[year][month] = [];
            }
            groupedRequests[year][month].push(request);
        });

        // Create collapsible sections for each year and month
        Object.keys(groupedRequests)
            .sort((a, b) => b - a) // Sort years in descending order
            .forEach(year => {
                const yearSection = document.createElement('div');
                yearSection.className = 'year-section mb-4';

                // Create year header
                const yearHeader = document.createElement('div');
                yearHeader.className = 'flex items-center justify-between bg-gray-100 p-3 rounded-lg cursor-pointer hover:bg-gray-200 transition-colors mb-2';
                yearHeader.innerHTML = `
                    <h3 class="text-lg font-medium text-gray-900">${year}</h3>
                    <svg class="h-5 w-5 transform transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                    </svg>
                `;

                // Create container for months
                const monthsContainer = document.createElement('div');
                monthsContainer.className = 'months-container ml-4';

                // Sort months in reverse chronological order
                const months = Object.keys(groupedRequests[year]).sort((a, b) => {
                    return new Date(Date.parse(`${a} 1, 2000`)) - new Date(Date.parse(`${b} 1, 2000`));
                }).reverse();

                months.forEach(month => {
                    const monthSection = document.createElement('div');
                    monthSection.className = 'month-section mb-3';

                    // Create month header
                    const monthHeader = document.createElement('div');
                    monthHeader.className = 'flex items-center justify-between bg-gray-50 p-2 rounded-md cursor-pointer hover:bg-gray-100 transition-colors mb-2';
                    monthHeader.innerHTML = `
                        <h4 class="text-md font-medium text-gray-800">${month}</h4>
                        <svg class="h-4 w-4 transform transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                        </svg>
                    `;

                    // Create container for requests
                    const requestsContainer = document.createElement('div');
                    requestsContainer.className = 'requests-container ml-3';

                    // Sort requests by start date within each month
                    const sortedRequests = groupedRequests[year][month].sort((a, b) => {
                        const dateA = new Date(a.dataset.date);
                        const dateB = new Date(b.dataset.date);
                        return dateB - dateA; // Sort in descending order
                    });

                    // Add requests to container
                    sortedRequests.forEach(request => {
                        requestsContainer.appendChild(request.cloneNode(true));
                    });

                    monthSection.appendChild(monthHeader);
                    monthSection.appendChild(requestsContainer);
                    monthsContainer.appendChild(monthSection);

                    // Add click handler for month header
                    monthHeader.addEventListener('click', () => {
                        requestsContainer.classList.toggle('hidden');
                        monthHeader.querySelector('svg').classList.toggle('rotate-180');
                    });
                });

                yearSection.appendChild(yearHeader);
                yearSection.appendChild(monthsContainer);
                container.appendChild(yearSection);

                // Add click handler for year header
                yearHeader.addEventListener('click', () => {
                    monthsContainer.classList.toggle('hidden');
                    yearHeader.querySelector('svg').classList.toggle('rotate-180');
                });
            });

        // Expand the most recent year and month by default
        const firstYearSection = container.querySelector('.year-section');
        const firstMonthSection = firstYearSection?.querySelector('.month-section');
        if (firstYearSection && firstMonthSection) {
            firstYearSection.querySelector('.months-container').classList.remove('hidden');
            firstYearSection.querySelector('svg').classList.add('rotate-180');
            firstMonthSection.querySelector('.requests-container').classList.remove('hidden');
            firstMonthSection.querySelector('svg').classList.add('rotate-180');
        }
    }

    // Initialize sorting
    groupLeaveRequests();

    // Add filter functionality
    const statusFilter = document.getElementById('statusFilter');
    if (statusFilter) {
        statusFilter.addEventListener('change', function() {
            const selectedStatus = this.value.toLowerCase();
            const requests = document.querySelectorAll('.leave-request');

            requests.forEach(request => {
                const requestStatus = request.dataset.status.toLowerCase();
                request.classList.toggle('hidden', 
                    selectedStatus !== 'all' && requestStatus !== selectedStatus);
            });

            // Regroup visible requests
            groupLeaveRequests();
        });
    }
}); 