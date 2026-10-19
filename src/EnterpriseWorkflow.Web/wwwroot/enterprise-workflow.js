(() => {
    const updatePointer = event => {
        const surface = event.target.closest?.('.workspace, .login-page');
        if (!surface) return;
        const rect = surface.getBoundingClientRect();
        surface.style.setProperty('--pointer-x', `${event.clientX - rect.left}px`);
        surface.style.setProperty('--pointer-y', `${event.clientY - rect.top}px`);
    };

    document.addEventListener('pointermove', updatePointer, { passive: true });
    document.addEventListener('click', event => {
        document.querySelectorAll('.top-profile-wrap[open]').forEach(menu => {
            if (!menu.contains(event.target)) menu.removeAttribute('open');
        });
    });
})();
