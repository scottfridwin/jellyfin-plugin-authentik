// Authentik SSO Login Button Injection
// This script is loaded via Jellyfin's custom JavaScript branding setting.
// It watches for the login page and adds a "Sign in with Authentik" button.

(function () {
    'use strict';

    const BUTTON_ID = 'authentik-sso-login-btn';
    const CHECK_INTERVAL = 500;
    const BASE_PATH = window.location.pathname.replace(/\/web\/.*$/, '');

    function getLoginForm() {
        // Jellyfin Web login form selectors (covers multiple versions)
        return document.querySelector('.manualLoginForm, #manualLoginForm, form.loginForm');
    }

    function buttonExists() {
        return document.getElementById(BUTTON_ID) !== null;
    }

    function createButton() {
        const btn = document.createElement('button');
        btn.id = BUTTON_ID;
        btn.type = 'button';
        btn.classList.add('raised', 'button-submit', 'block', 'emby-button');
        btn.style.cssText = 'margin-top: 1em; background: #4051b5; color: white; display: flex; align-items: center; justify-content: center; gap: 0.5em;';
        btn.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"/>
                <polyline points="10 17 15 12 10 7"/>
                <line x1="15" y1="12" x2="3" y2="12"/>
            </svg>
            <span>Sign in with Authentik</span>
        `;
        btn.addEventListener('click', function () {
            window.location.href = BASE_PATH + '/authentik/start';
        });
        return btn;
    }

    function injectButton() {
        if (buttonExists()) return;

        const form = getLoginForm();
        if (!form) return;

        const btn = createButton();

        // Add a separator
        const separator = document.createElement('div');
        separator.style.cssText = 'margin-top: 1.5em; margin-bottom: 0.5em; text-align: center; color: #888; font-size: 0.9em;';
        separator.textContent = '— or —';

        // Insert after the form
        form.parentNode.insertBefore(separator, form.nextSibling);
        form.parentNode.insertBefore(btn, separator.nextSibling);
    }

    function isLoginPage() {
        const path = window.location.hash || window.location.pathname;
        return path.includes('login') || path === '' || path === '#' || path === '#/' ||
               document.querySelector('.manualLoginForm, #manualLoginForm, form.loginForm') !== null;
    }

    // Poll for login page (handles SPA navigation)
    function poll() {
        if (isLoginPage()) {
            injectButton();
        }
    }

    // Use MutationObserver for efficiency, with polling fallback
    const observer = new MutationObserver(function () {
        if (isLoginPage() && !buttonExists()) {
            injectButton();
        }
    });

    observer.observe(document.body || document.documentElement, {
        childList: true,
        subtree: true
    });

    // Initial check + periodic fallback
    poll();
    setInterval(poll, CHECK_INTERVAL);
})();
