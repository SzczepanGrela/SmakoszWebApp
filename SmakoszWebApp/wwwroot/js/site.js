// Global JavaScript for SmakoszWebApp

// Global toast notification system
function showToast(type, message) {
    const toastContainer = document.querySelector('.toast-container');
    if (!toastContainer) {
        console.error('Toast container not found');
        return;
    }

    // Map alert types to toast colors and icons
    const toastConfig = {
        'success': { bg: 'bg-success', icon: 'fa-check-circle', title: 'Sukces' },
        'danger': { bg: 'bg-danger', icon: 'fa-exclamation-circle', title: 'Błąd' },
        'error': { bg: 'bg-danger', icon: 'fa-exclamation-circle', title: 'Błąd' },
        'warning': { bg: 'bg-warning', icon: 'fa-exclamation-triangle', title: 'Uwaga' },
        'info': { bg: 'bg-info', icon: 'fa-info-circle', title: 'Informacja' }
    };

    const config = toastConfig[type] || toastConfig['info'];
    const toastId = 'toast-' + Date.now();

    // Create toast element
    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center text-white ${config.bg} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center">
                    <i class="fa-solid ${config.icon} me-2"></i>
                    <div>
                        <strong class="me-2">${config.title}</strong>
                        <span>${message}</span>
                    </div>
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;

    // Insert toast into container
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);

    // Initialize and show toast
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, {
        autohide: true,
        delay: 4000
    });

    // Show the toast
    toast.show();

    // Remove from DOM after hiding
    toastElement.addEventListener('hidden.bs.toast', function() {
        this.remove();
    });
}

// Legacy function for backward compatibility
function showAlert(type, message) {
    showToast(type, message);
}

// Legacy function for backward compatibility  
function showNotification(message, type) {
    showToast(type, message);
}

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    console.log('SmakoszWebApp initialized');
    
    // Initialize any global functionality here
    initializeTooltips();
});

// Initialize Bootstrap tooltips
function initializeTooltips() {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    const tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
}
