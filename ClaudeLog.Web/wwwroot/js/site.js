// ClaudeLog - Client-side JavaScript
// Handles UI interactions, API calls, and conversation display

// State management
let currentPage = 1;
const pageSize = 200;
let currentSearch = '';
let selectedEntryId = null;
let allEntries = [];
let includeDeleted = false;
let showFavoritesOnly = false;
let isAddEntryPanelOpen = false;
let currentSessionForNewEntry = null;

/**
 * Debounce function to limit API calls during search typing
 * @param {Function} func - Function to debounce
 * @param {number} wait - Wait time in milliseconds (300ms for search)
 */
function debounce(func, wait) {
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

/**
 * Loads conversation entries from the API with filtering and pagination.
 * @param {string} search - Search query text
 * @param {number} page - Page number (1-indexed)
 * @param {boolean} append - If true, appends to existing list (for "Load More")
 */
async function loadEntries(search = '', page = 1, append = false) {
    try {
        const url = `/api/entries?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}&includeDeleted=${includeDeleted}&showFavoritesOnly=${showFavoritesOnly}`;
        const response = await fetch(url);
        const entries = await response.json();

        if (!append) {
            allEntries = entries;
            renderEntriesList(entries);
        } else {
            allEntries = [...allEntries, ...entries];
            renderEntriesList(allEntries);
        }

        // Show/hide load more button (shown only if full page returned)
        document.getElementById('loadMore').style.display =
            entries.length === pageSize ? 'block' : 'none';

        currentPage = page;
        currentSearch = search;
    } catch (error) {
        console.error('Error loading entries:', error);
        logError('UI', 'Failed to load entries', error.toString());
    }
}

/**
 * Applies filter checkboxes and reloads the conversation list.
 * Called when "Show Deleted" or "Favorites Only" checkboxes change.
 */
function applyFilters() {
    includeDeleted = document.getElementById('showDeleted').checked;
    showFavoritesOnly = document.getElementById('showFavoritesOnly').checked;
    loadEntries(currentSearch, 1, false);
}

/**
 * Renders the conversation list grouped by CLI session (section).
 * Each section shows date/time and tool (Claude Code).
 * Each entry has inline favorite/delete buttons.
 * @param {Array} entries - Array of EntryListDto objects from API
 */
function renderEntriesList(entries) {
    const container = document.getElementById('entriesList');

    if (entries.length === 0) {
        container.innerHTML = '<div class="text-center text-muted p-4">No conversations found</div>';
        return;
    }

    // Group by session (CLI session)
    const sessions = {};
    entries.forEach(entry => {
        if (!sessions[entry.sessionId]) {
            sessions[entry.sessionId] = {
                tool: entry.tool,
                createdAt: entry.sessionCreatedAt,
                entries: []
            };
        }
        sessions[entry.sessionId].entries.push(entry);
    });

    // Sort sessions by date desc (newest first)
    const sortedSections = Object.entries(sessions).sort((a, b) =>
        new Date(b[1].createdAt) - new Date(a[1].createdAt)
    );

    let html = '';
    sortedSections.forEach(([sessionId, session]) => {
        const sectionDate = new Date(session.createdAt).toLocaleDateString();
        const sectionTime = new Date(session.createdAt).toLocaleTimeString();
        const sectionDeleted = session.entries[0]?.sessionIsDeleted || false;
        
        const sectionDeleteTitle = sectionDeleted ? 'Restore section' : 'Delete section';
        const sectionClass = sectionDeleted ? 'deleted-entry' : '';
        html += `
            <div class="section-group mb-3">
                <div class="section-header p-2 bg-light fw-bold text-muted small d-flex align-items-center ${sectionClass}">
                    <span class="flex-grow-1">${sectionDate} ${sectionTime} - ${session.tool}</span>
                    <button class="btn btn-sm btn-link p-0 me-2"
                            onclick="event.stopPropagation(); showAddEntryPanel('${sessionId}')"
                            title="Add entry to this session">
                        ➕
                    </button>
                    <button class="btn btn-sm btn-link p-0 delete-btn"
                            onclick="event.stopPropagation(); toggleSessionDeleted('${sessionId}', ${!sectionDeleted})"
                            title="${sectionDeleteTitle}">
                        ${sectionDeleted ? '↩' : '🗑'}
                    </button>
                </div>
                <div class="entries-in-section">
        `;

        session.entries.forEach(entry => {
            const entryDate = new Date(entry.createdAt).toLocaleDateString();
            const entryTime = new Date(entry.createdAt).toLocaleTimeString();
            const entryDateTime = `${entryDate} ${entryTime}`;
            const isSelected = entry.id === selectedEntryId;
            const deleteTitle = entry.isDeleted ? 'Restore' : 'Delete';
            html += `
                <div class="entry-item p-2 border-bottom ${isSelected ? 'selected' : ''} "
                     data-entry-id="${entry.id}"
                     title="${entryDateTime}">
                    <div class="d-flex align-items-center gap-2">
                        <button class="btn btn-sm btn-link p-0 favorite-btn"
                                onclick="event.stopPropagation(); toggleFavoriteInline(${entry.id}, ${!entry.isFavorite})"
                                title="${entry.isFavorite ? 'Remove from favorites' : 'Add to favorites'}">
                            ${entry.isFavorite ? '★' : '☆'}
                        </button>
                        <div class="entry-title flex-grow-1" onclick="selectEntry(${entry.id})" style="cursor: pointer;">
                            ${escapeHtml(entry.title)}
                        </div>
                        <button class="btn btn-sm btn-link p-0 delete-btn"
                                onclick="event.stopPropagation(); toggleDeletedInline(${entry.id}, ${!entry.isDeleted})"
                                title="${deleteTitle}">
                            ${entry.isDeleted ? '↩' : '🗑'}
                        </button>
                    </div>
                </div>
            `;
        });

        html += `
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Select and load entry detail
async function selectEntry(id) {
    selectedEntryId = id;

    // Update selection highlight
    document.querySelectorAll('.entry-item').forEach(item => {
        item.classList.remove('selected');
    });
    document.querySelector(`[data-entry-id="${id}"]`)?.classList.add('selected');

    try {
        const response = await fetch(`/api/entries/${id}`);
        const entry = await response.json();
        renderEntryDetail(entry);
    } catch (error) {
        console.error('Error loading entry detail:', error);
        logError('UI', 'Failed to load entry detail', error.toString());
    }
}

// Render entry detail with markdown
function renderEntryDetail(entry) {
    const detailView = document.getElementById('detailView');
    const timestamp = new Date(entry.createdAt).toLocaleString();
    const question = (entry.question || '').trim();
    const response = (entry.response || '').trim();
    const title = (entry.title || '').trim();

    const favoriteClass = entry.isFavorite ? 'btn-warning' : 'btn-outline-warning';
    const deleteClass = entry.isDeleted ? 'btn-danger' : 'btn-outline-danger';
    const deleteText = entry.isDeleted ? 'Restore' : 'Delete';

    detailView.innerHTML = `
        <div class="entry-detail">
            <div class="mb-3 d-flex justify-content-between align-items-start">
                <div style="flex: 1;">
                    <div class="d-flex align-items-center gap-2">
                        <h4 id="entryTitle" class="mb-0" data-entry-id="${entry.id}" data-original-title="${escapeHtml(title)}">
                            ${escapeHtml(title)}
                        </h4>
                        <button id="editTitleBtn" class="btn btn-sm btn-link p-0 text-muted" onclick="startEditTitle()" title="Edit title" style="font-size: 1rem;">
                            ✏️
                        </button>
                        <button id="saveTitleBtn" class="btn btn-sm btn-link p-0 text-success" onclick="saveEditedTitle()" title="Save" style="font-size: 1rem; display: none;">
                            ✓
                        </button>
                        <button id="cancelTitleBtn" class="btn btn-sm btn-link p-0 text-danger" onclick="cancelEditTitle()" title="Cancel" style="font-size: 1rem; display: none;">
                            ✗
                        </button>
                    </div>
                    <div class="small text-muted">
                        ${timestamp} | Session: ${entry.sessionId} | ${entry.tool}
                    </div>
                </div>
                <div class="d-flex gap-2">
                    <button class="btn btn-sm ${favoriteClass}" onclick="toggleFavorite(${entry.id}, ${!entry.isFavorite})" title="">
                         Favorite
                    </button>
                    <button class="btn btn-sm ${deleteClass}" onclick="toggleDeleted(${entry.id}, ${!entry.isDeleted})" title="">
                        ${deleteText}
                    </button>
                </div>
            </div>

            <div class="mb-4">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h5>Question</h5>
                    <div class="d-flex gap-2">
                        <button id="editQuestionBtn" class="btn btn-sm btn-link p-0 text-muted" onclick="startEditQuestion()" title="Edit question" style="font-size: 1rem;">
                            ✏️
                        </button>
                        <button id="saveQuestionBtn" class="btn btn-sm btn-link p-0 text-success" onclick="saveEditedQuestion()" title="Save" style="font-size: 1rem; display: none;">
                            ✓
                        </button>
                        <button id="cancelQuestionBtn" class="btn btn-sm btn-link p-0 text-danger" onclick="cancelEditQuestion()" title="Cancel" style="font-size: 1rem; display: none;">
                            ✗
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="copyToClipboard('question')">
                            Copy
                        </button>
                    </div>
                </div>
                <div id="question" class="p-3 bg-light border rounded" data-raw="${escapeHtml(question)}" data-entry-id="${entry.id}">
                    ${escapeHtml(question)}
                </div>
            </div>

            <div class="mb-4">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h5>Response</h5>
                    <div class="d-flex gap-2">
                        <button id="editResponseBtn" class="btn btn-sm btn-link p-0 text-muted" onclick="startEditResponse()" title="Edit response" style="font-size: 1rem;">
                            ✏️
                        </button>
                        <button id="saveResponseBtn" class="btn btn-sm btn-link p-0 text-success" onclick="saveEditedResponse()" title="Save" style="font-size: 1rem; display: none;">
                            ✓
                        </button>
                        <button id="cancelResponseBtn" class="btn btn-sm btn-link p-0 text-danger" onclick="cancelEditResponse()" title="Cancel" style="font-size: 1rem; display: none;">
                            ✗
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="copyToClipboard('response')">
                            Copy
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="copyBoth()">
                            Copy Both
                        </button>
                    </div>
                </div>
                <div id="response" class="p-3 bg-light border rounded markdown-content" data-raw="${escapeHtml(response)}" data-entry-id="${entry.id}">
                    ${renderMarkdown(response)}
                </div>
            </div>
        </div>
    `;
}

// Render markdown (client-side fallback)
function renderMarkdown(text) {
    // Simple markdown rendering - in production you'd use a library
    return escapeHtml(text)
        .replace(/```([^`]+)```/g, '<pre><code>$1</code></pre>')
        .replace(/`([^`]+)`/g, '<code>$1</code>')
        .replace(/\n/g, '<br>');
}

/**
 * Starts editing the title by converting it to an input field.
 */
function startEditTitle() {
    const titleElement = document.getElementById('entryTitle');
    const currentTitle = titleElement.textContent.trim();

    // Replace h4 with input
    const input = document.createElement('input');
    input.type = 'text';
    input.value = currentTitle;
    input.id = 'entryTitleInput';
    input.className = 'form-control form-control-sm';
    input.style.width = '100%';

    // Store entry ID on the input for later use
    input.dataset.entryId = titleElement.dataset.entryId;
    input.dataset.originalTitle = titleElement.dataset.originalTitle;

    titleElement.replaceWith(input);
    input.focus();
    input.select();

    // Toggle button visibility
    document.getElementById('editTitleBtn').style.display = 'none';
    document.getElementById('saveTitleBtn').style.display = 'inline-block';
    document.getElementById('cancelTitleBtn').style.display = 'inline-block';

    // Allow Enter key to save
    input.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            saveEditedTitle();
        } else if (e.key === 'Escape') {
            cancelEditTitle();
        }
    });
}

/**
 * Saves the edited title to the server.
 */
async function saveEditedTitle() {
    const input = document.getElementById('entryTitleInput');
    if (!input) return;

    const newTitle = input.value.trim();
    const entryId = input.dataset.entryId;

    try {
        const response = await fetch(`/api/entries/${entryId}/title`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title: newTitle })
        });

        if (response.ok) {
            // Replace input with h4
            const h4 = document.createElement('h4');
            h4.id = 'entryTitle';
            h4.className = 'mb-0';
            h4.dataset.entryId = entryId;
            h4.dataset.originalTitle = newTitle;
            h4.textContent = newTitle;

            input.replaceWith(h4);

            // Toggle button visibility
            document.getElementById('editTitleBtn').style.display = 'inline-block';
            document.getElementById('saveTitleBtn').style.display = 'none';
            document.getElementById('cancelTitleBtn').style.display = 'none';

            // Update in list
            const listItem = document.querySelector(`[data-entry-id="${entryId}"] .entry-title`);
            if (listItem) {
                listItem.textContent = newTitle;
            }

            showToast('Title updated!');
        } else {
            showToast('Failed to save title');
        }
    } catch (error) {
        console.error('Error saving title:', error);
        logError('UI', 'Failed to save title', error.toString());
        showToast('Error saving title');
    }
}

/**
 * Cancels editing the title and reverts to the original value.
 */
function cancelEditTitle() {
    const input = document.getElementById('entryTitleInput');
    if (!input) return;

    const originalTitle = input.dataset.originalTitle;
    const entryId = input.dataset.entryId;

    // Replace input with h4
    const h4 = document.createElement('h4');
    h4.id = 'entryTitle';
    h4.className = 'mb-0';
    h4.dataset.entryId = entryId;
    h4.dataset.originalTitle = originalTitle;
    h4.textContent = originalTitle;

    input.replaceWith(h4);

    // Toggle button visibility
    document.getElementById('editTitleBtn').style.display = 'inline-block';
    document.getElementById('saveTitleBtn').style.display = 'none';
    document.getElementById('cancelTitleBtn').style.display = 'none';
}

/**
 * Starts editing the question by converting it to a textarea field.
 */
function startEditQuestion() {
    const questionElement = document.getElementById('question');
    const currentQuestion = questionElement.dataset.raw || questionElement.textContent.trim();

    // Replace div with textarea
    const textarea = document.createElement('textarea');
    textarea.value = currentQuestion;
    textarea.id = 'questionTextarea';
    textarea.className = 'form-control';
    textarea.rows = 10;
    textarea.dataset.entryId = questionElement.dataset.entryId;
    textarea.dataset.originalValue = currentQuestion;

    questionElement.replaceWith(textarea);
    textarea.focus();

    // Toggle button visibility
    document.getElementById('editQuestionBtn').style.display = 'none';
    document.getElementById('saveQuestionBtn').style.display = 'inline-block';
    document.getElementById('cancelQuestionBtn').style.display = 'inline-block';
}

/**
 * Saves the edited question to the server.
 */
async function saveEditedQuestion() {
    const textarea = document.getElementById('questionTextarea');
    if (!textarea) return;

    const newQuestion = textarea.value.trim();
    const entryId = textarea.dataset.entryId;

    if (!newQuestion) {
        showToast('Question cannot be empty');
        return;
    }

    try {
        const response = await fetch(`/api/entries/${entryId}/question`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question: newQuestion })
        });

        if (response.ok) {
            // Replace textarea with div
            const div = document.createElement('div');
            div.id = 'question';
            div.className = 'p-3 bg-light border rounded';
            div.dataset.raw = newQuestion;
            div.dataset.entryId = entryId;
            div.textContent = newQuestion;

            textarea.replaceWith(div);

            // Toggle button visibility
            document.getElementById('editQuestionBtn').style.display = 'inline-block';
            document.getElementById('saveQuestionBtn').style.display = 'none';
            document.getElementById('cancelQuestionBtn').style.display = 'none';

            showToast('Question updated!');
        } else {
            showToast('Failed to save question');
        }
    } catch (error) {
        console.error('Error saving question:', error);
        logError('UI', 'Failed to save question', error.toString());
        showToast('Error saving question');
    }
}

/**
 * Cancels editing the question and reverts to the original value.
 */
function cancelEditQuestion() {
    const textarea = document.getElementById('questionTextarea');
    if (!textarea) return;

    const originalValue = textarea.dataset.originalValue;
    const entryId = textarea.dataset.entryId;

    // Replace textarea with div
    const div = document.createElement('div');
    div.id = 'question';
    div.className = 'p-3 bg-light border rounded';
    div.dataset.raw = originalValue;
    div.dataset.entryId = entryId;
    div.textContent = originalValue;

    textarea.replaceWith(div);

    // Toggle button visibility
    document.getElementById('editQuestionBtn').style.display = 'inline-block';
    document.getElementById('saveQuestionBtn').style.display = 'none';
    document.getElementById('cancelQuestionBtn').style.display = 'none';
}

/**
 * Starts editing the response by converting it to a textarea field.
 */
function startEditResponse() {
    const responseElement = document.getElementById('response');
    const currentResponse = responseElement.dataset.raw || responseElement.textContent.trim();

    // Replace div with textarea
    const textarea = document.createElement('textarea');
    textarea.value = currentResponse;
    textarea.id = 'responseTextarea';
    textarea.className = 'form-control';
    textarea.rows = 15;
    textarea.dataset.entryId = responseElement.dataset.entryId;
    textarea.dataset.originalValue = currentResponse;

    responseElement.replaceWith(textarea);
    textarea.focus();

    // Toggle button visibility
    document.getElementById('editResponseBtn').style.display = 'none';
    document.getElementById('saveResponseBtn').style.display = 'inline-block';
    document.getElementById('cancelResponseBtn').style.display = 'inline-block';
}

/**
 * Saves the edited response to the server.
 */
async function saveEditedResponse() {
    const textarea = document.getElementById('responseTextarea');
    if (!textarea) return;

    const newResponse = textarea.value.trim();
    const entryId = textarea.dataset.entryId;

    if (!newResponse) {
        showToast('Response cannot be empty');
        return;
    }

    try {
        const response = await fetch(`/api/entries/${entryId}/response`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ response: newResponse })
        });

        if (response.ok) {
            // Replace textarea with div
            const div = document.createElement('div');
            div.id = 'response';
            div.className = 'p-3 bg-light border rounded markdown-content';
            div.dataset.raw = newResponse;
            div.dataset.entryId = entryId;
            div.innerHTML = renderMarkdown(newResponse);

            textarea.replaceWith(div);

            // Toggle button visibility
            document.getElementById('editResponseBtn').style.display = 'inline-block';
            document.getElementById('saveResponseBtn').style.display = 'none';
            document.getElementById('cancelResponseBtn').style.display = 'none';

            showToast('Response updated!');
        } else {
            showToast('Failed to save response');
        }
    } catch (error) {
        console.error('Error saving response:', error);
        logError('UI', 'Failed to save response', error.toString());
        showToast('Error saving response');
    }
}

/**
 * Cancels editing the response and reverts to the original value.
 */
function cancelEditResponse() {
    const textarea = document.getElementById('responseTextarea');
    if (!textarea) return;

    const originalValue = textarea.dataset.originalValue;
    const entryId = textarea.dataset.entryId;

    // Replace textarea with div
    const div = document.createElement('div');
    div.id = 'response';
    div.className = 'p-3 bg-light border rounded markdown-content';
    div.dataset.raw = originalValue;
    div.dataset.entryId = entryId;
    div.innerHTML = renderMarkdown(originalValue);

    textarea.replaceWith(div);

    // Toggle button visibility
    document.getElementById('editResponseBtn').style.display = 'inline-block';
    document.getElementById('saveResponseBtn').style.display = 'none';
    document.getElementById('cancelResponseBtn').style.display = 'none';
}

// Copy to clipboard
async function copyToClipboard(elementId) {
    const element = document.getElementById(elementId);
    const text = element.dataset.raw || element.textContent;

    try {
        await navigator.clipboard.writeText(text);
        showToast('Copied to clipboard!');
    } catch (error) {
        console.error('Copy failed:', error);
    }
}

// Copy both question and response
async function copyBoth() {
    const question = document.getElementById('question').textContent;
    const response = document.getElementById('response').dataset.raw ||
                     document.getElementById('response').textContent;
    const combined = `Q: ${question}\n\nA: ${response}`;

    try {
        await navigator.clipboard.writeText(combined);
        showToast('Copied both to clipboard!');
    } catch (error) {
        console.error('Copy failed:', error);
    }
}

// Show toast notification
function showToast(message) {
    // Simple toast - in production you'd use Bootstrap toast or similar
    const toast = document.createElement('div');
    toast.className = 'position-fixed top-0 end-0 m-3 p-3 bg-success text-white rounded';
    toast.textContent = message;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 2000);
}

// Load more entries
function loadMoreEntries() {
    loadEntries(currentSearch, currentPage + 1, true);
}

// Toggle favorite status (from detail view)
async function toggleFavorite(id, isFavorite) {
    try {
        const response = await fetch(`/api/entries/${id}/favorite`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isFavorite })
        });

        if (response.ok) {
            showToast(isFavorite ? 'Added to favorites!' : 'Removed from favorites!');
            // Reload current entry to update UI
            await selectEntry(id);
            // Reload list to reflect changes
            loadEntries(currentSearch, 1, false);
        }
    } catch (error) {
        console.error('Failed to toggle favorite:', error);
        logError('UI', 'Failed to toggle favorite', error.toString());
    }
}

// Toggle favorite status (inline from list)
async function toggleFavoriteInline(id, isFavorite) {
    try {
        const response = await fetch(`/api/entries/${id}/favorite`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isFavorite })
        });

        if (response.ok) {
            // Reload list to reflect changes
            loadEntries(currentSearch, 1, false);
            // If this entry is currently selected, refresh detail view
            if (selectedEntryId === id) {
                await selectEntry(id);
            }
        }
    } catch (error) {
        console.error('Failed to toggle favorite:', error);
        logError('UI', 'Failed to toggle favorite', error.toString());
    }
}

// Toggle deleted status (from detail view)
async function toggleDeleted(id, isDeleted) {
    try {
        const response = await fetch(`/api/entries/${id}/deleted`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isDeleted })
        });

        if (response.ok) {
            showToast(isDeleted ? 'Marked as deleted!' : 'Restored!');
            // Reload current entry to update UI
            await selectEntry(id);
            // Reload list to reflect changes
            loadEntries(currentSearch, 1, false);
        }
    } catch (error) {
        console.error('Failed to toggle deleted:', error);
        logError('UI', 'Failed to toggle deleted', error.toString());
    }
}

// Toggle deleted status (inline from list)
async function toggleDeletedInline(id, isDeleted) {
    try {
        const response = await fetch(`/api/entries/${id}/deleted`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isDeleted })
        });

        if (response.ok) {
            // Reload list to reflect changes
            loadEntries(currentSearch, 1, false);
            // If this entry is currently selected, refresh detail view
            if (selectedEntryId === id) {
                await selectEntry(id);
            }
        }
    } catch (error) {
        console.error('Failed to toggle deleted:', error);
        logError('UI', 'Failed to toggle deleted', error.toString());
    }
}

// Toggle session deleted status
async function toggleSessionDeleted(sessionId, isDeleted) {
    try {
        const response = await fetch(`/api/sessions/${sessionId}/deleted`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isDeleted })
        });

        if (response.ok) {
            showToast(isDeleted ? 'Session deleted!' : 'Session restored!');
            // Reload list to reflect changes
            loadEntries(currentSearch, 1, false);
        }
    } catch (error) {
        console.error('Failed to toggle session deleted:', error);
        logError('UI', 'Failed to toggle session deleted', error.toString());
    }
}

// Log error to API
async function logError(source, message, detail) {
    try {
        await fetch('/api/errors', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ source, message, detail })
        });
    } catch (err) {
        console.error('Failed to log error:', err);
    }
}

// Escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Panel resize and toggle functionality
let isResizing = false;
let leftPanelWidth = localStorage.getItem('leftPanelWidth') || 400;

function initializePanelResize() {
    const leftPanel = document.getElementById('leftPanel');
    const resizeHandle = document.getElementById('resizeHandle');

    // Set initial width from localStorage
    leftPanel.style.width = `${leftPanelWidth}px`;

    // Mouse down on resize handle
    resizeHandle.addEventListener('mousedown', (e) => {
        isResizing = true;
        resizeHandle.classList.add('resizing');
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
    });

    // Mouse move - resize panel
    document.addEventListener('mousemove', (e) => {
        if (!isResizing) return;

        const newWidth = e.clientX;
        if (newWidth >= 200 && newWidth <= 800) {
            leftPanel.style.width = `${newWidth}px`;
            leftPanelWidth = newWidth;
        }
    });

    // Mouse up - stop resizing
    document.addEventListener('mouseup', () => {
        if (isResizing) {
            isResizing = false;
            resizeHandle.classList.remove('resizing');
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            localStorage.setItem('leftPanelWidth', leftPanelWidth);
        }
    });
}

function toggleLeftPanel() {
    const leftPanel = document.getElementById('leftPanel');
    const resizeHandle = document.getElementById('resizeHandle');

    if (leftPanel.classList.contains('collapsed')) {
        // Expand
        leftPanel.classList.remove('collapsed');
        leftPanel.style.width = `${leftPanelWidth}px`;
        resizeHandle.style.display = 'block';
    } else {
        // Collapse
        leftPanel.classList.add('collapsed');
        resizeHandle.style.display = 'none';
    }
}

/**
 * Shows the panel to add a new entry to a specific session.
 * @param {string} sessionId - The session ID to add the entry to
 */
function showAddEntryPanel(sessionId) {
    currentSessionForNewEntry = sessionId;
    isAddEntryPanelOpen = true;

    const detailView = document.getElementById('detailView');
    detailView.innerHTML = `
        <div class="add-entry-panel">
            <h4 class="mb-3">Add New Entry</h4>
            <div class="mb-3">
                <label for="newQuestion" class="form-label">Question</label>
                <textarea id="newQuestion" class="form-control" rows="5" placeholder="Enter question..."></textarea>
            </div>
            <div class="mb-3">
                <label for="newResponse" class="form-label">Response</label>
                <textarea id="newResponse" class="form-control" rows="10" placeholder="Enter response..."></textarea>
            </div>
            <div class="d-flex gap-2">
                <button class="btn btn-primary" onclick="saveNewEntry()">Save</button>
                <button class="btn btn-secondary" onclick="cancelAddEntry()">Cancel</button>
            </div>
        </div>
    `;

    // Focus on the question field
    document.getElementById('newQuestion').focus();
}

/**
 * Saves the new entry to the session using the API.
 */
async function saveNewEntry() {
    const question = document.getElementById('newQuestion').value.trim();
    const response = document.getElementById('newResponse').value.trim();

    // Validate inputs
    if (!question) {
        showToast('Please enter a question');
        return;
    }
    if (!response) {
        showToast('Please enter a response');
        return;
    }

    try {
        // Call API to create entry
        const apiResponse = await fetch('/api/entries', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionId: currentSessionForNewEntry,
                question: question,
                response: response
            })
        });

        if (apiResponse.ok) {
            const result = await apiResponse.json();
            showToast('Entry added successfully!');

            // Close panel and reload entries
            cancelAddEntry();
            loadEntries(currentSearch, 1, false);

            // Select the newly created entry
            if (result.id) {
                setTimeout(() => selectEntry(result.id), 500);
            }
        } else {
            const errorText = await apiResponse.text();
            showToast('Failed to save entry: ' + errorText);
            logError('UI', 'Failed to save new entry', errorText);
        }
    } catch (error) {
        console.error('Error saving entry:', error);
        showToast('Error saving entry');
        logError('UI', 'Error saving new entry', error.toString());
    }
}

/**
 * Cancels adding a new entry and returns to the previous view.
 */
function cancelAddEntry() {
    isAddEntryPanelOpen = false;
    currentSessionForNewEntry = null;

    // Return to default view
    const detailView = document.getElementById('detailView');
    detailView.innerHTML = `
        <div class="text-center text-muted p-5">
            <h4>Select a conversation to view details</h4>
        </div>
    `;
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    loadEntries();

    // Initialize panel resize
    initializePanelResize();

    // Toggle left panel button
    document.getElementById('toggleLeftPanel').addEventListener('click', toggleLeftPanel);

    // Keyboard shortcut: Ctrl+B to toggle left panel
    document.addEventListener('keydown', (e) => {
        if (e.ctrlKey && e.key === 'b') {
            e.preventDefault();
            toggleLeftPanel();
        }
    });

    // Search with debounce
    const searchInput = document.getElementById('searchInput');
    searchInput.addEventListener('input', debounce(() => {
        loadEntries(searchInput.value);
    }, 300));

    // Clear search
    document.getElementById('clearSearch').addEventListener('click', () => {
        searchInput.value = '';
        loadEntries('');
    });
});




