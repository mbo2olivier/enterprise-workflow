(() => {
    const updatePointer = event => {
        const surface = event.target.closest?.('.workspace, .login-page');
        if (!surface) return;
        const layer = surface.querySelector('.grid-reveal');
        if (!layer) return;
        // The reveal is fixed to the viewport, independently of the scrolling content.
        const rect = layer.getBoundingClientRect();
        surface.style.setProperty('--pointer-x', `${event.clientX - rect.left}px`);
        surface.style.setProperty('--pointer-y', `${event.clientY - rect.top}px`);
    };

    document.addEventListener('pointermove', updatePointer, { passive: true });
    document.addEventListener('click', event => {
        document.querySelectorAll('.top-profile-wrap[open], .identity-action-menu[open]').forEach(menu => {
            if (!menu.contains(event.target)) menu.removeAttribute('open');
        });
    });
})();

// Delegated events keep SSR forms and uploaded values intact during remote lookups.
(() => {
    const pending = new WeakMap();
    function close(picker) {
        picker.querySelector('[data-candidate-results]').hidden = true;
        picker.querySelector('[data-candidate-query]').setAttribute('aria-expanded', 'false');
    }
    function choose(picker, option) {
        const input = picker.querySelector('[data-candidate-query]');
        input.value = option.textContent;
        picker.querySelector('[data-candidate-value]').value = option.dataset.value;
        picker.querySelector('[data-candidate-status]').textContent = 'Personne sélectionnée.';
        input.removeAttribute('aria-activedescendant');
        close(picker);
        input.focus();
    }
    document.addEventListener('input', event => {
        if (!event.target.matches('[data-candidate-query]')) return;
        const input = event.target, picker = input.closest('.candidate-picker');
        const previous = pending.get(picker);
        if (previous) { clearTimeout(previous.timer); previous.controller.abort(); }
        picker.querySelector('[data-candidate-value]').value = '';
        const status = picker.querySelector('[data-candidate-status]');
        const list = picker.querySelector('[data-candidate-results]');
        list.replaceChildren();
        close(picker);
        const q = input.value.trim();
        if (q.length < 2) { status.textContent = 'Saisissez au moins deux caractères.'; return; }
        const request = { controller: new AbortController() };
        pending.set(picker, request);
        request.timer = setTimeout(async () => {
            status.textContent = 'Recherche…';
            try {
                const response = await fetch(`${picker.dataset.candidateEndpoint}?q=${encodeURIComponent(q)}`, {
                    signal: request.controller.signal, credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }
                });
                if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('unavailable');
                const candidates = await response.json();
                if (pending.get(picker) !== request || !picker.isConnected) return;
                candidates.forEach((candidate, index) => {
                    const option = document.createElement('li');
                    option.id = `${input.id}-option-${index}`;
                    option.setAttribute('role', 'option');
                    option.setAttribute('aria-selected', 'false');
                    option.dataset.value = `${encodeURIComponent(candidate.identity.providerId)}|${encodeURIComponent(candidate.identity.subjectId)}`;
                    option.textContent = candidate.displayName + (candidate.email ? ` · ${candidate.email}` : '') + ` · ${candidate.identity.providerId}`;
                    list.append(option);
                });
                list.hidden = candidates.length === 0;
                input.setAttribute('aria-expanded', String(candidates.length > 0));
                status.textContent = candidates.length === 0 ? 'Aucun candidat éligible.' : candidates.length >= 20 ? 'Affinez la recherche si la personne souhaitée ne figure pas dans cette liste.' : `${candidates.length} candidat(s). Utilisez les flèches puis Entrée pour sélectionner.`;
            } catch (error) {
                if (error.name !== 'AbortError' && pending.get(picker) === request) status.textContent = 'Recherche indisponible. Réessayez.';
            }
        }, 300);
    });
    document.addEventListener('click', event => {
        const option = event.target.closest('[data-candidate-results] [role=option]');
        if (option) choose(option.closest('.candidate-picker'), option);
        document.querySelectorAll('.candidate-picker').forEach(picker => { if (!picker.contains(event.target)) close(picker); });
    });
    document.addEventListener('keydown', event => {
        if (!event.target.matches('[data-candidate-query]')) return;
        const input = event.target, picker = input.closest('.candidate-picker');
        if (event.key === 'Escape') { close(picker); return; }
        const options = [...picker.querySelectorAll('[role=option]')];
        if (!options.length) return;
        let current = options.findIndex(option => option.id === input.getAttribute('aria-activedescendant'));
        if (event.key === 'Enter') { event.preventDefault(); if (current >= 0) choose(picker, options[current]); }
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            current = (current + (event.key === 'ArrowDown' ? 1 : -1) + options.length) % options.length;
            options.forEach((option, index) => option.setAttribute('aria-selected', String(index === current)));
            input.setAttribute('aria-activedescendant', options[current].id);
            picker.querySelector('[data-candidate-results]').hidden = false;
            input.setAttribute('aria-expanded', 'true');
            options[current].scrollIntoView({ block: 'nearest' });
        }
    });
    document.addEventListener('submit', event => {
        const action = event.submitter?.name === 'action' ? event.submitter.value : null;
        event.target.querySelectorAll('[data-assignment-action]').forEach(group => {
            group.querySelectorAll('[data-candidate-value]').forEach(input => { input.disabled = action !== group.dataset.assignmentAction; });
        });
    });
})();
document.addEventListener('click', event => {
    if (event.target.closest('[data-open-search]')) {
        const dialog = document.querySelector('[data-search-dialog]');
        dialog?.showModal();
        dialog?.querySelector('select,input')?.focus();
    }
    if (event.target.closest('[data-close-search]')) document.querySelector('[data-search-dialog]')?.close();
});
(() => {
    const uploads = new WeakMap();
    document.addEventListener('change', async event => {
        if (!event.target.matches('[data-media-upload]')) return;
        const input = event.target, form = input.closest('form'), field = input.dataset.mediaUpload;
        const hidden = [...form.querySelectorAll('[data-media-reference]')].find(item => item.dataset.mediaReference === field);
        const status = [...form.querySelectorAll('[data-media-status]')].find(item => item.dataset.mediaStatus === field);
        uploads.get(input)?.abort();
        const controller = new AbortController();
        uploads.set(input, controller);
        hidden.value = '[]';
        delete input.dataset.mediaError;
        if (input.files.length > Number(input.dataset.mediaMaxFiles)) {
            input.dataset.mediaError = 'true';
            status.textContent = 'Trop de fichiers pour ce champ.';
            return;
        }
        input.dataset.mediaPending = 'true';
        status.textContent = 'Validation et dépôt des fichiers…';
        const references = [];
        try {
            const path = new URL(form.action).pathname;
            const endpoint = (path.startsWith('/workflow/start/') ? path : path.replace(/\/action$/, '')) + `/media/${encodeURIComponent(field)}`;
            for (const file of input.files) {
                const data = new FormData();
                data.append('file', file);
                data.append('operationId', crypto.randomUUID());
                data.append('submissionKey', form.querySelector('[name=idempotencyKey]')?.value || '');
                data.append('__RequestVerificationToken', form.querySelector('[name=__RequestVerificationToken]').value);
                const response = await fetch(endpoint, { method: 'POST', body: data, signal: controller.signal, credentials: 'same-origin', cache: 'no-store' });
                if (!response.ok) throw new Error('rejected');
                const result = await response.json();
                const reference = result.reference || result;
                if (result.pending) {
                    status.textContent = 'Analyse du fichier en attente…';
                    let ready = false;
                    for (let attempt = 0; attempt < 60; attempt++) {
                        await new Promise(resolve => setTimeout(resolve, 2000));
                        const checked = await fetch(`/workflow/media/${reference.id}/${reference.revision}/status`, { signal: controller.signal, cache: 'no-store', credentials: 'same-origin' });
                        if (!checked.ok) throw new Error('unavailable');
                        const inspection = await checked.json();
                        if (inspection.status === 'ready') { ready = true; break; }
                        if (inspection.status === 'rejected') throw new Error('rejected');
                    }
                    if (!ready) throw new Error('pending');
                }
                references.push({ Id: reference.id, Revision: reference.revision });
            }
            if (uploads.get(input) !== controller) return;
            hidden.value = JSON.stringify(references);
            status.textContent = references.length ? `${references.length} fichier(s) validé(s). Ils seront rattachés à l’enregistrement du formulaire.` : 'Aucun fichier sélectionné.';
        } catch (error) {
            if (error.name !== 'AbortError' && uploads.get(input) === controller) {
                input.dataset.mediaError = 'true';
                status.textContent = 'Dépôt refusé ou indisponible. Vérifiez le type et la taille, puis sélectionnez à nouveau les fichiers.';
            }
        } finally { if (uploads.get(input) === controller) delete input.dataset.mediaPending; }
    });
    document.addEventListener('submit', event => {
        if (event.target.querySelector('[data-media-pending],[data-media-error]')) {
            event.preventDefault();
            event.target.querySelector('[data-media-pending],[data-media-error]').focus();
        }
    });
})();

// Remote controls become interactive only after their delegated handlers are registered.
(() => {
    const enable = () => document.querySelectorAll('[data-candidate-query][disabled],[data-media-upload][disabled],[data-open-search][disabled]').forEach(input => { input.disabled = false; });
    enable();
    new MutationObserver(enable).observe(document.documentElement, { childList: true, subtree: true });
})();
// Promote access editors into the browser top layer, outside page stacking contexts.
(() => {
    function promote() {
        document.querySelectorAll('div.admin-modal-backdrop:not([data-promoted])').forEach(backdrop => {
            const dialog = document.createElement('dialog');
            dialog.className = backdrop.className;
            dialog.dataset.promoted = 'true';
            while (backdrop.firstChild) dialog.append(backdrop.firstChild);
            backdrop.replaceWith(dialog);
            dialog.showModal();
            dialog.addEventListener('cancel', event => {
                const link = dialog.querySelector('a.close-decision');
                if (link) { event.preventDefault(); link.click(); }
            });
        });
    }
    document.addEventListener('input', event => {
        if (!event.target.matches('[data-status-search]')) return;
        event.target.parentElement.querySelectorAll('label').forEach(label => {
            label.hidden = !label.textContent.toLocaleLowerCase().includes(event.target.value.toLocaleLowerCase());
        });
    });
    document.addEventListener('change', event => {
        const dropdown = event.target.closest('.status-dropdown');
        if (!dropdown) return;
        updateStatuses(dropdown);
    });
    document.addEventListener('toggle', event => {
        const dropdown = event.target;
        if (!dropdown.matches?.('.status-dropdown')) return;
        const menu = dropdown.querySelector('.status-options');
        if (!dropdown.open) { menu.hidePopover(); return; }
        const rect = dropdown.querySelector('summary').getBoundingClientRect();
        const width = Math.min(Math.max(rect.width, 260), innerWidth - 24);
        menu.style.width = `${width}px`;
        menu.style.left = `${Math.max(12, Math.min(rect.left, innerWidth - width - 12))}px`;
        menu.style.top = `${rect.bottom + 6}px`;
        menu.style.maxHeight = `${Math.min(340, innerHeight - rect.bottom - 18)}px`;
        menu.showPopover();
    }, true);
    function updateStatuses(dropdown) {
        const selected = [...dropdown.querySelectorAll('input[type=checkbox]:checked')];
        const names = selected.map(input => input.getAttribute('aria-label'));
        dropdown.querySelector('[data-status-summary]').textContent = names.length ? names.slice(0, 2).join(', ') + (names.length > 2 ? ` + ${names.length - 2}` : '') : 'Tous les statuts';
        dropdown.querySelector('[data-status-count]').textContent = `${names.length} sélectionné(s)`;
        const options = [...dropdown.querySelectorAll('[data-status-option]')];
        options.filter(label => label.querySelector('input').checked).concat(options.filter(label => !label.querySelector('input').checked))
            .forEach(label => dropdown.querySelector('.status-menu-footer').before(label));
    }
    document.addEventListener('click', event => {
        const clear = event.target.closest('[data-status-clear]');
        if (clear) {
            const dropdown = clear.closest('.status-dropdown');
            dropdown.querySelectorAll('input[type=checkbox]').forEach(input => input.checked = false);
            updateStatuses(dropdown);
        }
        document.querySelectorAll('.status-dropdown[open]').forEach(dropdown => {
            if (!dropdown.contains(event.target)) dropdown.open = false;
        });
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const dropdown = event.target.closest('.status-dropdown[open]');
        if (dropdown) { event.preventDefault(); event.stopPropagation(); dropdown.open = false; dropdown.querySelector('summary').focus(); }
    }, true);
    new MutationObserver(promote).observe(document.body, {childList:true, subtree:true});
    promote();
})();

document.addEventListener('click', async event => {
    const button = event.target.closest('[data-copy-target]');
    if (!button) return;
    const target = document.getElementById(button.dataset.copyTarget);
    if (!target) return;
    const feedback = document.getElementById('copy-feedback');
    try { await navigator.clipboard.writeText(target.textContent.trim()); if (feedback) feedback.textContent = 'Référence copiée'; }
    catch { if (feedback) feedback.textContent = 'Copiez la référence affichée'; }
});
