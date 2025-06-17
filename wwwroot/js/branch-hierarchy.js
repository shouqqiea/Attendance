/**
 * Branch Hierarchy Management JavaScript
 * Handles tree navigation, dynamic filtering, and branch selection functionality
 */

class BranchHierarchyManager {
    constructor() {
        this.expandedBranches = new Set();
        this.branchData = [];
        this.currentFilters = {
            search: '',
            branchType: '',
            parentId: null
        };
        
        this.initializeEventListeners();
        this.loadBranchData();
    }

    /**
     * Initialize event listeners for branch hierarchy interactions
     */
    initializeEventListeners() {
        // Tree toggle buttons
        document.addEventListener('click', (e) => {
            if (e.target.closest('[data-toggle-branch]')) {
                const branchId = e.target.closest('[data-toggle-branch]').dataset.toggleBranch;
                this.toggleBranch(parseInt(branchId));
            }
        });

        // Search functionality
        const searchInput = document.getElementById('branchSearch');
        if (searchInput) {
            searchInput.addEventListener('input', this.debounce((e) => {
                this.currentFilters.search = e.target.value.toLowerCase();
                this.filterBranches();
            }, 300));
        }

        // Parent branch selection in forms
        const parentBranchSelect = document.getElementById('ParentBranchId');
        if (parentBranchSelect) {
            parentBranchSelect.addEventListener('change', (e) => {
                this.updateHierarchyPreview();
            });
        }

        // Branch type selection in forms
        const branchTypeSelect = document.getElementById('BranchType');
        if (branchTypeSelect) {
            branchTypeSelect.addEventListener('change', (e) => {
                this.updateParentBranchOptions(e.target.value);
                this.updateHierarchyPreview();
            });
        }
    }

    /**
     * Load branch data from the server or page data
     */
    loadBranchData() {
        // Try to get branch data from window object (set by server)
        if (window.branchHierarchyData) {
            this.branchData = window.branchHierarchyData;
        }
    }

    /**
     * Toggle branch expansion state
     * @param {number} branchId - The ID of the branch to toggle
     */
    toggleBranch(branchId) {
        const isExpanded = this.expandedBranches.has(branchId);
        const toggleIcon = document.getElementById(`toggle-icon-${branchId}`) || 
                         document.getElementById(`toggle-icon-mobile-${branchId}`);
        
        if (isExpanded) {
            this.expandedBranches.delete(branchId);
            this.hideChildBranches(branchId);
            
            if (toggleIcon) {
                toggleIcon.classList.remove('rotate-90');
            }
        } else {
            this.expandedBranches.add(branchId);
            this.showChildBranches(branchId);
            
            if (toggleIcon) {
                toggleIcon.classList.add('rotate-90');
            }
        }
    }

    /**
     * Hide child branches of a specific branch
     * @param {number} parentBranchId - The parent branch ID
     */
    hideChildBranches(parentBranchId) {
        const childElements = this.getChildElements(parentBranchId);
        
        childElements.forEach(element => {
            element.classList.add('hidden');
            
            // Also collapse any expanded children
            const childBranchId = parseInt(element.dataset.branchId);
            if (this.expandedBranches.has(childBranchId)) {
                this.expandedBranches.delete(childBranchId);
                const childIcon = document.getElementById(`toggle-icon-${childBranchId}`) ||
                                document.getElementById(`toggle-icon-mobile-${childBranchId}`);
                if (childIcon) {
                    childIcon.classList.remove('rotate-90');
                }
            }
        });
    }

    /**
     * Show child branches of a specific branch
     * @param {number} parentBranchId - The parent branch ID
     */
    showChildBranches(parentBranchId) {
        const directChildren = this.getDirectChildElements(parentBranchId);
        
        directChildren.forEach(element => {
            element.classList.remove('hidden');
        });

        // Show mobile children containers
        const mobileContainer = document.getElementById(`children-mobile-${parentBranchId}`);
        if (mobileContainer) {
            mobileContainer.classList.remove('hidden');
        }
    }

    /**
     * Get all child elements of a branch
     * @param {number} parentBranchId - The parent branch ID
     * @returns {Array} - Child elements
     */
    getChildElements(parentBranchId) {
        const parentElement = document.querySelector(`[data-branch-id="${parentBranchId}"]`);
        if (!parentElement) return [];

        const parentLevel = parseInt(parentElement.dataset.level);
        const allElements = document.querySelectorAll('[data-branch-id]');
        const childElements = [];
        
        let foundParent = false;
        for (const element of allElements) {
            const elementBranchId = parseInt(element.dataset.branchId);
            const elementLevel = parseInt(element.dataset.level);
            
            if (elementBranchId === parentBranchId) {
                foundParent = true;
                continue;
            }
            
            if (foundParent) {
                if (elementLevel <= parentLevel) {
                    break;
                }
                childElements.push(element);
            }
        }
        
        return childElements;
    }

    /**
     * Get direct child elements of a branch
     * @param {number} parentBranchId - The parent branch ID
     * @returns {Array} - Direct child elements
     */
    getDirectChildElements(parentBranchId) {
        const parentElement = document.querySelector(`[data-branch-id="${parentBranchId}"]`);
        if (!parentElement) return [];

        const parentLevel = parseInt(parentElement.dataset.level);
        const targetLevel = parentLevel + 1;
        
        return this.getChildElements(parentBranchId).filter(element => 
            parseInt(element.dataset.level) === targetLevel
        );
    }

    /**
     * Update hierarchy preview in forms
     */
    updateHierarchyPreview() {
        const parentSelect = document.getElementById('ParentBranchId');
        const branchNameInput = document.getElementById('BranchName');
        const preview = document.getElementById('hierarchyPreview');
        const hierarchyPath = document.getElementById('hierarchyPath');
        
        if (!parentSelect || !branchNameInput || !preview || !hierarchyPath) return;
        
        const selectedParent = parentSelect.options[parentSelect.selectedIndex];
        const branchName = branchNameInput.value.trim() || '[New Branch]';
        
        if (selectedParent.value) {
            const parentText = selectedParent.text.replace(/^[-─\s]*[🏢🏪]\s*/, '');
            hierarchyPath.textContent = `${parentText} > ${branchName}`;
            preview.classList.remove('hidden');
        } else {
            hierarchyPath.textContent = `${branchName} (Root Level)`;
            preview.classList.remove('hidden');
        }
    }

    /**
     * Update parent branch options based on selected branch type
     * @param {string} branchType - The selected branch type
     */
    updateParentBranchOptions(branchType) {
        const parentSelect = document.getElementById('ParentBranchId');
        if (!parentSelect) return;

        const options = parentSelect.querySelectorAll('option[value]:not([value=""])');
        
        options.forEach(option => {
            const optionText = option.textContent;
            const isCorporateParent = optionText.includes('🏢');
            
            if (branchType === '1' || branchType === '2') {
                option.style.display = isCorporateParent ? '' : 'none';
            }
        });
    }

    /**
     * Utility function to debounce function calls
     * @param {Function} func - Function to debounce
     * @param {number} wait - Wait time in milliseconds
     * @returns {Function} - Debounced function
     */
    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }
}

// Global functions for compatibility with existing code
function toggleBranch(branchId) {
    if (window.branchHierarchyManager) {
        window.branchHierarchyManager.toggleBranch(branchId);
    }
}

function toggleBranchMobile(branchId) {
    if (window.branchHierarchyManager) {
        window.branchHierarchyManager.toggleBranch(branchId);
    }
}

function expandAllBranches() {
    const allIcons = document.querySelectorAll('[id^="toggle-icon-"]:not([id*="mobile"])');
    const allRows = document.querySelectorAll('tr[data-branch-id]');
    
    allIcons.forEach(icon => {
        icon.classList.add('rotate-90');
    });
    
    allRows.forEach(row => {
        row.classList.remove('hidden');
    });
    
    const mobileIcons = document.querySelectorAll('[id^="toggle-icon-mobile-"]');
    const mobileContainers = document.querySelectorAll('[id^="children-mobile-"]');
    
    mobileIcons.forEach(icon => {
        icon.classList.add('rotate-90');
    });
    
    mobileContainers.forEach(container => {
        container.classList.remove('hidden');
    });
}

function collapseAllBranches() {
    const allIcons = document.querySelectorAll('[id^="toggle-icon-"]:not([id*="mobile"])');
    const childRows = document.querySelectorAll('tr[data-level]:not([data-level="0"])');
    
    allIcons.forEach(icon => {
        icon.classList.remove('rotate-90');
    });
    
    childRows.forEach(row => {
        row.classList.add('hidden');
    });
    
    const mobileIcons = document.querySelectorAll('[id^="toggle-icon-mobile-"]');
    const mobileContainers = document.querySelectorAll('[id^="children-mobile-"]');
    
    mobileIcons.forEach(icon => {
        icon.classList.remove('rotate-90');
    });
    
    mobileContainers.forEach(container => {
        container.classList.add('hidden');
    });
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.branchHierarchyManager = new BranchHierarchyManager();
}); 